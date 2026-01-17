namespace Kode.Agent.WebApiAssistant.Hooks;

/// <summary>
/// 记忆回忆策略钩子
/// 在适当的时候触发记忆检索
/// </summary>
public class MemoryRecallHook : IAssistantHook
{
    private readonly ILogger<MemoryRecallHook> _logger;

    public string Name => "MemoryRecall";
    public int Order => 200;

    public MemoryRecallHook(ILogger<MemoryRecallHook> logger)
    {
        _logger = logger;
    }

    public Task<HookResult> OnBeforeRunAsync(HookContext context)
    {
        // 检测用户输入是否涉及回忆需求
        var needsRecall = DetectRecallIntent(context.UserInput);

        if (needsRecall)
        {
            _logger.LogInformation("Memory recall triggered for input: {Input}", context.UserInput);
            // TODO: 触发记忆检索
        }

        return Task.FromResult(new HookResult { ShouldProceed = true });
    }

    public Task OnAfterRunAsync(HookContext context)
    {
        return Task.CompletedTask;
    }

    /// <summary>
    /// 检测是否需要回忆记忆
    /// </summary>
    private static bool DetectRecallIntent(string input)
    {
        var recallKeywords = new[]
        {
            "记得", "回忆", "之前", "上次", "什么时候", "谁", "哪里",
            "remember", "recall", "previous", "last time", "when", "who", "where"
        };

        var lowerInput = input.ToLower();
        return recallKeywords.Any(keyword => lowerInput.Contains(keyword));
    }
}
