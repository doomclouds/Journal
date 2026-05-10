using System.ClientModel;
using System.Diagnostics;
using System.Text.Json;
using Journal.Domain.Entries;
using Microsoft.Agents.AI;
using Microsoft.Extensions.AI;
using OpenAI;
using OpenAI.Chat;

namespace Journal.Infrastructure.Ai;

public sealed class OpenAiCompatibleAgentRuntime : IJournalAiAgentRuntime
{
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
            var options = new ChatClientAgentRunOptions(new ChatOptions
            {
                ResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat.Json,
                Temperature = (float)request.Temperature,
                MaxOutputTokens = request.MaxTokens > 0 ? request.MaxTokens : null,
                ModelId = request.Model
            });

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
