using System.Reflection;
using System.Text.Json;

namespace Kode.Agent.Sdk.Tools;

/// <summary>
/// Utilities for building JSON Schema from types.
/// </summary>
public static class JsonSchemaBuilder
{
    /// <summary>
    /// Builds a JSON Schema object from a type.
    /// </summary>
    public static object BuildSchema<T>() => BuildSchema(typeof(T));

    /// <summary>
    /// Builds a JSON Schema object from a type.
    /// </summary>
    public static object BuildSchema(Type type)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var prop in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var paramAttr = prop.GetCustomAttribute<ToolParameterAttribute>();
            var propSchema = BuildPropertySchema(prop.PropertyType, paramAttr?.Description ?? "");

            var propName = JsonNamingPolicy.CamelCase.ConvertName(prop.Name);
            properties[propName] = propSchema;

            if (paramAttr?.Required != false)
            {
                required.Add(propName);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    /// <summary>
    /// Builds a JSON Schema from a parameter list.
    /// </summary>
    public static object BuildSchema(params (string Name, string Type, string Description, bool Required)[] parameters)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var (name, type, description, isRequired) in parameters)
        {
            properties[name] = new Dictionary<string, object>
            {
                ["type"] = type,
                ["description"] = description
            };

            if (isRequired)
            {
                required.Add(name);
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["type"] = "object",
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        return schema;
    }

    private static object BuildPropertySchema(Type type, string description)
    {
        var schema = new Dictionary<string, object>();

        if (!string.IsNullOrEmpty(description))
        {
            schema["description"] = description;
        }

        var (jsonType, format) = GetJsonType(type);
        schema["type"] = jsonType;

        if (!string.IsNullOrEmpty(format))
        {
            schema["format"] = format;
        }

        // Handle nullable types
        var underlyingType = Nullable.GetUnderlyingType(type);
        if (underlyingType != null)
        {
            var (innerType, innerFormat) = GetJsonType(underlyingType);
            schema["type"] = new[] { innerType, "null" };
            if (!string.IsNullOrEmpty(innerFormat))
            {
                schema["format"] = innerFormat;
            }
        }

        // Handle arrays and collections
        if (type.IsArray ||
            (type.IsGenericType && (type.GetGenericTypeDefinition() == typeof(List<>) ||
                                     type.GetGenericTypeDefinition() == typeof(IReadOnlyList<>) ||
                                     type.GetGenericTypeDefinition() == typeof(IEnumerable<>))))
        {
            var elementType = type.IsArray ? type.GetElementType()! : type.GetGenericArguments()[0];
            schema["type"] = "array";
            schema["items"] = BuildPropertySchema(elementType, "");
        }

        // Handle enums
        if (type.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(type);
        }

        return schema;
    }

    private static (string Type, string? Format) GetJsonType(Type type)
    {
        if (type == typeof(string)) return ("string", null);
        if (type == typeof(int) || type == typeof(long) || type == typeof(short) || type == typeof(byte)) return ("integer", null);
        if (type == typeof(float) || type == typeof(double) || type == typeof(decimal)) return ("number", null);
        if (type == typeof(bool)) return ("boolean", null);
        if (type == typeof(DateTime) || type == typeof(DateTimeOffset)) return ("string", "date-time");
        if (type == typeof(Guid)) return ("string", "uuid");
        if (type == typeof(Uri)) return ("string", "uri");
        return ("object", null);
    }
}
