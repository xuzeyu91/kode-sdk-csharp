namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 网络浏览策略钩子
/// 控制网络访问权限，防止恶意浏览
/// </summary>
public class BrowsePolicyHook : IAssistantHook
{
    private readonly ILogger<BrowsePolicyHook> _logger;
    private readonly HashSet<string> _allowedDomains = new();
    private readonly HashSet<string> _blockedDomains = new();

    public string Name => "BrowsePolicy";
    public int Order => 100;

    public BrowsePolicyHook(ILogger<BrowsePolicyHook> logger)
    {
        _logger = logger;
    }

    public Task<HookResult> OnBeforeRunAsync(HookContext context)
    {
        // 检查用户输入中是否包含 URL
        var urls = ExtractUrls(context.UserInput);
        if (urls.Count > 0)
        {
            foreach (var url in urls)
            {
                var domain = ExtractDomain(url);
                if (IsBlocked(domain))
                {
                    _logger.LogWarning("Blocked URL: {Url} (domain: {Domain})", url, domain);
                    return Task.FromResult(new HookResult
                    {
                        ShouldProceed = false,
                        Message = $"访问 {domain} 被策略阻止"
                    });
                }
            }
        }

        return Task.FromResult(new HookResult { ShouldProceed = true });
    }

    public Task OnAfterRunAsync(HookContext context)
    {
        return Task.CompletedTask;
    }

    private static List<string> ExtractUrls(string text)
    {
        var urls = new List<string>();
        var pattern = @"https?://[^\s]+";
        var matches = System.Text.RegularExpressions.Regex.Matches(text, pattern);
        foreach (System.Text.RegularExpressions.Match match in matches)
        {
            urls.Add(match.Value);
        }
        return urls;
    }

    private static string? ExtractDomain(string url)
    {
        try
        {
            var uri = new Uri(url);
            return uri.Host;
        }
        catch
        {
            return null;
        }
    }

    private bool IsBlocked(string? domain)
    {
        if (string.IsNullOrEmpty(domain)) return false;
        return _blockedDomains.Contains(domain.ToLower());
    }
}
