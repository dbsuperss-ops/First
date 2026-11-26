using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels.Duplicate;

public sealed partial class DuplicateGroupViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;

    public int    GroupNumber { get; set; }
    public string GroupLabel  => $"#{GroupNumber}";

    public string Hash        => Group.Hash[..Math.Min(8, Group.Hash.Length)] + "…";
    public string FileCount   => $"{Group.Files.Count}개 파일";
    public string WastedBytes => FormatBytes(Group.WastedBytes);
    public string ExpandIcon  => IsExpanded ? "▲" : "▼";
    public string TotalSize   => FormatBytes(Group.SizeBytes * Group.Files.Count);
    public DuplicateGroup Group { get; }

    public ObservableCollection<FileEntryViewModel> Files { get; }

    // True when ALL files in the group are checked; setting it checks/unchecks all
    public bool IsGroupChecked
    {
        get => Files.Count > 0 && Files.All(f => f.IsChecked);
        set
        {
            foreach (var f in Files)
                f.IsChecked = value;
            OnPropertyChanged(nameof(IsGroupChecked));
        }
    }

    private readonly CleanupOrchestrator _cleanup;
    private readonly Func<int, Task<bool>> _confirmDelete;
    // 같은 그룹의 계획은 한 번에 하나씩 실행한다. 겹치면 서로를 남는 사본으로 셀 수 있다.
    private readonly SemaphoreSlim _gate = new(1, 1);

    /// <param name="confirmDelete">삭제할 파일 수를 받아 사용자의 동의를 묻는다. 파일별 삭제도 이것을 거친다.</param>
    public DuplicateGroupViewModel(DuplicateGroup group, CleanupOrchestrator cleanup, Func<int, Task<bool>> confirmDelete)
    {
        Group = group;
        _cleanup = cleanup;
        _confirmDelete = confirmDelete;
        // 처음 선택값은 모두 "유지"다. 삭제는 사용자가 고르거나 "자동 선택"·"중복 삭제"를 눌렀을 때만 정해진다.
        Files = new ObservableCollection<FileEntryViewModel>(
            group.Files.Select(f => new FileEntryViewModel(f, RunSingleAsync, RestoreAsync)));

        // Bubble file-level checkbox changes up to group checkbox
        foreach (var vm in Files)
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FileEntryViewModel.IsChecked))
                    OnPropertyChanged(nameof(IsGroupChecked));
            };
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandIcon));

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    private FileEntryViewModel? FindKeeper()
    {
        // 이미 삭제·이동한 파일은 남길 후보가 아니다. 후보로 삼으면 남은 사본이 모두 삭제된다.
        var present = Files.Where(f => File.Exists(f.FullPath)).ToList();
        return present.FirstOrDefault(f => f.FullPath == Group.SuggestedKeep?.FullPath)
               ?? present.FirstOrDefault();
    }

    // ── 계획 ─────────────────────────────────────────────────────────────
    /// <summary>"전체 적용": 체크된, 아직 처리하지 않은 파일의 선택값. 실행할 작업이 없으면 null.</summary>
    public GroupPlan? PlanChecked(string quarantinePath, string moveToPath) =>
        Plan(Files.Where(f => f.IsChecked && !f.IsDone && !f.IsKeep && !f.IsProcessing)
                  .Select(f => (f, f.SelectedAction)), quarantinePath, moveToPath);

    /// <summary>"중복 삭제": 선택값을 바꾸지 않고, 추천 파일만 남기고 나머지를 지우는 계획.</summary>
    public GroupPlan? PlanDuplicates(string quarantinePath, string moveToPath)
    {
        var keeper = FindKeeper();
        return keeper is null
            ? null
            : Plan(Files.Where(f => f != keeper && !f.IsDone && !f.IsProcessing)
                        .Select(f => (f, FileAction.Delete)), quarantinePath, moveToPath);
    }

    /// <summary>동의한 계획을 화면의 선택값에 반영한다.</summary>
    public void ShowPlan(GroupPlan plan)
    {
        foreach (var file in Files.Where(f => !f.IsDone))
        {
            var planned = plan.Actions.FirstOrDefault(a => SamePath(a.File, file));
            file.SelectedAction = planned?.Action ?? FileAction.Keep;
            if (planned is not null)
                file.IsChecked = true;
        }
    }

    private GroupPlan? Plan(IEnumerable<(FileEntryViewModel File, FileAction Action)> actions, string quarantinePath, string moveToPath)
    {
        var planned = actions.Select(x => new PlannedAction(x.File.Current, x.Action, moveToPath)).ToList();
        if (planned.Count == 0)
            return null;

        // 지운 파일·격리한 파일은 후보가 아니다. 이동한 파일은 옮긴 위치에서 후보가 된다.
        var candidates = Files.Where(f => (!f.IsDone || f.IsMoved) && !f.IsProcessing).Select(f => f.Current).ToList();
        return new GroupPlan(Group.Hash, candidates, planned, quarantinePath);
    }

    // ── 실행 ─────────────────────────────────────────────────────────────
    /// <summary>이 그룹의 파일이 처리되거나 되돌려진 뒤. 요약 통계를 다시 계산할 때 쓴다.</summary>
    public event Action? Processed;

    /// <summary>휴지통으로 보내지 않고 남아 있는 파일 수(이동·격리한 파일 포함).</summary>
    public int RemainingCount => Files.Count(f => !f.IsDeleted);

    public int DeletedCount => Files.Count(f => f.IsDeleted);

    public async Task ExecuteAsync(GroupPlan plan, CancellationToken ct)
    {
        var targets = plan.Actions
            .Select(a => (Action: a, Vm: Files.First(f => SamePath(a.File, f))))
            .ToList();
        foreach (var (_, vm) in targets)
        {
            vm.IsProcessing = true;
            vm.StatusMessage = "처리 중…";
        }

        try
        {
            await _gate.WaitAsync(ct);
            try
            {
                var results = await _cleanup.ExecuteAsync(plan, ct);
                foreach (var (action, vm) in targets)
                    vm.ShowResult(results.FirstOrDefault(r => r.File == action.File)
                                  ?? new ActionResult(action.File, ActionOutcome.Failed, "실패: 결과 없음"));
            }
            finally
            {
                _gate.Release();
            }
        }
        catch (OperationCanceledException)
        {
            foreach (var (action, vm) in targets)
                vm.ShowResult(new ActionResult(action.File, ActionOutcome.Cancelled, "취소됨"));
        }
        finally
        {
            foreach (var (_, vm) in targets)
                vm.IsProcessing = false;
            Processed?.Invoke();
        }
    }

    /// <summary>파일별 버튼. 일괄 처리와 같은 실행 경로를 지나며, 삭제는 확인을 받는다.</summary>
    private async Task RunSingleAsync(FileEntryViewModel file, FileAction action)
    {
        if (file.IsDone || file.IsProcessing)
            return;
        // 동의하지 않으면 선택값도 바꾸지 않는다.
        if (action == FileAction.Delete && !await _confirmDelete(1))
            return;

        file.SelectedAction = action;
        var plan = Plan([(file, action)], file.QuarantinePath, file.MoveToPath)!;
        await ExecuteAsync(plan, CancellationToken.None);
        if (action == FileAction.Delete && file.Outcome == ActionOutcome.Held)
            file.SelectedAction = FileAction.Keep;
    }

    /// <summary>이동·격리한 파일을 원래 위치로 되돌린다. 같은 그룹의 다른 작업과 겹치지 않게 실행한다.</summary>
    private async Task RestoreAsync(FileEntryViewModel file)
    {
        file.IsProcessing = true;
        file.StatusMessage = "되돌리는 중…";
        await _gate.WaitAsync();
        try
        {
            file.ShowRestored(await _cleanup.RestoreAsync(file.Entry, file.CurrentPath, Group.Hash));
        }
        finally
        {
            _gate.Release();
            file.IsProcessing = false;
            Processed?.Invoke();
        }
    }

    private static bool SamePath(FileEntry entry, FileEntryViewModel file) =>
        string.Equals(entry.FullPath, file.CurrentPath, StringComparison.OrdinalIgnoreCase);

    [RelayCommand]
    private void AutoSelect()
    {
        var keeper = FindKeeper();

        foreach (var file in Files.Where(f => !f.IsDone))
            file.SelectedAction = file == keeper ? FileAction.Keep : FileAction.Delete;
    }

    [RelayCommand]
    private void KeepAll()
    {
        foreach (var file in Files.Where(f => !f.IsDone))
            file.SelectedAction = FileAction.Keep;
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
