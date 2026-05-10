using System.Text.RegularExpressions;

namespace Journal.Infrastructure.Ai;

public sealed record JournalAiSafeError(
    string Stage,
    string Code,
    string Message,
    string TechnicalDetails)
{
    private static readonly Regex AuthorizationBearerRegex = new(
        @"\bAuthorization\s*[:=]\s*Bearer\s+[^\s,;]+",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    private static readonly Regex ApiKeyRegex = new(
        @"\bapi_?key\s*[:=]\s*[""']?[^\s,;""']+[""']?",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public static JournalAiSafeError Create(string stage, string code, string message, string technicalDetails) =>
        new(stage, code, message, Redact(technicalDetails));

    public static string Redact(string value) =>
        ApiKeyRegex.Replace(
            AuthorizationBearerRegex.Replace(value, "[redacted-header]: [redacted-value]"),
            "[redacted-key-name]=[redacted-value]");
}
