namespace Kode.Agent.Sdk.Tools;

/// <summary>
/// Attribute to mark a class as a tool.
/// </summary>
[AttributeUsage(AttributeTargets.Class, Inherited = false, AllowMultiple = false)]
public sealed class ToolAttribute : Attribute
{
    /// <summary>
    /// The unique name of the tool.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// The description of the tool.
    /// </summary>
    public string Description { get; set; } = "";

    public ToolAttribute(string name)
    {
        Name = name;
    }
}

/// <summary>
/// Attribute to mark a property as a tool parameter.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Parameter, Inherited = false, AllowMultiple = false)]
public sealed class ToolParameterAttribute : Attribute
{
    /// <summary>
    /// The description of the parameter.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether the parameter is required.
    /// </summary>
    public bool Required { get; set; } = true;

    /// <summary>
    /// The default value for the parameter.
    /// </summary>
    public object? DefaultValue { get; set; }
}

/// <summary>
/// Attribute to mark a method as a tool method (for ToolKit classes).
/// </summary>
[AttributeUsage(AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ToolMethodAttribute : Attribute
{
    /// <summary>
    /// The name of the tool (defaults to method name).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// The description of the tool.
    /// </summary>
    public string Description { get; set; } = "";

    /// <summary>
    /// Whether the tool is read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Whether the tool requires approval.
    /// </summary>
    public bool RequiresApproval { get; set; }
}

/// <summary>
/// Attribute to specify tool attributes.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, Inherited = false, AllowMultiple = false)]
public sealed class ToolAttributesAttribute : Attribute
{
    /// <summary>
    /// Whether the tool is read-only.
    /// </summary>
    public bool ReadOnly { get; set; }

    /// <summary>
    /// Whether the tool has no side effects.
    /// </summary>
    public bool NoEffect { get; set; }

    /// <summary>
    /// Whether the tool requires approval.
    /// </summary>
    public bool RequiresApproval { get; set; }

    /// <summary>
    /// Whether the tool can run in parallel.
    /// </summary>
    public bool AllowParallel { get; set; } = true;

    /// <summary>
    /// Custom permission category.
    /// </summary>
    public string? PermissionCategory { get; set; }
}
