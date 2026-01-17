using System.Text.Json;

namespace Kode.Agent.Sdk.Core.Agent;

public static class PermissionModes
{
    public const string DecisionAllow = "allow";
    public const string DecisionDeny = "deny";
    public const string DecisionAsk = "ask";

    public sealed record PermissionEvaluationContext
    {
        public required string ToolName { get; init; }
        public ToolDescriptor? Descriptor { get; init; }
        public required PermissionConfig Config { get; init; }
    }

    public delegate string PermissionModeHandler(PermissionEvaluationContext ctx);

    public sealed record SerializedPermissionMode
    {
        public required string Name { get; init; }
        public required bool BuiltIn { get; init; }
    }

    public sealed class PermissionModeRegistry
    {
        private readonly Dictionary<string, PermissionModeHandler> _handlers = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _customModes = new(StringComparer.OrdinalIgnoreCase);

        public void Register(string mode, PermissionModeHandler handler, bool isBuiltIn = false)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(mode);
            ArgumentNullException.ThrowIfNull(handler);

            _handlers[mode] = handler;
            if (!isBuiltIn)
            {
                _customModes.Add(mode);
            }
            else
            {
                _customModes.Remove(mode);
            }
        }

        public PermissionModeHandler? Get(string mode)
        {
            if (string.IsNullOrWhiteSpace(mode)) return null;
            return _handlers.GetValueOrDefault(mode);
        }

        public IReadOnlyList<string> List() => _handlers.Keys.ToList();

        public IReadOnlyList<SerializedPermissionMode> Serialize()
        {
            return _handlers.Keys
                .Select(name => new SerializedPermissionMode
                {
                    Name = name,
                    BuiltIn = !_customModes.Contains(name)
                })
                .ToList();
        }

        public IReadOnlyList<string> ValidateRestore(IEnumerable<SerializedPermissionMode> serialized)
        {
            var missing = new List<string>();
            foreach (var mode in serialized)
            {
                if (!mode.BuiltIn && !_handlers.ContainsKey(mode.Name))
                {
                    missing.Add(mode.Name);
                }
            }
            return missing;
        }
    }

    public static PermissionModeRegistry Registry { get; } = CreateDefaultRegistry();

    private static PermissionModeRegistry CreateDefaultRegistry()
    {
        var registry = new PermissionModeRegistry();

        registry.Register("auto", _ => DecisionAllow, isBuiltIn: true);
        registry.Register("approval", _ => DecisionAsk, isBuiltIn: true);
        registry.Register("readonly", ctx =>
        {
            var metadata = ctx.Descriptor?.Metadata;
            if (metadata == null || metadata.Count == 0)
            {
                return DecisionAsk;
            }

            if (TryReadBool(metadata!, "mutates", out var mutates))
            {
                return mutates ? DecisionDeny : DecisionAllow;
            }

            if (TryReadString(metadata!, "access", out var access))
            {
                access = access.ToLowerInvariant();
                if (access is "write" or "execute" or "manage" or "mutate")
                {
                    return DecisionDeny;
                }
                return DecisionAllow;
            }

            return DecisionAsk;
        }, isBuiltIn: true);

        return registry;
    }

    private static bool TryReadBool(IDictionary<string, object?> dict, string key, out bool value)
    {
        value = false;
        if (!dict.TryGetValue(key, out var obj) || obj is null) return false;
        if (obj is bool b)
        {
            value = b;
            return true;
        }
        if (obj is JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.True:
                    value = true;
                    return true;
                case JsonValueKind.False:
                    value = false;
                    return true;
                case JsonValueKind.String:
                {
                    var s1 = element.GetString();
                    if (s1 != null && bool.TryParse(s1, out var parsed1))
                    {
                        value = parsed1;
                        return true;
                    }
                    return false;
                }
            }
        }
        if (obj is string s && bool.TryParse(s, out var parsed))
        {
            value = parsed;
            return true;
        }
        return false;
    }

    private static bool TryReadString(IDictionary<string, object?> dict, string key, out string value)
    {
        value = "";
        if (!dict.TryGetValue(key, out var obj) || obj is null) return false;
        if (obj is string s)
        {
            value = s;
            return true;
        }
        if (obj is JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                value = element.GetString() ?? "";
                return !string.IsNullOrWhiteSpace(value);
            }

            // For non-string JSON values, preserve TS-like "best effort" behavior by using raw JSON text.
            value = element.GetRawText();
            return !string.IsNullOrWhiteSpace(value);
        }
        value = obj.ToString() ?? "";
        return !string.IsNullOrWhiteSpace(value);
    }
}
