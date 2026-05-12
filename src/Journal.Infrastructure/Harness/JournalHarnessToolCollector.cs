using Journal.Domain.Entries;

namespace Journal.Infrastructure.Harness;

public sealed class JournalHarnessToolCollector
{
    private readonly List<JournalHarnessOperation> _operations = [];

    public IReadOnlyList<JournalHarnessOperation> Operations => _operations;

    public string AppendJournalSection(
        string sectionId,
        string content,
        string[] basedOnRawInputIds,
        string reason)
    {
        _operations.Add(JournalHarnessOperation.Append(sectionId, content, basedOnRawInputIds, reason));
        return "accepted: append journal section operation recorded for planner review.";
    }

    public string UpsertJournalSection(
        string sectionId,
        string content,
        string[] basedOnRawInputIds,
        string reason)
    {
        _operations.Add(JournalHarnessOperation.Upsert(sectionId, content, basedOnRawInputIds, reason));
        return "accepted: upsert journal section operation recorded for planner review.";
    }

    public string ReviseAiGeneratedSection(
        string sectionId,
        string content,
        string[] basedOnRawInputIds,
        string reason)
    {
        _operations.Add(JournalHarnessOperation.ReviseAiGeneratedSection(sectionId, content, basedOnRawInputIds, reason));
        return "accepted: revise AI-generated section operation recorded for planner review.";
    }

    public string NoOp(string reason)
    {
        _operations.Add(JournalHarnessOperation.NoOp(reason));
        return "accepted: no-op operation recorded for planner review.";
    }
}
