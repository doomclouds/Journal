using Journal.Domain.Entries;

namespace Journal.Infrastructure.Jmf;

public static class JmfMarkdownValidator
{
    private const string DocumentSchema = "journal-entry/v1";

    public static JmfValidationResult Validate(
        JmfDocument document,
        IReadOnlyList<JmfValidationIssue>? parseIssues = null)
    {
        var issues = new List<JmfValidationIssue>();
        if (parseIssues is not null)
        {
            issues.AddRange(parseIssues);
        }

        if (string.IsNullOrWhiteSpace(document.FrontMatterText))
        {
            issues.Add(CreateIssue("missing-front-matter", "JMF front matter is missing.", "Add the required YAML front matter block."));
        }
        else if (!document.FrontMatter.ContainsKey("schema"))
        {
            issues.Add(CreateIssue("missing-schema", "JMF schema is missing.", "Set schema to journal-entry/v1."));
        }
        else if (!string.Equals(document.FrontMatter["schema"], DocumentSchema, StringComparison.Ordinal))
        {
            issues.Add(CreateIssue("invalid-schema", "JMF schema is invalid.", "Set schema to journal-entry/v1."));
        }

        AddSectionIssues(document.Sections, issues);

        return issues.Count == 0
            ? JmfValidationResult.Valid
            : new JmfValidationResult(false, issues);
    }

    public static JmfValidationResult ValidateBlockEditRequest(JournalBlockEditRequest request)
    {
        var issues = new List<JmfValidationIssue>();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        foreach (var section in request.Sections)
        {
            if (!seen.Add(section.Id))
            {
                issues.Add(CreateIssue("duplicate-section", $"Section '{section.Id}' appears more than once.", "Keep a single block per JMF section id."));
            }

            if (!JmfSectionCatalog.TryGet(section.Id, out var definition))
            {
                issues.Add(CreateIssue("unknown-section", $"Section '{section.Id}' is not a known JMF section.", "Use a known JMF section id."));
                continue;
            }

            if (string.Equals(definition.Id, "raw-inputs", StringComparison.Ordinal))
            {
                issues.Add(CreateIssue("raw-inputs-is-readonly", "Raw inputs cannot be edited in block mode.", "Edit source markdown only when preserving raw input provenance."));
                continue;
            }

            if (!definition.IsEditableInBlockMode)
            {
                issues.Add(CreateIssue("readonly-section", $"Section '{definition.Id}' cannot be edited in block mode.", "Remove this section from the block edit request."));
            }
        }

        return issues.Count == 0
            ? JmfValidationResult.Valid
            : new JmfValidationResult(false, issues);
    }

    private static void AddSectionIssues(IReadOnlyList<JmfSection> sections, List<JmfValidationIssue> issues)
    {
        var sectionIds = sections.Select(section => section.Id).ToArray();
        var sectionIdSet = sectionIds.ToHashSet(StringComparer.Ordinal);

        foreach (var required in JmfSectionCatalog.Required)
        {
            if (!sectionIdSet.Contains(required.Id))
            {
                issues.Add(CreateIssue("missing-required-section", $"Required section '{required.Id}' is missing.", "Restore the required JMF section marker pair."));
            }
        }

        foreach (var section in sections)
        {
            if (!JmfSectionCatalog.TryGet(section.Id, out _))
            {
                issues.Add(CreateIssue("unknown-section", $"Section '{section.Id}' is not a known JMF section.", "Use a known JMF section id or remove the marker pair."));
            }
        }

        foreach (var duplicateGroup in sectionIds.GroupBy(id => id, StringComparer.Ordinal).Where(group => group.Count() > 1))
        {
            issues.Add(CreateIssue("duplicate-section", $"Section '{duplicateGroup.Key}' appears more than once.", "Keep a single marker pair per JMF section id."));
        }
    }

    private static JmfValidationIssue CreateIssue(string code, string message, string repairHint) =>
        new(code, message, repairHint);
}
