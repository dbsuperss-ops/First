namespace DupeFinderPro.Domain.Models;

public sealed record FileEntry(
    string FullPath,
    string FileName,
    long SizeBytes,
    DateTime LastModified,
    DateTime CreatedAt,
    int SourcePriority)
{
    public string? PartialHash { get; init; }
    public string? FullHash { get; init; }
}
