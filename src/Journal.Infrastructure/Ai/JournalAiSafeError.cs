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

    public static JournalAiSafeError Create(
        string stage,
        string code,
        string message,
        string technicalDetails,
        IEnumerable<string>? sensitiveValues = null) =>
        new(stage, code, message, Redact(technicalDetails, sensitiveValues));

    public static string Redact(string value, IEnumerable<string>? sensitiveValues = null)
    {
        var redacted = ApiKeyRegex.Replace(
            AuthorizationBearerRegex.Replace(value, "[redacted-header]: [redacted-value]"),
            "[redacted-key-name]=[redacted-value]");

        if (sensitiveValues is null)
        {
            return redacted;
        }

        foreach (var sensitiveValue in sensitiveValues.Where(item => !string.IsNullOrWhiteSpace(item)))
        {
            redacted = redacted.Replace(sensitiveValue, "[redacted-value]", StringComparison.Ordinal);
        }

        return redacted;
    }
}
