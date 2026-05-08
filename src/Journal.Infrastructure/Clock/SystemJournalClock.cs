namespace Journal.Infrastructure.Clock;

public sealed class SystemJournalClock : IJournalClock
{
    public DateOnly Today => DateOnly.FromDateTime(DateTime.Now);

    public DateTimeOffset Now => DateTimeOffset.Now;
}
