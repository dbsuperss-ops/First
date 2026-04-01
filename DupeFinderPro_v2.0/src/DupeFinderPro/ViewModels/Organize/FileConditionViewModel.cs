using CommunityToolkit.Mvvm.ComponentModel;
using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class FileConditionViewModel : ViewModelBase
{
    [ObservableProperty] private ConditionType _type = ConditionType.Extension;
    [ObservableProperty] private ConditionOperator _operator = ConditionOperator.Equals;
    [ObservableProperty] private string _value = string.Empty;
    [ObservableProperty] private SizeUnit _unit = SizeUnit.Bytes;

    public FileConditionViewModel() { }

    public FileConditionViewModel(FileCondition c)
    {
        _type = c.Type;
        _operator = c.Operator;
        _value = c.Value;
        _unit = c.Unit;
    }

    public FileCondition ToModel() => new(Type, Operator, Value, Unit);
}
