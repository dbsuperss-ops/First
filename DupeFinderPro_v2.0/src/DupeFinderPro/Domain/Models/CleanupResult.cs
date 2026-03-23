namespace DupeFinderPro.Domain.Models;

public sealed record CleanupResult(
    int DeletedCount,
    long FreedBytes,
    IReadOnlyList<string> Errors);
