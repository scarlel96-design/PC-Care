namespace SmartPerformanceDoctor.AstraVault.Journal;

public enum Av3JournalState : uint
{
    Pending = 0,
    ObjectsDurable = 1,
    MetadataDurable = 2,
    JournalDurable = 3,
    ActivationPending = 4,
    Committed = 5,
    Aborted = 6,
    Stale = 7
}