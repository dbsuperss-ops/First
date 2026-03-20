namespace DupeFinderPro.Domain.Models.Organize;

public sealed record ClassifyRule(
    string RuleName,
    IReadOnlyList<FileCondition> Conditions,
    ConditionLogic Logic,
    string TargetPath,
    string Destination,
    DestinationMode DestinationMode);
