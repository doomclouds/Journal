using System.Globalization;
using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public static class JournalAiJsonValidator
{
    public static JournalAiValidationResult Validate(JournalAiJson aiJson)
    {
        var errors = new List<string>();

        if (!string.Equals(aiJson.Schema, "journal.v1", StringComparison.Ordinal))
        {
            errors.Add("schema must be journal.v1.");
        }

        if (!DateOnly.TryParseExact(aiJson.Date, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        {
            errors.Add("date must use yyyy-MM-dd.");
        }

        if (string.IsNullOrWhiteSpace(aiJson.MonthDay))
        {
            errors.Add("monthDay is required.");
        }
        else if (date != default && !string.Equals(aiJson.MonthDay, date.ToString("MM-dd", CultureInfo.InvariantCulture), StringComparison.Ordinal))
        {
            errors.Add("monthDay must match date.");
        }

        if (IsEmpty(aiJson.RawInputs))
        {
            errors.Add("rawInputs must contain at least one item.");
        }

        if (IsEmpty(aiJson.YesterdayReview))
        {
            errors.Add("yesterdayReview must contain at least one item.");
        }

        if (IsEmpty(aiJson.TodayFocus))
        {
            errors.Add("todayFocus must contain at least one item.");
        }

        return errors.Count == 0
            ? JournalAiValidationResult.Valid
            : JournalAiValidationResult.Invalid(errors.ToArray());
    }

    private static bool IsEmpty(IReadOnlyList<string> values) =>
        values.Count == 0 || values.All(string.IsNullOrWhiteSpace);
}
