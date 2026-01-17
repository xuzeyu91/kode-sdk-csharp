namespace Kode.Agent.Sdk.Tools;

/// <summary>
/// Registry for tool factories.
/// </summary>
public sealed class ToolRegistry : IToolRegistry
{
    private sealed class DescriptorAwareTool : ITool
    {
        private readonly ITool _inner;
        private readonly string _registryId;
        private readonly Dictionary<string, object>? _config;
        private readonly ToolSource _source;

        public DescriptorAwareTool(ITool inner, string registryId, Dictionary<string, object>? config, ToolSource source)
        {
            _inner = inner;
            _registryId = registryId;
            _config = config;
            _source = source;
        }

        public string Name => _inner.Name;
        public string Description => _inner.Description;
        public object InputSchema => _inner.InputSchema;
        public ToolAttributes Attributes => _inner.Attributes;
        public ValueTask<string?> GetPromptAsync(ToolContext context) => _inner.GetPromptAsync(context);

        public Task<ToolResult> ExecuteAsync(object arguments, ToolContext context, CancellationToken cancellationToken = default) =>
            _inner.ExecuteAsync(arguments, context, cancellationToken);

        public ToolDescriptor ToDescriptor()
        {
            var baseDescriptor = _inner.ToDescriptor();
            var meta = baseDescriptor.Metadata != null
                ? new Dictionary<string, object?>(baseDescriptor.Metadata!, StringComparer.OrdinalIgnoreCase)
                : new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

            // TS permission-modes.readonly relies on descriptor.metadata.mutates/access.
            if (!meta.ContainsKey("mutates"))
            {
                meta["mutates"] = !_inner.Attributes.ReadOnly;
            }

            if (!meta.ContainsKey("access"))
            {
                meta["access"] = _inner.Attributes.ReadOnly
                    ? "read"
                    : _inner.Name.StartsWith("bash_", StringComparison.OrdinalIgnoreCase)
                        ? "execute"
                        : "write";
            }

            // Extra hints (best-effort; safe for consumers).
            if (!meta.ContainsKey("readOnly")) meta["readOnly"] = _inner.Attributes.ReadOnly;
            if (!meta.ContainsKey("noEffect")) meta["noEffect"] = _inner.Attributes.NoEffect;
            if (!meta.ContainsKey("requiresApproval")) meta["requiresApproval"] = _inner.Attributes.RequiresApproval;

#pragma warning disable CS8619 // 值中的引用类型的为 Null 性与目标类型不匹配。
      return baseDescriptor with
            {
                Source = baseDescriptor.Source == ToolSource.Builtin || baseDescriptor.Source == ToolSource.Mcp
                    ? baseDescriptor.Source
                    : _source,
                Name = string.IsNullOrWhiteSpace(baseDescriptor.Name) ? Name : baseDescriptor.Name,
                RegistryId = string.IsNullOrWhiteSpace(baseDescriptor.RegistryId) ? _registryId : baseDescriptor.RegistryId,
                Config = baseDescriptor.Config ?? (_config != null ? new Dictionary<string, object>(_config) : null),
                Metadata = meta.Count > 0 ? meta : null
            };
#pragma warning restore CS8619 // 值中的引用类型的为 Null 性与目标类型不匹配。
    }
    }

    private static ITool Wrap(ITool tool, string registryId, Dictionary<string, object>? config, ToolSource source)
    {
        if (tool is DescriptorAwareTool) return tool;
        return new DescriptorAwareTool(tool, registryId, config, source);
    }

    private readonly Dictionary<string, Func<ToolFactoryContext, ITool>> _factories = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, ITool> _instances = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _lock = new();

    /// <inheritdoc />
    public void Register(string id, Func<ToolFactoryContext, ITool> factory)
    {
        lock (_lock)
        {
            _factories[id] = ctx =>
            {
                var tool = factory(ctx);
                return Wrap(tool, id, ctx.Config, ToolSource.Registered);
            };
        }
    }

    /// <inheritdoc />
    public void Register(ITool tool)
    {
        lock (_lock)
        {
            var wrapped = Wrap(tool, tool.Name, config: null, source: ToolSource.Builtin);
            _instances[tool.Name] = wrapped;
            _factories[tool.Name] = _ => wrapped;
        }
    }

    /// <summary>
    /// Registers a tool type that has a parameterless constructor.
    /// </summary>
    public void Register<TTool>() where TTool : ITool, new()
    {
        var instance = new TTool();
        Register(instance);
    }

    /// <inheritdoc />
    public ITool Create(string id, Dictionary<string, object>? config = null)
    {
        Func<ToolFactoryContext, ITool>? factory;
        lock (_lock)
        {
            if (!_factories.TryGetValue(id, out factory))
            {
                throw new Core.ToolNotFoundException(id);
            }
        }

        var context = new ToolFactoryContext { Config = config };
        return factory(context);
    }

    /// <inheritdoc />
    public bool Has(string id)
    {
        lock (_lock)
        {
            return _factories.ContainsKey(id);
        }
    }

    /// <inheritdoc />
    public IReadOnlyList<string> List()
    {
        lock (_lock)
        {
            return _factories.Keys.ToList();
        }
    }

    /// <inheritdoc />
    public ITool? Get(string name)
    {
        lock (_lock)
        {
            if (_instances.TryGetValue(name, out var tool))
            {
                return tool;
            }

            if (_factories.TryGetValue(name, out var factory))
            {
                var instance = factory(new ToolFactoryContext());
                _instances[name] = instance;
                return instance;
            }

            return null;
        }
    }

    /// <summary>
    /// Creates a global default registry with built-in tools.
    /// </summary>
    public static ToolRegistry CreateDefault()
    {
        var registry = new ToolRegistry();
        // Built-in tools will be registered from the Kode.Agent.Tools.Builtin package
        return registry;
    }
}
