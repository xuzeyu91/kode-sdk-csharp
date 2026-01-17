namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 网络请求策略钩子
/// 控制外部网络访问
/// </summary>
public class NetworkPolicyHook : IAssistantHook
{
    private readonly ILogger<NetworkPolicyHook> _logger;
    private readonly HashSet<string> _allowedHosts = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _blockedHosts = new(StringComparer.OrdinalIgnoreCase);

    public string Name => "NetworkPolicy";
    public int Order => 150;

    public NetworkPolicyHook(ILogger<NetworkPolicyHook> logger)
    {
        _logger = logger;
        // 默认允许常见的安全域名
        _allowedHosts.Add("api.openai.com");
        _allowedHosts.Add("api.anthropic.com");
    }

    public Task<HookResult> OnBeforeRunAsync(HookContext context)
    {
        // TODO: 实现网络请求拦截逻辑
        return Task.FromResult(new HookResult { ShouldProceed = true });
    }

    public Task OnAfterRunAsync(HookContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检查主机是否允许访问
    /// </summary>
    public bool IsHostAllowed(string host)
    {
        if (_blockedHosts.Contains(host))
        {
            return false;
        }

        return _allowedHosts.Count == 0 || _allowedHosts.Contains(host);
    }
}
