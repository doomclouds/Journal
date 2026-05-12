using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Journal.Domain.Entries;
using Journal.Infrastructure.Harness;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Journal.Infrastructure.Ai;

public sealed class OpenAiCompatibleAgentRuntime : IJournalAiAgentRuntime
{
    private static readonly JsonSerializerOptions ToolSerializerOptions = JsonSerializerOptions.Web;

    public async Task<OpenAiCompatibleRunResult> RunJsonAsync(
        OpenAiCompatibleRunRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.TimeoutSeconds > 0)
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var chatClient = new ChatClient(
                request.Model,
                new ApiKeyCredential(request.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(request.BaseUrl) });
            var agent = chatClient.AsAIAgent(
                instructions: request.SystemPrompt,
                name: "JournalJsonFormatter",
                description: "Formats morning journal raw inputs into JournalAiJson.");
            var options = new ChatClientAgentRunOptions(CreateJsonChatOptions(request));

            var response = await agent.RunAsync(
                request.UserPrompt,
                session: null,
                options: options,
                cancellationToken: timeout.Token);

            stopwatch.Stop();
            var safeSnippet = JournalAiSafeError.Redact(response.Text, [request.ApiKey]);

            using var document = JsonDocument.Parse(response.Text);
            if (document.RootElement.ValueKind != JsonValueKind.Object)
            {
                return InvalidJsonFailure(stopwatch.Elapsed, safeSnippet, request.ApiKey);
            }

            if (!request.RequiresJournalAiJson)
            {
                return OpenAiCompatibleRunResult.Success(null, safeSnippet, stopwatch.Elapsed, 200);
            }

            try
            {
                var aiJson = JsonSerializer.Deserialize<JournalAiJson>(response.Text, JsonSerializerOptions.Web);
                return aiJson is not null
                    ? OpenAiCompatibleRunResult.Success(aiJson, safeSnippet, stopwatch.Elapsed, 200)
                    : InvalidJsonFailure(stopwatch.Elapsed, safeSnippet, request.ApiKey);
            }
            catch (JsonException)
            {
                return InvalidJsonFailure(stopwatch.Elapsed, safeSnippet, request.ApiKey);
            }
        }
        catch (ClientResultException exception)
        {
            stopwatch.Stop();
            var code = MapStatusToCode(exception.Status);
            return OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    code,
                    "LLM request failed.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed,
                exception.Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    "timeout",
                    "LLM request timed out.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed,
                408);
        }
        catch (JsonException exception)
        {
            stopwatch.Stop();
            return OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    "invalid_json",
                    "LLM returned invalid JSON.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return OpenAiCompatibleRunResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    "provider_error",
                    "LLM request failed.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed);
        }
    }

    public async Task<JournalHarnessPlannerRuntimeResult> RunHarnessPlannerAsync(
        JournalHarnessPlannerRuntimeRequest request,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (request.TimeoutSeconds > 0)
        {
            timeout.CancelAfter(TimeSpan.FromSeconds(request.TimeoutSeconds));
        }

        var stopwatch = Stopwatch.StartNew();

        try
        {
            var collector = new JournalHarnessToolCollector();
            var chatClient = new ChatClient(
                request.Model,
                new ApiKeyCredential(request.ApiKey),
                new OpenAIClientOptions { Endpoint = new Uri(request.BaseUrl) });
            var agent = chatClient.AsAIAgent(
                instructions: request.SystemInstructions,
                name: "JournalHarnessPlanner",
                description: "Plans side-effect-free journal harness operations by calling collector tools.");
            var tools = new AITool[]
            {
                AIFunctionFactory.Create(
                    collector.AppendJournalSection,
                    "appendJournalSection",
                    "Record a side-effect-free append operation for an editable journal section.",
                    ToolSerializerOptions),
                AIFunctionFactory.Create(
                    collector.UpsertJournalSection,
                    "upsertJournalSection",
                    "Record a side-effect-free upsert operation for an editable journal section.",
                    ToolSerializerOptions),
                AIFunctionFactory.Create(
                    collector.ReviseAiGeneratedSection,
                    "reviseAiGeneratedSection",
                    "Record a side-effect-free revision operation for a pure AI-generated section.",
                    ToolSerializerOptions),
                AIFunctionFactory.Create(
                    collector.NoOp,
                    "noOp",
                    "Record that no safe journal operation should be applied.",
                    ToolSerializerOptions)
            };
            var options = new ChatClientAgentRunOptions(CreateHarnessPlannerChatOptions(request, tools));

            var response = await agent.RunAsync(
                BuildHarnessPlannerMessage(request.ProtectedContext, request.UserMessage),
                session: null,
                options: options,
                cancellationToken: timeout.Token);

            stopwatch.Stop();
            var safeSnippet = JournalAiSafeError.Redact(response.Text, [request.ApiKey]);
            if (collector.Operations.Count == 0)
            {
                return JournalHarnessPlannerRuntimeResult.Failure(
                    JournalAiSafeError.Create(
                        "runtime",
                        "no_tool_calls",
                        "LLM did not call a harness tool.",
                        safeSnippet,
                        [request.ApiKey]),
                    stopwatch.Elapsed,
                    200,
                    safeSnippet);
            }

            return JournalHarnessPlannerRuntimeResult.Success(
                collector.Operations.ToArray(),
                safeSnippet,
                stopwatch.Elapsed,
                200);
        }
        catch (ClientResultException exception)
        {
            stopwatch.Stop();
            var code = MapStatusToCode(exception.Status);
            return JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    code,
                    "LLM request failed.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed,
                exception.Status);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            stopwatch.Stop();
            return JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    "timeout",
                    "LLM request timed out.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed,
                408);
        }
        catch (Exception exception)
        {
            stopwatch.Stop();
            return JournalHarnessPlannerRuntimeResult.Failure(
                JournalAiSafeError.Create(
                    "runtime",
                    "provider_error",
                    "LLM request failed.",
                    exception.Message,
                    [request.ApiKey]),
                stopwatch.Elapsed);
        }
    }

    private static OpenAiCompatibleRunResult InvalidJsonFailure(
        TimeSpan latency,
        string safeSnippet,
        string apiKey) =>
        OpenAiCompatibleRunResult.Failure(
            JournalAiSafeError.Create(
                "runtime",
                "invalid_json",
                "LLM returned invalid JSON.",
                safeSnippet,
                [apiKey]),
            latency,
            safeResponseSnippet: safeSnippet);

    private static string BuildHarnessPlannerMessage(string protectedContext, string userMessage) =>
        $"""
Protected context:
{protectedContext}

Current user message:
{userMessage}
""";

    internal static ChatOptions CreateJsonChatOptions(OpenAiCompatibleRunRequest request)
    {
        var options = new ChatOptions
        {
            ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json,
            Temperature = (float)request.Temperature,
            MaxOutputTokens = request.MaxTokens > 0 ? request.MaxTokens : null,
            ModelId = request.Model
        };

        ApplyProviderCompatibility(options, request.ProviderId, request.BaseUrl);
        return options;
    }

    internal static ChatOptions CreateHarnessPlannerChatOptions(
        JournalHarnessPlannerRuntimeRequest request,
        IList<AITool> tools)
    {
        var options = new ChatOptions
        {
            Temperature = (float)request.Temperature,
            MaxOutputTokens = request.MaxTokens > 0 ? request.MaxTokens : null,
            ModelId = request.Model,
            ToolMode = ChatToolMode.Auto,
            Tools = tools
        };

        ApplyProviderCompatibility(options, request.ProviderId, request.BaseUrl);
        return options;
    }

    private static void ApplyProviderCompatibility(ChatOptions options, string providerId, string baseUrl)
    {
        if (!ShouldDisableDeepSeekThinking(providerId, baseUrl))
        {
            return;
        }

        options.RawRepresentationFactory = _ =>
        {
            var rawOptions = new ChatCompletionOptions();
            // DeepSeek exposes thinking as an OpenAI-compatible extra body field.
#pragma warning disable SCME0001
            rawOptions.Patch.Set("$.thinking"u8, BinaryData.FromString("""{"type":"disabled"}"""));
#pragma warning restore SCME0001
            return rawOptions;
        };
    }

    internal static bool ShouldDisableDeepSeekThinking(string providerId, string baseUrl)
    {
        if (providerId.Equals("deepseek", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri)
            && uri.Host.Equals("api.deepseek.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string MapStatusToCode(int status) =>
        status switch
        {
            401 => "unauthorized",
            403 => "forbidden",
            404 => "model_not_found",
            408 => "timeout",
            429 => "rate_limited",
            >= 500 => "provider_error",
            _ => "provider_error"
        };
}
