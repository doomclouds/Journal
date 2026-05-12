using Journal.Domain.Entries;
using Journal.Infrastructure.Jmf;

namespace Journal.Infrastructure.Harness;

public sealed record JournalHarnessExecutionResult(
    JmfDocument Document,
    JmfValidationResult Validation,
    IReadOnlyList<JmfValidationIssue> Issues);

public static class JournalHarnessOperationExecutor
{
    public static JournalHarnessExecutionResult Apply(
        JmfDocument document,
        IReadOnlyList<JournalHarnessOperation> operations)
    {
        var issues = new List<JmfValidationIssue>();
        var sections = document.Sections.ToList();
        var changed = false;

        foreach (var operation in operations)
        {
            if (string.Equals(operation.Kind, "no-op", StringComparison.Ordinal))
            {
                continue;
            }

            if (operation.Kind is not ("append" or "upsert" or "revise-ai-generated-section"))
            {
                issues.Add(CreateIssue("harness-unknown-operation", $"Operation '{operation.Kind}' is not supported."));
                continue;
            }

            if (!JmfSectionCatalog.TryGet(operation.TargetSectionId, out var definition)
                || string.Equals(definition.Id, "raw-inputs", StringComparison.Ordinal)
                || !definition.IsEditableInBlockMode
                || definition.Kind == JmfSectionKind.System)
            {
                issues.Add(CreateIssue("harness-target-readonly", $"Section '{operation.TargetSectionId}' cannot be edited by harness."));
                continue;
            }

            var index = sections.FindIndex(section => string.Equals(section.Id, operation.TargetSectionId, StringComparison.Ordinal));
            if (operation.Kind == "upsert" && index < 0)
            {
                sections.Add(new JmfSection(
                    definition.Id,
                    definition.Title,
                    operation.Content.Trim(),
                    definition.Kind,
                    definition.IsEditableInBlockMode,
                    new JmfSectionProvenance("ai", "ai", "ai", "create", operation.BasedOnRawInputIds)));
                changed = true;
                continue;
            }

            if (index < 0)
            {
                issues.Add(CreateIssue("harness-target-missing", $"Section '{operation.TargetSectionId}' does not exist."));
                continue;
            }

            var existing = sections[index];
            if (operation.Kind == "revise-ai-generated-section")
            {
                if (!IsPureAiSection(existing))
                {
                    issues.Add(CreateIssue("harness-revise-user-section", $"Section '{operation.TargetSectionId}' is not a pure AI section."));
                    continue;
                }

                sections[index] = existing with
                {
                    Content = operation.Content.Trim(),
                    Provenance = existing.Provenance with
                    {
                        Origin = "ai",
                        CreatedBy = "ai",
                        LastTouchedBy = "ai",
                        LastOperation = "revise",
                        BasedOnRawInputIds = operation.BasedOnRawInputIds
                    }
                };
                changed = true;
                continue;
            }

            sections[index] = existing with
            {
                Content = AppendContent(existing.Content, operation.Content),
                Provenance = existing.Provenance.WithAiAppend(operation.BasedOnRawInputIds)
            };
            changed = true;
        }

        var nextDocument = changed ? document with { Sections = sections } : document;
        var validation = JmfMarkdownValidator.Validate(nextDocument, issues);

        return new JournalHarnessExecutionResult(nextDocument, validation, validation.Issues);
    }

    private static bool IsPureAiSection(JmfSection section) =>
        string.Equals(section.Provenance.Origin, "ai", StringComparison.Ordinal)
        && string.Equals(section.Provenance.CreatedBy, "ai", StringComparison.Ordinal)
        && !string.Equals(section.Provenance.LastTouchedBy, "user", StringComparison.Ordinal);

    private static string AppendContent(string existingContent, string newContent)
    {
        var existing = existingContent.Trim();
        var next = newContent.Trim();

        if (existing.Length == 0)
        {
            return next;
        }

        if (next.Length == 0)
        {
            return existing;
        }

        return $"{existing}\n\n{next}";
    }

    private static JmfValidationIssue CreateIssue(string code, string message) =>
        new(code, message, "Review the harness tool call and retry with an allowed operation.");
}
