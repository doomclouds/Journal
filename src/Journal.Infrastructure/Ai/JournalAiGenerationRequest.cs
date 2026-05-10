using Journal.Domain.Entries;

namespace Journal.Infrastructure.Ai;

public sealed record JournalAiGenerationRequest(
    JournalDate Date,
    IReadOnlyList<RawInput> RawInputs,
    DateTimeOffset GeneratedAt,
    JournalAiProviderSettings Settings);

public sealed record JournalAiProviderSettings;
