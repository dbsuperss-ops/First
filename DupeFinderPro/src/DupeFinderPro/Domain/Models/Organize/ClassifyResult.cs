namespace DupeFinderPro.Domain.Models.Organize;

public sealed record ClassifyResult(
    string FileName,
    string SourcePath,
    string TargetPath,
    string RuleName,
    long FileSize);
