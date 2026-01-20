using System.Net;
using System.Text.Json;
using AgentImpl = Kode.Agent.Sdk.Core.Agent.Agent;
using Kode.Agent.Sdk.Core.Abstractions;
using AgentDependencies = Kode.Agent.Sdk.Core.Types.AgentDependencies;
using Kode.Agent.WebApiAssistant.Assistant;
using Kode.Agent.WebApiAssistant.OpenAI;

namespace Kode.Agent.WebApiAssistant;

/// <summary>
/// Options for getting an owned agent.
/// </summary>
public sealed record GetOwnedAgentOptions
{
    /// <summary>Whether this appears to be a new conversation</summary>
    public bool IsNewConversation { get; init; }
    /// <summary>The thread key (from OpenAI user field)</summary>
    public string? ThreadKey { get; init; }
    /// <summary>The auto request mode classification</summary>
    public AutoRequestMode AutoRequestMode { get; init; } = AutoRequestMode.Unknown;
    /// <summary>System prompt override</summary>
    public string? SystemPrompt { get; init; }
    /// <summary>Temperature override</summary>
    public double? Temperature { get; init; }
    /// <summary>Max tokens override</summary>
    public int? MaxTokens { get; init; }
}

/// <summary>
/// Result of GetOwnedAgentAsync operation.
/// </summary>
public sealed record GetOwnedAgentResult
{
    /// <summary>The agent instance (null if error occurred)</summary>
    public AgentImpl? Agent { get; init; }
    /// <summary>The agent ID</summary>
    public string? AgentId { get; init; }
    /// <summary>Whether the response has already been written (error case)</summary>
    public bool ResponseWritten { get; init; }
}

public sealed class AssistantService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly AgentDependencies _globalDeps;
    private readonly AssistantOptions _options;
    private readonly IServiceProvider _serviceProvider;
    private readonly OpenAiRoutingStateManager _routingStateManager;

    public AssistantService(
        AgentDependencies globalDeps,
        AssistantOptions options,
        IServiceProvider serviceProvider)
    {
        _globalDeps = globalDeps;
        _options = options;
        _serviceProvider = serviceProvider;
        _routingStateManager = new OpenAiRoutingStateManager(
            options.WorkDir,
            serviceProvider.GetService<ILogger<OpenAiRoutingStateManager>>());
    }

    /// <summary>
    /// Get or create an agent based on session routing rules.
    /// This implements the same logic as the TypeScript getOwnedAgent function.
    /// </summary>
    private async Task<GetOwnedAgentResult> GetOwnedAgentAsync(
        HttpContext httpContext,
        OpenAiChatCompletionRequest request,
        GetOwnedAgentOptions opts,
        CancellationToken cancellationToken)
    {
        // For now, we don't have user authentication, so userId is null (global routing)
        string? userId = null;

        // Get session ID from path and headers
        var agentIdFromPath = SessionRouting.NormalizeSessionId(
            httpContext.Request.RouteValues.TryGetValue("sessionId", out var pathVal) ? pathVal?.ToString() : null);

        var agentIdFromHeader = SessionRouting.NormalizeSessionId(
            httpContext.Request.Headers.TryGetValue("X-Session-Id", out var sessionHeader) ? sessionHeader.ToString() : null);

        // Check for conflict between path and header
        if (agentIdFromPath != null && agentIdFromHeader != null && agentIdFromPath != agentIdFromHeader)
        {
            httpContext.Response.StatusCode = (int)HttpStatusCode.BadRequest;
            await httpContext.Response.WriteAsJsonAsync(new
            {
                error = new
                {
                    message = "sessionId in path must match X-Session-Id header when both are provided",
                    type = "invalid_request_error",
                    param = "sessionId",
                    code = (string?)null
                }
            }, JsonOptions, cancellationToken);
            return new GetOwnedAgentResult { ResponseWritten = true };
        }

        var explicitAgentId = agentIdFromPath ?? agentIdFromHeader;

        // Case 1: Explicit session ID provided
        if (explicitAgentId != null)
        {
            // Check if session exists in store
            var sessionExists = await _globalDeps.Store.ExistsAsync(explicitAgentId, cancellationToken);
            if (!sessionExists)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.NotFound;
                await httpContext.Response.WriteAsJsonAsync(new
                {
                    error = new
                    {
                        message = "Session not found",
                        type = "not_found_error"
                    }
                }, JsonOptions, cancellationToken);
                return new GetOwnedAgentResult { ResponseWritten = true };
            }

            var agent = await CreateOrResumeAgentAsync(explicitAgentId, opts, cancellationToken);
            SetSessionIdHeaders(httpContext, explicitAgentId);
            return new GetOwnedAgentResult { Agent = agent, AgentId = explicitAgentId };
        }

        // Case 2: Thread key provided (OpenAI user field)
        var threadKey = SessionRouting.NormalizeThreadKey(opts.ThreadKey);
        if (threadKey != null)
        {
            var state = _routingStateManager.LoadRoutingState(userId);
            var mappedId = state.ThreadKeyToAgentId?.GetValueOrDefault(threadKey);

            if (mappedId != null && await _globalDeps.Store.ExistsAsync(mappedId, cancellationToken))
            {
                var agent = await CreateOrResumeAgentAsync(mappedId, opts, cancellationToken);
                SetSessionIdHeaders(httpContext, mappedId);
                return new GetOwnedAgentResult { Agent = agent, AgentId = mappedId };
            }

            // First time seeing this threadKey: create new session and bind it
            var newAgentId = AssistantBuilder.GenerateAgentId();
            var newAgent = await CreateOrResumeAgentAsync(newAgentId, opts, cancellationToken);

            var nextState = state with
            {
                ThreadKeyToAgentId = new Dictionary<string, string>(state.ThreadKeyToAgentId ?? new())
                {
                    [threadKey] = newAgentId
                }
            };
            _routingStateManager.SaveRoutingState(userId, nextState);
            SetSessionIdHeaders(httpContext, newAgentId);
            return new GetOwnedAgentResult { Agent = newAgent, AgentId = newAgentId };
        }

        // Case 3: Auto default session (no explicit session, no threadKey)
        {
            var state = _routingStateManager.LoadRoutingState(userId);
            var autoDefault = state.AutoDefaultAgentId;
            var currentMode = opts.AutoRequestMode;

            // Conservative strategy: only create new session in auto mode when:
            // - Last request was "history" mode AND current request looks like new conversation
            // This handles both:
            // - Clients that always send single user message: reuse same session
            // - Clients that send history: switching from history to single-user indicates "new conversation"
            var lastMode = SessionRouting.ParseMode(state.AutoLastRequestMode);
            var shouldCreateNewAutoSession = opts.IsNewConversation && lastMode == AutoRequestMode.History;

            if (!shouldCreateNewAutoSession && autoDefault != null && await _globalDeps.Store.ExistsAsync(autoDefault, cancellationToken))
            {
                var agent = await CreateOrResumeAgentAsync(autoDefault, opts, cancellationToken);
                _routingStateManager.SaveRoutingState(userId, state with
                {
                    AutoDefaultAgentId = autoDefault,
                    AutoLastRequestMode = SessionRouting.ModeToString(currentMode),
                    AutoLastRequestAt = DateTimeOffset.UtcNow.ToString("O")
                });
                SetSessionIdHeaders(httpContext, autoDefault);
                return new GetOwnedAgentResult { Agent = agent, AgentId = autoDefault };
            }

            // Create new auto-default session
            var newAgentId = AssistantBuilder.GenerateAgentId();
            var newAgent = await CreateOrResumeAgentAsync(newAgentId, opts, cancellationToken);
            _routingStateManager.SaveRoutingState(userId, state with
            {
                AutoDefaultAgentId = newAgentId,
                AutoLastRequestMode = SessionRouting.ModeToString(currentMode),
                AutoLastRequestAt = DateTimeOffset.UtcNow.ToString("O")
            });
            SetSessionIdHeaders(httpContext, newAgentId);
            return new GetOwnedAgentResult { Agent = newAgent, AgentId = newAgentId };
        }
    }

    /// <summary>
    /// Create or resume an agent with the given ID.
    /// </summary>
    private async Task<AgentImpl> CreateOrResumeAgentAsync(
        string agentId,
        GetOwnedAgentOptions opts,
        CancellationToken cancellationToken)
    {
        var createOptions = new CreateAssistantOptions
        {
            AgentId = agentId,
            WorkDir = _options.WorkDir,
            Model = _options.DefaultModel,
            SystemPrompt = opts.SystemPrompt ?? _options.DefaultSystemPrompt,
            Temperature = opts.Temperature,
            MaxTokens = opts.MaxTokens,
            Skills = _options.SkillsConfig,
            Permissions = _options.PermissionConfig
        };

        return await AssistantBuilder.CreateAssistantAsync(
            createOptions,
            _globalDeps,
            _serviceProvider,
            _globalDeps.LoggerFactory!,
            cancellationToken);
    }

    /// <summary>
    /// Set session ID response headers.
    /// </summary>
    private static void SetSessionIdHeaders(HttpContext httpContext, string agentId)
    {
        httpContext.Response.Headers["X-Session-Id"] = agentId;
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
                    type = "invalid_request_error",
                    param = "messages",
                    code = (string?)null
                }
            }, JsonOptions);
            return;
        }

        // Classify the request to determine session routing behavior
        var classification = SessionRouting.ClassifyAutoRequestFromMessages(request.Messages);

        // Get or create agent based on session routing rules
        var agentResult = await GetOwnedAgentAsync(
            httpContext,
            request,
            new GetOwnedAgentOptions
            {
                IsNewConversation = classification.IsNewConversation,
                AutoRequestMode = classification.Mode,
                ThreadKey = request.User,
                SystemPrompt = systemPrompt,
                Temperature = request.Temperature,
                MaxTokens = request.MaxTokens
            },
            httpContext.RequestAborted);

        // If response was already written (error case), return early
        if (agentResult.ResponseWritten)
        {
            return;
        }

        var agent = agentResult.Agent!;
        var agentId = agentResult.AgentId!;

        await using (agent)
        {
            if (request.Stream)
            {
                httpContext.Response.StatusCode = (int)HttpStatusCode.OK;
                httpContext.Response.ContentType = "text/event-stream";
                httpContext.Response.Headers["Cache-Control"] = "no-cache";
                httpContext.Response.Headers["X-Accel-Buffering"] = "no";

                await StreamAsOpenAiSseAsync(httpContext, agent, input, _options.DefaultModel!, agentId);
                return;
            }

            var result = await agent.RunAsync(input, httpContext.RequestAborted);

            var response = new OpenAiChatCompletionResponse
            {
                Id = "chatcmpl-" + Guid.NewGuid().ToString("N"),
                Created = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Model = _options.DefaultModel!,
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

            await httpContext.Response.WriteAsJsonAsync(response, JsonOptions);
        }
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
            throw new BadHttpRequestException("messages is required and must be a non-empty array");
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
            throw new BadHttpRequestException("At least one user message is required");
        }

        if (!string.Equals(lastNonSystem.Role, "user", StringComparison.OrdinalIgnoreCase))
        {
            throw new BadHttpRequestException("At least one user message is required");
        }

        var input = lastNonSystem.GetTextContent();
        return (systemPrompt, input);
    }

    private static async Task StreamAsOpenAiSseAsync(
        HttpContext httpContext,
        AgentImpl agent,
        string input,
        string model,
        string agentId)
    {
        var streamId = "chatcmpl-" + Guid.NewGuid().ToString("N");
        var created = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Note: Session ID headers are already set by SetSessionIdHeaders in GetOwnedAgentAsync

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
        }
        catch (IOException)
        {
            // Connection aborted mid-write; ignore.
        }
        catch (ObjectDisposedException)
        {
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
}
