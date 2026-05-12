using Journal.Domain.Entries;

namespace Journal.Infrastructure.Harness;

public sealed class JournalHarnessToolCollector
{
    private readonly List<JournalHarnessOperation> _operations = [];

    public IReadOnlyList<JournalHarnessOperation> Operations => _operations;

    public string AppendJournalSection(
        string? sectionId,
        string? content,
        string[]? basedOnRawInputIds,
        string? reason)
    {
        _operations.Add(JournalHarnessOperation.Append(
            Normalize(sectionId),
            Normalize(content),
            Normalize(basedOnRawInputIds),
            Normalize(reason)));
        return "accepted: append journal section operation recorded for planner review.";
    }

    public string UpsertJournalSection(
        string? sectionId,
        string? content,
        string[]? basedOnRawInputIds,
        string? reason)
    {
        _operations.Add(JournalHarnessOperation.Upsert(
            Normalize(sectionId),
            Normalize(content),
            Normalize(basedOnRawInputIds),
            Normalize(reason)));
        return "accepted: upsert journal section operation recorded for planner review.";
    }

    public string ReviseAiGeneratedSection(
        string? sectionId,
        string? content,
        string[]? basedOnRawInputIds,
        string? reason)
    {
        _operations.Add(JournalHarnessOperation.ReviseAiGeneratedSection(
            Normalize(sectionId),
            Normalize(content),
            Normalize(basedOnRawInputIds),
            Normalize(reason)));
        return "accepted: revise AI-generated section operation recorded for planner review.";
    }

    public string NoOp(string? reason)
    {
        _operations.Add(JournalHarnessOperation.NoOp(Normalize(reason)));
        return "accepted: no-op operation recorded for planner review.";
    }

    private static string Normalize(string? value) => value ?? string.Empty;

    private static IReadOnlyList<string> Normalize(string[]? values) =>
        values is null ? Array.Empty<string>() : values.Select(Normalize).ToArray();
}
