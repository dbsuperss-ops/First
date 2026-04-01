namespace DupeFinderPro.Domain.Models.Organize;

public sealed record FileMove(
    string OriginalPath,
    string NewPath,
    string FileName,
    string RuleName);
