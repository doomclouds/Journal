using System.Text;
using Journal.Infrastructure.Ai;
using OpenAI.Chat;

namespace Journal.Tests;

public sealed class OpenAiCompatibleAgentRuntimeTests
{
    [Fact]
    public void CreateJsonChatOptions_DisablesThinkingForDeepSeekProvider()
    {
        var request = new OpenAiCompatibleRunRequest(
            "deepseek",
            "https://api.deepseek.com",
            "deepseek-v4-flash",
            "secret-key",
            "system",
            "user",
            "json_object",
            45,
            0.2,
            1200);

        var options = OpenAiCompatibleAgentRuntime.CreateJsonChatOptions(request);

        AssertDeepSeekThinkingDisabled(options);
    }

    [Fact]
    public void CreateHarnessPlannerChatOptions_DisablesThinkingForDeepSeekEndpoint()
    {
        var request = new JournalHarnessPlannerRuntimeRequest(
            "custom",
            "https://api.deepseek.com/v1",
            "deepseek-v4-pro",
            "secret-key",
            "system",
            "context",
            "user",
            45,
            0.2,
            1200);

        var options = OpenAiCompatibleAgentRuntime.CreateHarnessPlannerChatOptions(request, []);

        AssertDeepSeekThinkingDisabled(options);
    }

    [Fact]
    public void CreateJsonChatOptions_DoesNotSendThinkingForOpenAiProvider()
    {
        var request = new OpenAiCompatibleRunRequest(
            "openai",
            "https://api.openai.com/v1",
            "gpt-5.4",
            "secret-key",
            "system",
            "user",
            "json_object",
            45,
            0.2,
            1200);

        var options = OpenAiCompatibleAgentRuntime.CreateJsonChatOptions(request);

        Assert.Null(options.RawRepresentationFactory);
    }

    private static void AssertDeepSeekThinkingDisabled(Microsoft.Extensions.AI.ChatOptions options)
    {
        var rawOptions = Assert.IsType<ChatCompletionOptions>(
            options.RawRepresentationFactory?.Invoke(null!));

#pragma warning disable SCME0001
        Assert.True(rawOptions.Patch.TryGetJson("$.thinking"u8, out var thinkingJson));
#pragma warning restore SCME0001
        Assert.Equal("""{"type":"disabled"}""", Encoding.UTF8.GetString(thinkingJson.Span));
    }
}
