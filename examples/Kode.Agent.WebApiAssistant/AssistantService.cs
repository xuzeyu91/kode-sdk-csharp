using System.Net;
using System.Text.Json;
using Kode.Agent.Sdk.Core.Abstractions;
using Kode.Agent.Sdk.Core.Types;
using Kode.Agent.Tools.Builtin;
using Kode.Agent.WebApiAssistant.Extensions;
using Kode.Agent.WebApiAssistant.OpenAI;
using Kode.Agent.WebApiAssistant.Utils;
using Microsoft.Extensions.Configuration;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;

namespace Kode.Agent.WebApiAssistant;

public sealed class AssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AgentDependencies _deps;
    private readonly AssistantOptions _options;
    private readonly ILogger<AssistantService> _logger;
    private readonly Services.AgentToolsLoader _agentToolsLoader;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public AssistantService(
        AgentDependencies deps,
        AssistantOptions options,
        ILogger<AssistantService> logger,
        Services.AgentToolsLoader agentToolsLoader,
        IServiceProvider serviceProvider,
        IConfiguration configuration)
    {
        _deps = deps;
        _options = options;
        _logger = logger;
        _agentToolsLoader = agentToolsLoader;
        _serviceProvider = serviceProvider;
        _configuration = configuration;
    }

    public async Task HandleChatCompletionsAsync(HttpContext httpContext, OpenAiChatCompletionRequest request)
    {
        if (!Authorize(httpContext))
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.Unauthorized;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = "Unauthorized",
                    type = "authentication_error"
                }
            }, JsonOptions);
            return;
        }

        string? systemPrompt;
        string input;
        try
        {
            (systemPrompt, input) = ExtractPromptAndInput(request);
        }
        catch (BadHttpRequestException ex)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = ex.Message,
                    type = "invalid_request_error"
                }
            }, JsonOptions);
            return;
        }
        var agentId = ResolveAgentId(httpContext, request);
        var dataDir = Path.Combine(_options.WorkDir, "data", agentId);

        EnsureAgentDataDir(_options.StoreDir, dataDir, _configuration);

        // 构建 AllowPaths，包含 Skills 目录
        var allowPaths = new List<string>
        {
            _options.StoreDir,
            dataDir
        };

        // 添加 Skills 路径到 AllowPaths（如果有 Skills 配置）
        if (_options.SkillsConfig?.Paths != null)
        {
            foreach (var skillsPath in _options.SkillsConfig.Paths)
            {
                var resolvedPath = Path.IsPathRooted(skillsPath)
                    ? skillsPath
                    : Path.Combine(_options.WorkDir, skillsPath);
                allowPaths.Add(resolvedPath);
            }
        }

        var config = new AgentConfig
        {
            // Model = string.IsNullOrWhiteSpace(request.Model) ? _options.DefaultModel : request.Model,
            Model = _options.DefaultModel,
            SystemPrompt = systemPrompt ?? _options.DefaultSystemPrompt,
            // 使用 "*" 允许所有工具（包括动态注册的 MCP 工具）
            Tools = ["*"],
            Permissions = _options.PermissionConfig with
            {
                // AllowTools 优先使用配置中的工具白名单
                AllowTools = _options.PermissionConfig.AllowTools ?? _options.Tools
            },
            SandboxOptions = new SandboxOptions
            {
                WorkingDirectory = dataDir,
                EnforceBoundary = true,
                AllowPaths = allowPaths
            },
            Temperature = request.Temperature,
            MaxTokens = request.MaxTokens,
            MaxIterations = 50,
            // 传递 Skills 配置
            Skills = _options.SkillsConfig,
        };

        await using var agent = await CreateOrResumeAgentAsync(agentId, config, _deps, httpContext.RequestAborted);

        if (request.Stream)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
            httpContext.Response.ContentType = "text/event-stream";
            httpContext.Response.Headers["Cache-Control"] = "no-cache";
            httpContext.Response.Headers["X-Accel-Buffering"] = "no";

            await StreamAsOpenAiSseAsync(httpContext, agent, input, config.Model!, agentId);
            return;
        }

        var result = await agent.RunAsync(input, httpContext.RequestAborted);

        var response = new OpenAiChatCompletionResponse
        {
            Id = "chatcmpl-" + Guid.NewGuid().ToString("N"),
            Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
            Model = config.Model!,
            Choices =
            [
                new OpenAiChatCompletionChoice
                {
                    Index = 0,
                    FinishReason = MapFinishReason(result.StopReason),
                    Message = new OpenAiChatCompletionMessage
                    {
                        Role = "assistant",
                        Content = result.Response ?? ""
                    }
                }
            ],
            Usage = new OpenAiUsage
            {
                PromptTokens = 0,
                CompletionTokens = 0,
                TotalTokens = 0
            }
        };

        httpContext.Response.Headers["X-Kode-Agent-Id"] = agentId;
        await httpContext.Response.WriteAsJsonAsync(response, JsonOptions);
    }

    private bool Authorize(HttpContext httpContext)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey)) return true;

        if (!httpContext.Request.Headers.TryGetValue("Authorization", out var auth))
        {
            return false;
        }

        var value = auth.ToString();
        if (!value.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)) return false;
        var token = value["Bearer ".Length..].Trim();

        return string.Equals(token, _options.ApiKey, StringComparison.Ordinal);
    }

    private static (string? SystemPrompt, string Input) ExtractPromptAndInput(OpenAiChatCompletionRequest request)
    {
        if (request.Messages.Count == 0)
        {
            throw new BadHttpRequestException("messages is required");
        }

        var systemPrompts = request.Messages
            .Where(m => string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase))
            .Select(m => m.GetTextContent())
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .ToList();

        var systemPrompt = systemPrompts.Count > 0 ? string.Join("\n", systemPrompts) : null;

        var lastNonSystem = request.Messages
            .LastOrDefault(m => !string.Equals(m.Role, "system", StringComparison.OrdinalIgnoreCase));
        if (lastNonSystem == null)
        {
            throw new BadHttpRequestException("At least one non-system message is required");
        }

        if (!string.Equals(lastNonSystem.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException("The last non-system message must be a user message");
        }

        var input = lastNonSystem.GetTextContent();
        return (systemPrompt, input);
    }

    private static string ResolveAgentId(HttpContext httpContext, OpenAiChatCompletionRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.User))
        {
            return request.User.Trim();
        }

        if (httpContext.Request.Headers.TryGetValue("X-Kode-Agent-Id", out var headerValue))
        {
            var id = headerValue.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(id)) return id;
        }

        return Guid.NewGuid().ToString("N");
    }

    private async Task<AgentImpl> CreateOrResumeAgentAsync(
        string agentId,
        AgentConfig config,
        AgentDependencies deps,
        CancellationToken cancellationToken)
    {
        // Create per-agent tool registry with global + agent-specific tools
        var agentDeps = await CreateAgentDependenciesAsync(agentId, deps);

        AgentImpl agent;

        if (await deps.Store.ExistsAsync(agentId, cancellationToken))
        {
            var overrides = new AgentConfigOverrides
            {
                // TS-aligned: resumed agents restore tool instances from persisted descriptors.
                // Only override environment/request-scoped fields here (model/system prompt/sandbox/model params).
                Model = config.Model,
                SystemPrompt = config.SystemPrompt,
                SandboxOptions = config.SandboxOptions,
                MaxTokens = config.MaxTokens,
                Temperature = config.Temperature,
                EnableThinking = config.EnableThinking,
                ThinkingBudget = config.ThinkingBudget,
                ExposeThinking = config.ExposeThinking,
            };

            agent = await AgentImpl.ResumeFromStoreAsync(agentId, agentDeps, overrides: overrides, cancellationToken: cancellationToken);
        }
        else
        {
            agent = await AgentImpl.CreateAsync(agentId, config, agentDeps, cancellationToken);

            // 新创建的 Agent 自动激活技能
            await SkillsAutoActivator.ActivateSkillsAsync(agent, _logger, cancellationToken);
        }

        return agent;
    }

    /// <summary>
    /// Create agent-specific dependencies with per-agent tool registry
    /// </summary>
    private async Task<AgentDependencies> CreateAgentDependenciesAsync(string agentId, AgentDependencies globalDeps)
    {
        // Create a new tool registry for this agent
        var agentRegistry = new Kode.Agent.Sdk.Tools.ToolRegistry();

        // Register built-in tools
        agentRegistry.RegisterBuiltinTools();

        // Register platform tools (time, calendar)
        agentRegistry.RegisterPlatformTools(_serviceProvider);

        // Load agent-specific tools from .config directory (email, notify)
        await _agentToolsLoader.LoadAgentToolsAsync(agentId, _options.WorkDir, agentRegistry);

        // Create new dependencies with the agent-specific tool registry
        return new AgentDependencies
        {
            Store = globalDeps.Store,
            ToolRegistry = agentRegistry,
            SandboxFactory = globalDeps.SandboxFactory,
            ModelProvider = globalDeps.ModelProvider,
            LoggerFactory = globalDeps.LoggerFactory
        };
    }

    private async Task StreamAsOpenAiSseAsync(
        HttpContext httpContext,
        AgentImpl agent,
        string input,
        string model,
        string agentId)
    {
        var streamId = "chatcmpl-" + Guid.NewGuid().ToString("N");
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        httpContext.Response.Headers["X-Kode-Agent-Id"] = agentId;

        await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
        {
            Id = streamId,
            Created = created,
            Model = model,
            Choices =
            [
                new OpenAiChatCompletionChunkChoice
                {
                    Index = 0,
                    Delta = new OpenAiChatCompletionDelta { Role = "assistant" }
                }
            ]
        });

        try
        {
            await foreach (var envelope in agent.ChatStreamAsync(input, opts: null, cancellationToken: httpContext.RequestAborted))
            {
                switch (envelope.Event)
                {
                    case TextChunkEvent textChunk when !string.IsNullOrEmpty(textChunk.Delta):
                        await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
                        {
                            Id = streamId,
                            Created = created,
                            Model = model,
                            Choices =
                            [
                                new OpenAiChatCompletionChunkChoice
                                {
                                    Index = 0,
                                    Delta = new OpenAiChatCompletionDelta { Content = textChunk.Delta }
                                }
                            ]
                        });
                        break;

                    case DoneEvent done:
                        await WriteSseAsync(httpContext, new OpenAiChatCompletionChunk
                        {
                            Id = streamId,
                            Created = created,
                            Model = model,
                            Choices =
                            [
                                new OpenAiChatCompletionChunkChoice
                                {
                                    Index = 0,
                                    Delta = new OpenAiChatCompletionDelta(),
                                    FinishReason = "stop"
                                }
                            ]
                        });

                        await httpContext.Response.WriteAsync("data: [DONE]\n\n");
                        await httpContext.Response.Body.FlushAsync();
                        return;
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Client disconnected; align with TS assistant behavior: stop streaming without interrupting the agent.
            return;
        }
        catch (IOException)
        {
            // Connection aborted mid-write; ignore.
            return;
        }
        catch (ObjectDisposedException)
        {
            return;
        }
    }

    private static async Task WriteSseAsync(HttpContext httpContext, object payload)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        await httpContext.Response.WriteAsync($"data: {json}\n\n");
        await httpContext.Response.Body.FlushAsync();
    }

    private static string MapFinishReason(StopReason stopReason)
    {
        return stopReason switch
        {
            StopReason.EndTurn => "stop",
            StopReason.MaxIterations => "length",
            StopReason.Cancelled => "stop",
            StopReason.AwaitingApproval => "stop",
            StopReason.Error => "stop",
            _ => "stop"
        };
    }

    private static void EnsureAgentDataDir(string storeDir, string dataDir, IConfiguration configuration)
    {
        Directory.CreateDirectory(storeDir);
        Directory.CreateDirectory(dataDir);

        var memoryDir = Path.Combine(dataDir, ".memory");
        var knowledgeDir = Path.Combine(dataDir, ".knowledge");
        var configDir = Path.Combine(dataDir, ".config");
        var tasksDir = Path.Combine(dataDir, ".tasks");

        foreach (var dir in new[] { memoryDir, knowledgeDir, configDir, tasksDir })
        {
            Directory.CreateDirectory(dir);
        }

        InitDefaultConfigs(configDir, configuration);
        InitDefaultMemory(memoryDir);
    }

    private static void InitDefaultConfigs(string configDir, IConfiguration configuration)
    {
        var notifyConfigPath = Path.Combine(configDir, "notify.json");
        if (!File.Exists(notifyConfigPath))
        {
            var notifySection = configuration.GetSection("Notify");
            var defaultNotifyConfig = new
            {
                @default = notifySection["DefaultChannel"] ?? "dingtalk",
                channels = new
                {
                    dingtalk = new
                    {
                        webhook = notifySection.GetSection("DingTalk")["WebhookUrl"] ?? "",
                        secret = notifySection.GetSection("DingTalk")["Secret"] ?? ""
                    },
                    wecom = new
                    {
                        webhook = notifySection.GetSection("WeCom")["WebhookUrl"] ?? ""
                    },
                    telegram = new
                    {
                        botToken = notifySection.GetSection("Telegram")["BotToken"] ?? "",
                        chatId = notifySection.GetSection("Telegram")["ChatId"] ?? ""
                    }
                }
            };
            WriteIndentedJson(notifyConfigPath, defaultNotifyConfig);
        }

        var emailConfigPath = Path.Combine(configDir, "email.json");
        if (!File.Exists(emailConfigPath))
        {
            var emailSection = configuration.GetSection("Email");
            var defaultEmailConfig = new
            {
                imap = new
                {
                    host = emailSection.GetSection("Imap")["Host"] ?? "imap.gmail.com",
                    port = int.TryParse(emailSection.GetSection("Imap")["Port"], out var imapPort) ? imapPort : 993,
                    secure = bool.TryParse(emailSection.GetSection("Imap")["UseSsl"], out var imapSsl) && imapSsl,
                    auth = new
                    {
                        user = emailSection.GetSection("Imap")["Username"] ?? "",
                        pass = emailSection.GetSection("Imap")["Password"] ?? ""
                    }
                },
                smtp = new
                {
                    host = emailSection.GetSection("Smtp")["Host"] ?? "smtp.gmail.com",
                    port = int.TryParse(emailSection.GetSection("Smtp")["Port"], out var smtpPort) ? smtpPort : 587,
                    secure = bool.TryParse(emailSection.GetSection("Smtp")["UseSsl"], out var smtpSsl) && smtpSsl,
                    auth = new
                    {
                        user = emailSection.GetSection("Smtp")["Username"] ?? "",
                        pass = emailSection.GetSection("Smtp")["Password"] ?? ""
                    }
                }
            };
            WriteIndentedJson(emailConfigPath, defaultEmailConfig);
        }
    }

    private static void InitDefaultMemory(string memoryDir)
    {
        var profilePath = Path.Combine(memoryDir, "profile.json");
        if (!File.Exists(profilePath))
        {
            var now = DateTimeOffset.UtcNow.ToString("O");
            var defaultProfile = new
            {
                assistantName = "Koda",
                userName = "",
                timezone = "Asia/Shanghai",
                language = "zh-CN",
                preferences = new { },
                createdAt = now,
                updatedAt = now
            };
            WriteIndentedJson(profilePath, defaultProfile);
        }

        Directory.CreateDirectory(Path.Combine(memoryDir, "facts"));
    }

    private static void WriteIndentedJson<T>(string path, T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        });
        File.WriteAllText(path, json);
    }
}
