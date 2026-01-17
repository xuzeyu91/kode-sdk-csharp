using Kode.Agent.Sdk.Core.Types;

namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 钩子执行上下文
/// </summary>
public class HookContext
{
    public required string AgentId { get; set; }
    public required string UserInput { get; set; }
    public AgentConfig? Config { get; set; }
    public Dictionary<string, object> Properties { get; set; } = new();
}

/// <summary>
/// 钩子执行结果
/// </summary>
public class HookResult
{
    public bool ShouldProceed { get; set; } = true;
    public string? Message { get; set; }
    public AgentConfig? ModifiedConfig { get; set; }
}

/// <summary>
/// 助手钩子接口
/// </summary>
public interface IAssistantHook
{
    /// <summary>
    /// 钩子名称
    /// </summary>
    string Name { get; }

    /// <summary>
    /// 执行顺序（数字越小越先执行）
    /// </summary>
    int Order { get; }

    /// <summary>
    /// 在 Agent 运行前执行
    /// </summary>
    Task<HookResult> OnBeforeRunAsync(HookContext context);

    /// <summary>
    /// 在 Agent 运行后执行
    /// </summary>
    Task OnAfterRunAsync(HookContext context);
}
