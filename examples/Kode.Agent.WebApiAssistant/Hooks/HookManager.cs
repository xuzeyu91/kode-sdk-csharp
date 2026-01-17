namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 钩子管理器
/// </summary>
public class HookManager
{
    private readonly List<IAssistantHook> _hooks;
    private readonly ILogger<HookManager> _logger;

    public HookManager(IEnumerable<IAssistantHook> hooks, ILogger<HookManager> logger)
    {
        _hooks = hooks.OrderBy(h => h.Order).ToList();
        _logger = logger;
    }

    /// <summary>
    /// 执行前置钩子
    /// </summary>
    public async Task<(bool ShouldProceed, string? Message)> ExecuteBeforeHooksAsync(HookContext context)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                var result = await hook.OnBeforeRunAsync(context);
                if (!result.ShouldProceed)
                {
                    _logger.LogWarning("Hook {HookName} blocked execution: {Message}",
                        hook.Name, result.Message);
                    return (false, result.Message);
                }

                // 应用配置修改
                if (result.ModifiedConfig != null)
                {
                    context.Config = result.ModifiedConfig;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hook {HookName} failed", hook.Name);
            }
        }

        return (true, null);
    }

    /// <summary>
    /// 执行后置钩子
    /// </summary>
    public async Task ExecuteAfterHooksAsync(HookContext context)
    {
        foreach (var hook in _hooks)
        {
            try
            {
                await hook.OnAfterRunAsync(context);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Hook {HookName} failed in after hook", hook.Name);
            }
        }
    }

    /// <summary>
    /// 注册钩子
    /// </summary>
    public void RegisterHook(IAssistantHook hook)
    {
        _hooks.Add(hook);
        _hooks.Sort((a, b) => a.Order.CompareTo(b.Order));
    }
}
