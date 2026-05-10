namespace Journal.Infrastructure.Ai;

public sealed record JournalAiSettings(
    string ActiveProviderId,
    IReadOnlyList<JournalAiProviderSettings> Providers)
{
    public static JournalAiSettings CreateDefault() =>
        new(
            "mock",
            [
                new("mock", "mock", "Mock", "mock", "local", "mock-journal", "", true, 1, 0.0, 0, "faithful"),
                new("openai", "openai-compatible", "OpenAI", "openai", "https://api.openai.com/v1", "gpt-5.4", "", false, 45, 0.2, 1200, "faithful"),
                new("deepseek", "openai-compatible", "DeepSeek", "deepseek", "https://api.deepseek.com", "deepseek-v4-flash", "", false, 45, 0.2, 1200, "faithful"),
                new("zhipu", "openai-compatible", "智谱 GLM", "zhipu", "https://open.bigmodel.cn/api/paas/v4", "glm-5.1", "", false, 45, 0.2, 1200, "faithful"),
                new("custom", "openai-compatible", "Custom", "custom", "http://localhost:11434/v1", "", "", false, 45, 0.2, 1200, "faithful")
            ]);
}
