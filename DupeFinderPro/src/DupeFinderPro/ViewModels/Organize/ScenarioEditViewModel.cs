using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class ScenarioEditViewModel : ViewModelBase
{
    private Guid _editingId = Guid.Empty;

    public event Action<Scenario>? Saved;
    public event Action? Cancelled;

    // ── Basic properties ─────────────────────────────────────────────────
    [ObservableProperty] private string _name = string.Empty;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _sourceFolder = string.Empty;
    [ObservableProperty] private string _targetFolder = string.Empty;
    [ObservableProperty] private bool _includeSubfolders;
    [ObservableProperty] private bool _excludeSystemFiles = true;
    [ObservableProperty] private bool _cleanupEmptyFolders;
    [ObservableProperty] private ConflictMode _conflictMode = ConflictMode.Rename;

    // ── Schedule ─────────────────────────────────────────────────────────
    [ObservableProperty] private bool _isScheduled;
    [ObservableProperty] private string _scheduleTime = "09:00";

    public ObservableCollection<string> ScheduleDays { get; } = [];
    public ObservableCollection<ClassifyRuleViewModel> Rules { get; } = [];

    // ── Validation ────────────────────────────────────────────────────────
    [ObservableProperty] private string _validationError = string.Empty;
    [ObservableProperty] private bool _hasValidationError;

    // ── Day toggle helpers ───────────────────────────────────────────────
    public bool IsMon { get => ScheduleDays.Contains("월"); set => ToggleDay("월", value); }
    public bool IsTue { get => ScheduleDays.Contains("화"); set => ToggleDay("화", value); }
    public bool IsWed { get => ScheduleDays.Contains("수"); set => ToggleDay("수", value); }
    public bool IsThu { get => ScheduleDays.Contains("목"); set => ToggleDay("목", value); }
    public bool IsFri { get => ScheduleDays.Contains("금"); set => ToggleDay("금", value); }
    public bool IsSat { get => ScheduleDays.Contains("토"); set => ToggleDay("토", value); }
    public bool IsSun { get => ScheduleDays.Contains("일"); set => ToggleDay("일", value); }

    private void ToggleDay(string day, bool on)
    {
        if (on && !ScheduleDays.Contains(day)) ScheduleDays.Add(day);
        else if (!on) ScheduleDays.Remove(day);
        OnPropertyChanged(DayProperty(day));
    }

    private static string DayProperty(string day) => day switch
    {
        "월" => nameof(IsMon), "화" => nameof(IsTue), "수" => nameof(IsWed),
        "목" => nameof(IsThu), "금" => nameof(IsFri), "토" => nameof(IsSat),
        _   => nameof(IsSun)
    };

    public void Initialize(Scenario? scenario = null)
    {
        Rules.Clear();
        ScheduleDays.Clear();

        if (scenario is null)
        {
            _editingId = Guid.Empty;
            Name = string.Empty;
            IsActive = true;
            SourceFolder = string.Empty;
            TargetFolder = string.Empty;
            IncludeSubfolders = false;
            ExcludeSystemFiles = true;
            CleanupEmptyFolders = false;
            ConflictMode = ConflictMode.Rename;
            IsScheduled = false;
            ScheduleTime = "09:00";
        }
        else
        {
            _editingId = scenario.Id;
            Name = scenario.Name;
            IsActive = scenario.IsActive;
            SourceFolder = scenario.SourceFolder;
            TargetFolder = scenario.TargetFolder;
            IncludeSubfolders = scenario.IncludeSubfolders;
            ExcludeSystemFiles = scenario.ExcludeSystemFiles;
            CleanupEmptyFolders = scenario.CleanupEmptyFolders;
            ConflictMode = scenario.ConflictMode;
            IsScheduled = scenario.IsScheduled;
            ScheduleTime = scenario.ScheduleTime;
            foreach (var day in scenario.ScheduleDays) ScheduleDays.Add(day);
            foreach (var rule in scenario.Rules) Rules.Add(new ClassifyRuleViewModel(rule));
        }

        // Notify all day properties
        foreach (var p in new[] { nameof(IsMon), nameof(IsTue), nameof(IsWed), nameof(IsThu), nameof(IsFri), nameof(IsSat), nameof(IsSun) })
            OnPropertyChanged(p);

        HasValidationError = false;
        ValidationError = string.Empty;
    }

    [RelayCommand]
    private void AddRule() => Rules.Add(new ClassifyRuleViewModel());

    [RelayCommand]
    private void RemoveRule(ClassifyRuleViewModel rule) => Rules.Remove(rule);

    [RelayCommand]
    private void Save()
    {
        if (!Validate()) return;

        var id = _editingId == Guid.Empty ? Guid.NewGuid() : _editingId;
        var scenario = new Scenario(
            id, Name.Trim(), IsActive, SourceFolder.Trim(), TargetFolder.Trim(),
            IncludeSubfolders, ExcludeSystemFiles, CleanupEmptyFolders, ConflictMode,
            Rules.Select(r => r.ToModel()).ToList(),
            IsScheduled, ScheduleTime, [.. ScheduleDays]);

        Saved?.Invoke(scenario);
    }

    [RelayCommand]
    private void Cancel() => Cancelled?.Invoke();

    private bool Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            ValidationError = "시나리오 이름을 입력해주세요.";
            HasValidationError = true;
            return false;
        }
        if (string.IsNullOrWhiteSpace(SourceFolder))
        {
            ValidationError = "원본 폴더를 선택해주세요.";
            HasValidationError = true;
            return false;
        }
        if (string.IsNullOrWhiteSpace(TargetFolder))
        {
            ValidationError = "대상 폴더를 선택해주세요.";
            HasValidationError = true;
            return false;
        }
        if (string.Equals(SourceFolder.Trim(), TargetFolder.Trim(), StringComparison.OrdinalIgnoreCase))
        {
            ValidationError = "원본 폴더와 대상 폴더가 같을 수 없습니다.";
            HasValidationError = true;
            return false;
        }
        HasValidationError = false;
        ValidationError = string.Empty;
        return true;
    }
}
