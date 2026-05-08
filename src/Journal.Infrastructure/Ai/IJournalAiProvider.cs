using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public interface IJournalAiProvider
{
    JournalAiJson Generate(JournalDate date, IReadOnlyList<RawInput> rawInputs, DateTimeOffset generatedAt);
}
