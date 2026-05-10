namespace Journal.Infrastructure.Ai;

public interface IJournalAiEnvironment
{
    string? Get(string name);
}

public sealed class SystemJournalAiEnvironment : IJournalAiEnvironment
{
    public string? Get(string name) => Environment.GetEnvironmentVariable(name);
}
