namespace Kode.Agent.Sdk.Tools;

/// <summary>
/// Base class for creating a toolkit (collection of related tools).
/// </summary>
public abstract class ToolKit
{
    private readonly List<ITool> _tools = [];

    /// <summary>
    /// Gets the tools in this toolkit.
    /// </summary>
    public IReadOnlyList<ITool> Tools => _tools;

    /// <summary>
    /// Gets the toolkit prefix for tool names.
    /// </summary>
    protected virtual string? Prefix => null;

    /// <summary>
    /// Called during initialization to register tools.
    /// </summary>
    protected abstract void RegisterTools();

    /// <summary>
    /// Initializes the toolkit.
    /// </summary>
    public void Initialize()
    {
        _tools.Clear();
        RegisterTools();
    }

    /// <summary>
    /// Registers a tool in this toolkit.
    /// </summary>
    protected void RegisterTool(ITool tool)
    {
        _tools.Add(tool);
    }

    /// <summary>
    /// Registers a tool defined by a delegate.
    /// </summary>
    protected void RegisterTool<TArgs>(
        string name,
        string description,
        object inputSchema,
        Func<TArgs, ToolContext, CancellationToken, Task<ToolResult>> execute,
        ToolAttributes? attributes = null) where TArgs : class
    {
        var fullName = Prefix != null ? $"{Prefix}_{name}" : name;
        var tool = new DelegateTool<TArgs>(fullName, description, inputSchema, execute, attributes);
        _tools.Add(tool);
    }

    /// <summary>
    /// Registers all tools in this toolkit to a registry.
    /// </summary>
    public void RegisterTo(IToolRegistry registry)
    {
        Initialize();
        foreach (var tool in _tools)
        {
            registry.Register(tool);
        }
    }

    private sealed class DelegateTool<TArgs> : ToolBase<TArgs> where TArgs : class
    {
        private readonly Func<TArgs, ToolContext, CancellationToken, Task<ToolResult>> _execute;

        public override string Name { get; }
        public override string Description { get; }
        public override object InputSchema { get; }
        public override ToolAttributes Attributes { get; }

        public DelegateTool(
            string name,
            string description,
            object inputSchema,
            Func<TArgs, ToolContext, CancellationToken, Task<ToolResult>> execute,
            ToolAttributes? attributes = null)
        {
            Name = name;
            Description = description;
            InputSchema = inputSchema;
            Attributes = attributes ?? new ToolAttributes();
            _execute = execute;
        }

        protected override Task<ToolResult> ExecuteAsync(TArgs arguments, ToolContext context, CancellationToken cancellationToken)
        {
            return _execute(arguments, context, cancellationToken);
        }
    }
}
