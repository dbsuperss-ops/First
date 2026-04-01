namespace DupeFinderPro.Domain.Models.Organize;

public sealed record OrganizeLogEntry(
    Guid BatchId,
    DateTime Timestamp,
    string FileName,
    string SourcePath,
    string TargetPath,
    string Action);
