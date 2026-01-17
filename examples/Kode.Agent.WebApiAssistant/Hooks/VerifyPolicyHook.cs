namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 事实验证策略钩子
/// 对重要声明进行验证
/// </summary>
public class VerifyPolicyHook : IAssistantHook
{
    private readonly ILogger<VerifyPolicyHook> _logger;

    public string Name => "VerifyPolicy";
    public int Order => 300;

    public VerifyPolicyHook(ILogger<VerifyPolicyHook> logger)
    {
        _logger = logger;
    }

    public Task<HookResult> OnBeforeRunAsync(HookContext context)
    {
        // 检测是否需要验证
        var needsVerification = DetectVerificationNeed(context.UserInput);

        if (needsVerification)
        {
            _logger.LogInformation("Verification suggested for: {Input}", context.UserInput);
            // TODO: 实现验证逻辑
        }

        return Task.FromResult(new HookResult { ShouldProceed = true });
    }

    public Task OnAfterRunAsync(HookContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检测是否需要验证
    /// </summary>
    private static bool DetectVerificationNeed(string input)
    {
        var verifyKeywords = new[]
        {
            "确认", "验证", "是否正确", "真的吗", "确定",
            "confirm", "verify", "is that correct", "really", "are you sure"
        };

        var lowerInput = input.ToLower();
        return verifyKeywords.Any(keyword => lowerInput.Contains(keyword));
    }
}
