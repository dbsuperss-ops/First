namespace DupeFinderPro.Domain.Models.Organize;

public sealed record FileCondition(
    ConditionType Type,
    ConditionOperator Operator,
    string Value,
    SizeUnit Unit = SizeUnit.Bytes);
