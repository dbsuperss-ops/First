namespace DupeFinderPro.Domain.Models.Organize;

public sealed record ClassifyRecord(
    Guid Id,
    DateTime ExecutedAt,
    string ScenarioName,
    string SourceFolder,
    string TargetFolder,
    int FileCount,
    long TotalBytes,
    IReadOnlyList<FileMove> Files);
