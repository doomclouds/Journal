namespace Journal.Infrastructure.Clock;

public interface IJournalClock
{
    DateOnly Today { get; }

    DateTimeOffset Now { get; }
}
