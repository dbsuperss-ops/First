using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed class ScenarioItemViewModel : ViewModelBase
{
    public Guid Id { get; }
    public string ScenarioName { get; }
    public bool IsActive { get; }
    public string SourceFolder { get; }
    public string TargetFolder { get; }
    public bool IsScheduled { get; }
    public bool IsWatching { get; }
    public int RuleCount { get; }

    public ScenarioItemViewModel(Scenario scenario, bool isWatching = false)
    {
        Id = scenario.Id;
        ScenarioName = scenario.Name;
        IsActive = scenario.IsActive;
        SourceFolder = scenario.SourceFolder;
        TargetFolder = scenario.TargetFolder;
        IsScheduled = scenario.IsScheduled;
        IsWatching = isWatching;
        RuleCount = scenario.Rules.Count;
    }
}
