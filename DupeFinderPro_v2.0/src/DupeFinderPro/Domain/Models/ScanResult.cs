namespace DupeFinderPro.Domain.Models;

public sealed record ScanResult(
    IReadOnlyList<DuplicateGroup> DuplicateGroups,
    int FilesScanned,
    long TotalWastedBytes,
    TimeSpan ElapsedTime,
    IReadOnlyList<FileEntry> SkippedFiles,
    IReadOnlyList<FileEntry>? MergedAliases = null);
