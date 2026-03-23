namespace DupeFinderPro.Domain.Models.Organize;

public sealed record Scenario(
    Guid Id,
    string Name,
    bool IsActive,
    string SourceFolder,
    string TargetFolder,
    bool IncludeSubfolders,
    bool ExcludeSystemFiles,
    bool CleanupEmptyFolders,
    ConflictMode ConflictMode,
    IReadOnlyList<ClassifyRule> Rules,
    bool IsScheduled,
    string ScheduleTime,
    IReadOnlyList<string> ScheduleDays);
