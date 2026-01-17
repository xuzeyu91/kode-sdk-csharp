using System.Text.Json;

namespace Kode.Agent.Sdk.Core.Agent;

internal readonly record struct ToolInputValidationResult(
    bool Ok,
    string? Error = null,
    IReadOnlyList<string>? RequiredKeys = null
);

internal static class ToolInputValidator
{
    public static ToolInputValidationResult Validate(object? schemaObj, object? inputObj)
    {
        if (schemaObj == null)
        {
            return new ToolInputValidationResult(true);
        }

        JsonElement schema;
        try
        {
            schema = schemaObj is JsonElement je ? je : JsonSerializer.SerializeToElement(schemaObj);
        }
        catch
        {
            // If schema is not serializable, skip validation.
            return new ToolInputValidationResult(true);
        }

        JsonElement input = JsonSerializer.SerializeToElement(inputObj ?? new { });

        if (schema.ValueKind != JsonValueKind.Object)
        {
            return new ToolInputValidationResult(true);
        }

        var schemaType = GetString(schema, "type");
        if (string.Equals(schemaType, "object", StringComparison.OrdinalIgnoreCase))
        {
            if (input.ValueKind != JsonValueKind.Object)
            {
                return new ToolInputValidationResult(false, "Input must be a JSON object");
            }

            var requiredKeys = GetStringArray(schema, "required");
            if (requiredKeys.Count > 0)
            {
                foreach (var key in requiredKeys)
                {
                    if (!input.TryGetProperty(key, out _))
                    {
                        return new ToolInputValidationResult(
                            false,
                            $"Missing required key: {key}",
                            requiredKeys);
                    }
                }
            }

            var additionalPropsAllowed = true;
            if (schema.TryGetProperty("additionalProperties", out var ap) &&
                (ap.ValueKind == JsonValueKind.True || ap.ValueKind == JsonValueKind.False))
            {
                additionalPropsAllowed = ap.GetBoolean();
            }

            HashSet<string>? definedProps = null;
            if (schema.TryGetProperty("properties", out var props) && props.ValueKind == JsonValueKind.Object)
            {
                definedProps = props.EnumerateObject()
                    .Select(p => p.Name)
                    .ToHashSet(StringComparer.Ordinal);

                foreach (var kv in input.EnumerateObject())
                {
                    if (!definedProps.Contains(kv.Name) && !additionalPropsAllowed)
                    {
                        return new ToolInputValidationResult(false, $"Unexpected key: {kv.Name}", requiredKeys);
                    }
                }

                foreach (var prop in props.EnumerateObject())
                {
                    if (!input.TryGetProperty(prop.Name, out var value)) continue;
                    if (!ValidateType(prop.Value, value, out var err))
                    {
                        return new ToolInputValidationResult(false, $"Invalid type for '{prop.Name}': {err}", requiredKeys);
                    }
                }
            }

            return new ToolInputValidationResult(true, RequiredKeys: requiredKeys);
        }

        // If schema doesn't specify type, skip strict validation.
        return new ToolInputValidationResult(true);
    }

    private static bool ValidateType(JsonElement propSchema, JsonElement value, out string error)
    {
        error = "schema mismatch";
        if (propSchema.ValueKind != JsonValueKind.Object)
        {
            error = "invalid schema";
            return true;
        }

        if (propSchema.TryGetProperty("enum", out var enumEl) && enumEl.ValueKind == JsonValueKind.Array)
        {
            foreach (var option in enumEl.EnumerateArray())
            {
                if (JsonElementDeepEquals(option, value))
                {
                    error = "";
                    return true;
                }
            }
            error = "value not in enum";
            return false;
        }

        var type = GetString(propSchema, "type");
        if (string.IsNullOrWhiteSpace(type))
        {
            // No type => don't enforce.
            error = "";
            return true;
        }

        switch (type)
        {
            case "string":
                if (value.ValueKind == JsonValueKind.String) { error = ""; return true; }
                error = "expected string";
                return false;
            case "boolean":
                if (value.ValueKind is JsonValueKind.True or JsonValueKind.False) { error = ""; return true; }
                error = "expected boolean";
                return false;
            case "integer":
                if (value.ValueKind == JsonValueKind.Number && value.TryGetInt64(out _)) { error = ""; return true; }
                error = "expected integer";
                return false;
            case "number":
                if (value.ValueKind == JsonValueKind.Number) { error = ""; return true; }
                error = "expected number";
                return false;
            case "object":
                if (value.ValueKind == JsonValueKind.Object) { error = ""; return true; }
                error = "expected object";
                return false;
            case "array":
                if (value.ValueKind == JsonValueKind.Array) { error = ""; return true; }
                error = "expected array";
                return false;
            default:
                // Unsupported type keyword => don't enforce.
                error = "";
                return true;
        }
    }

    private static string? GetString(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var el)) return null;
        return el.ValueKind == JsonValueKind.String ? el.GetString() : null;
    }

    private static IReadOnlyList<string> GetStringArray(JsonElement obj, string key)
    {
        if (!obj.TryGetProperty(key, out var el) || el.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        foreach (var item in el.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String)
            {
                var s = item.GetString();
                if (!string.IsNullOrWhiteSpace(s))
                {
                    list.Add(s);
                }
            }
        }
        return list;
    }

    private static bool JsonElementDeepEquals(JsonElement a, JsonElement b)
    {
        if (a.ValueKind != b.ValueKind)
        {
            // Attempt numeric coercion
            if (a.ValueKind == JsonValueKind.Number && b.ValueKind == JsonValueKind.Number)
            {
                return a.GetDouble().Equals(b.GetDouble());
            }
            return false;
        }

        return a.ValueKind switch
        {
            JsonValueKind.String => a.GetString() == b.GetString(),
            JsonValueKind.Number => a.GetDouble().Equals(b.GetDouble()),
            JsonValueKind.True => b.ValueKind == JsonValueKind.True,
            JsonValueKind.False => b.ValueKind == JsonValueKind.False,
            JsonValueKind.Null => true,
            JsonValueKind.Object => a.EnumerateObject().All(p => b.TryGetProperty(p.Name, out var vb) && JsonElementDeepEquals(p.Value, vb)) &&
                                   b.EnumerateObject().All(p => a.TryGetProperty(p.Name, out var va) && JsonElementDeepEquals(p.Value, va)),
            JsonValueKind.Array => a.EnumerateArray().Select((v, i) => (v, i)).All(t =>
            {
                var arrB = b.EnumerateArray().ToArray();
                return t.i < arrB.Length && JsonElementDeepEquals(t.v, arrB[t.i]);
            }),
            _ => a.ToString() == b.ToString()
        };
    }
}

