namespace DupeFinderPro.Domain.Models.Organize;

public enum ConditionOperator
{
    Equals, NotEquals,
    Contains, DoesNotContain,
    StartsWith, EndsWith, Regex,
    GreaterThan, LessThan,   // Size
    Year, Month              // Date
}
