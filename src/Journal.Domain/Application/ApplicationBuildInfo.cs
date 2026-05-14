namespace Journal.Domain.Application;

public sealed record ApplicationBuildInfo(
    string ReleaseVersion,
    string FrontendVersion,
    string Commit,
    string BuildTimeUtc)
{
    public static ApplicationBuildInfo Current { get; } = new(
        GetValue("JOURNAL_RELEASE_VERSION", ApplicationInfo.Version),
        GetValue("JOURNAL_FRONTEND_VERSION", ApplicationInfo.Version),
        GetValue("JOURNAL_BUILD_COMMIT", "dev"),
        GetValue("JOURNAL_BUILD_TIME_UTC", "local"));

    private static string GetValue(string name, string fallback)
    {
        var value = Environment.GetEnvironmentVariable(name);
        return string.IsNullOrWhiteSpace(value) ? fallback : value;
    }
}
