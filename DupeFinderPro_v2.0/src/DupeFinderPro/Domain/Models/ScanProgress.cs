namespace DupeFinderPro.Domain.Models;

public sealed record ScanProgress(
    ScanPhase Phase,
    int ProcessedCount,
    int TotalCount,
    int DuplicateGroupCount,
    string CurrentFile = "");

public enum ScanPhase
{
    Collecting,
    PartialHashing,
    FullHashing,
    Completed
}
