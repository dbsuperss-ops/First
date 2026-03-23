using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class ClassifyRuleViewModel : ViewModelBase
{
    [ObservableProperty] private string _ruleName = string.Empty;
    [ObservableProperty] private ConditionLogic _logic = ConditionLogic.Or;
    [ObservableProperty] private string _targetPath = string.Empty;
    [ObservableProperty] private string _destination = string.Empty;
    [ObservableProperty] private DestinationMode _destinationMode = DestinationMode.Default;

    public ObservableCollection<FileConditionViewModel> Conditions { get; } = [];

    public ClassifyRuleViewModel() { }

    public ClassifyRuleViewModel(ClassifyRule rule)
    {
        _ruleName = rule.RuleName;
        _logic = rule.Logic;
        _targetPath = rule.TargetPath;
        _destination = rule.Destination;
        _destinationMode = rule.DestinationMode;
        foreach (var c in rule.Conditions)
            Conditions.Add(new FileConditionViewModel(c));
    }

    [RelayCommand]
    private void AddCondition() => Conditions.Add(new FileConditionViewModel());

    [RelayCommand]
    private void RemoveCondition(FileConditionViewModel condition) => Conditions.Remove(condition);

    public ClassifyRule ToModel() => new(
        RuleName,
        Conditions.Select(c => c.ToModel()).ToList(),
        Logic, TargetPath, Destination, DestinationMode);
}
