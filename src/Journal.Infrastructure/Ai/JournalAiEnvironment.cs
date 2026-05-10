namespace Journal.Infrastructure.Ai;

public interface IJournalAiEnvironment
{
    string? Get(string name);
}

public sealed class SystemJournalAiEnvironment : IJournalAiEnvironment
{
    public string? Get(string name)
    {
        var processValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Process);
        if (!string.IsNullOrWhiteSpace(processValue))
        {
            return processValue;
        }

        if (!OperatingSystem.IsWindows())
        {
            return processValue;
        }

        var userValue = Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.User);
        if (!string.IsNullOrWhiteSpace(userValue))
        {
            return userValue;
        }

        return Environment.GetEnvironmentVariable(name, EnvironmentVariableTarget.Machine);
    }
}
