namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 信息泄漏防护钩子
/// 检测并防止敏感信息泄漏
/// </summary>
public class LeakProtectionHook : IAssistantHook
{
    private readonly ILogger<LeakProtectionHook> _logger;
    private readonly List<string> _sensitivePatterns = new()
    {
        @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", // 邮箱
        @"\b\d{4}-\d{4}-\d{4}-\d{4}\b", // 信用卡号
        @"\b\d{3}-\d{2}-\d{4}\b", // SSN
        "password|secret|token|api[_-]?key", // 敏感关键词
    };

    public string Name => "LeakProtection";
    public int Order => 50;

    public LeakProtectionHook(ILogger<LeakProtectionHook> logger)
    {
        _logger = logger;
    }

    public Task<HookResult> OnBeforeRunAsync(HookContext context)
    {
        return Task.FromResult(new HookResult { ShouldProceed = true });
    }

    public Task OnAfterRunAsync(HookContext context)
    {
        // 检查响应中是否包含敏感信息
        // TODO: 实现 AI 响应检查逻辑
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检测文本中的敏感信息
    /// </summary>
    public List<string> DetectSensitiveInfo(string text)
    {
        var found = new List<string>();
        foreach (var pattern in _sensitivePatterns)
        {
            var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern,
                System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            foreach (System.Text.RegularExpressions.Match match in matches)
            {
                found.Add(match.Value);
            }
        }
        return found;
    }
}
