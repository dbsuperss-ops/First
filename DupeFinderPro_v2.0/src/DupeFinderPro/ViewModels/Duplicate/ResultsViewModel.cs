using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels.Duplicate;

public sealed partial class ResultsViewModel : ViewModelBase
{
    private readonly ScanJobService _scanJobService;
    private readonly CleanupOrchestrator _cleanup;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _applyCts;
    private ScanJob? _currentJob;

    // ── Scan progress ─────────────────────────────────────────────────────
    [ObservableProperty] private bool _isScanning;
    [ObservableProperty] private bool _hasResults;
    [ObservableProperty] private bool _isEmpty;
    [ObservableProperty] private string _phaseLabel    = string.Empty;
    [ObservableProperty] private string _currentFile   = string.Empty;
    [ObservableProperty] private int    _progressValue;
    [ObservableProperty] private int    _progressMax   = 100;
    [ObservableProperty] private bool   _isIndeterminate;

    // ── Result summary ────────────────────────────────────────────────────
    [ObservableProperty] private int    _totalGroups;
    [ObservableProperty] private int    _totalFiles;
    [ObservableProperty] private string _totalWasted   = string.Empty;
    [ObservableProperty] private string _scanDuration  = string.Empty;
    [ObservableProperty] private string _scanName      = string.Empty;
    [ObservableProperty] private string _errorMessage  = string.Empty;
    [ObservableProperty] private bool   _hasScanError;
    [ObservableProperty] private bool   _hasSkippedFiles;
    [ObservableProperty] private string _skippedSummary = string.Empty;
    [ObservableProperty] private string _skippedPaths   = string.Empty;
    [ObservableProperty] private bool   _hasMergedAliases;
    [ObservableProperty] private string _mergedAliasSummary = string.Empty;

    /// <summary>삭제할 파일 수를 받아 사용자의 동의를 묻는다. View가 설정하며, 없으면 일괄·파일별 모두 삭제하지 않는다.</summary>
    public Func<int, Task<bool>>? ConfirmDeleteAsync { get; set; }

    // ── Cleanup options ────────────────────────────────────────────────────
    [ObservableProperty] private string _quarantinePath = QuarantineFolder.DefaultPath;
    [ObservableProperty] private string _moveToPath = string.Empty;
    [ObservableProperty] private bool   _isApplying;

    public string QuarantineFolderName => FolderDisplayName(QuarantinePath);
    public string MoveToFolderName     => FolderDisplayName(MoveToPath);

    // ── Preview ───────────────────────────────────────────────────────────
    [ObservableProperty] private FileEntryViewModel? _selectedFile;
    [ObservableProperty] private Bitmap?             _previewBitmap;
    [ObservableProperty] private bool                _isImagePreview;
    [ObservableProperty] private bool                _hasSelectedFile;

    // ── Sort / Filter ─────────────────────────────────────────────────────
    [ObservableProperty] private string _selectedSortMode   = "낭비 공간";
    [ObservableProperty] private string _filterExtension    = string.Empty;
    [ObservableProperty] private double _filterMinSizeMb;

    public static IReadOnlyList<string> SortModeLabels { get; } =
        ["낭비 공간", "파일 수", "파일명", "파일 크기"];

    // ── Groups ────────────────────────────────────────────────────────────
    private readonly List<DuplicateGroupViewModel> _allGroups = [];
    public ObservableCollection<DuplicateGroupViewModel> Groups { get; } = [];

    public ResultsViewModel(ScanJobService scanJobService, CleanupOrchestrator cleanup)
    {
        _scanJobService = scanJobService;
        _cleanup = cleanup;
    }

    public void StartScan(ScanJob job)
    {
        _currentJob = job;
        ScanName = job.Name;
        IsScanning = true;
        HasResults = false;
        IsEmpty = false;
        HasScanError = false;
        ErrorMessage = string.Empty;
        HasSkippedFiles = false;
        HasMergedAliases = false;
        Groups.Clear();
        PhaseLabel = "준비 중…";
        CurrentFile = string.Empty;
        ProgressValue = 0;
        IsIndeterminate = true;
        SelectedFile = null;
        PreviewBitmap = null;
        HasSelectedFile = false;

        _cts = new CancellationTokenSource();
        _ = RunScanAsync(job, _cts.Token);
    }

    private async Task RunScanAsync(ScanJob job, CancellationToken ct)
    {
        var progress = new Progress<ScanProgress>(OnProgress);
        await _scanJobService.RunJobAsync(job, progress, ct);

        IsScanning = false;
        IsIndeterminate = false;

        if (job.Status == ScanJobStatus.Completed && job.Result is { } result)
            LoadResults(result);
        else if (job.Status == ScanJobStatus.Failed)
        {
            HasScanError = true;
            ErrorMessage = job.ErrorMessage ?? "알 수 없는 오류";
        }
    }

    private void OnProgress(ScanProgress p)
    {
        PhaseLabel = p.Phase switch
        {
            ScanPhase.Collecting     => "파일 수집 중…",
            ScanPhase.PartialHashing => $"빠른 해시 중… ({p.ProcessedCount}/{p.TotalCount})",
            ScanPhase.FullHashing    => $"전체 해시 중… ({p.ProcessedCount}/{p.TotalCount})",
            ScanPhase.Completed      => "완료",
            _                        => string.Empty
        };

        CurrentFile = p.CurrentFile;

        if (p.TotalCount > 0)
        {
            IsIndeterminate = false;
            ProgressMax = p.TotalCount;
            ProgressValue = p.ProcessedCount;
        }
        else
        {
            IsIndeterminate = true;
        }
    }

    private void LoadResults(ScanResult result)
    {
        TotalGroups  = result.DuplicateGroups.Count;
        TotalFiles   = result.FilesScanned;
        TotalWasted  = FormatBytes(result.TotalWastedBytes);
        ScanDuration = $"{result.ElapsedTime.TotalSeconds:F1}s";

        HasSkippedFiles = result.SkippedFiles.Count > 0;
        SkippedSummary  = $"검사하지 못한 파일 {result.SkippedFiles.Count}개 (읽을 수 없거나 클라우드에만 있음). 중복 여부를 알 수 없습니다.";
        SkippedPaths    = string.Join(Environment.NewLine, result.SkippedFiles.Select(f => f.FullPath));

        var aliases = result.MergedAliases ?? [];
        HasMergedAliases   = aliases.Count > 0;
        MergedAliasSummary = $"같은 실제 파일을 가리키는 경로 {aliases.Count}개(정션·매핑 드라이브·하드 링크 등)는 한 번만 셌습니다.";

        _allGroups.Clear();
        Groups.Clear();
        var index = 1;
        foreach (var g in result.DuplicateGroups.OrderByDescending(g => g.WastedBytes))
        {
            var groupVm = new DuplicateGroupViewModel(g, _cleanup, ConfirmDeleteForFileAsync) { GroupNumber = index++ };
            SyncPathsToGroup(groupVm);
            _allGroups.Add(groupVm);
        }

        ApplySortFilter();
        HasResults = TotalGroups > 0;
        IsEmpty    = TotalGroups == 0;
    }

    private Task<bool> ConfirmDeleteForFileAsync(int count) =>
        ConfirmDeleteAsync?.Invoke(count) ?? Task.FromResult(false);

    private void SyncPathsToGroup(DuplicateGroupViewModel group)
    {
        foreach (var f in group.Files)
        {
            f.QuarantinePath = QuarantinePath;
            f.MoveToPath     = MoveToPath;
        }
    }

    partial void OnQuarantinePathChanged(string value)
    {
        OnPropertyChanged(nameof(QuarantineFolderName));
        foreach (var g in Groups)
            foreach (var f in g.Files)
                f.QuarantinePath = value;
    }

    partial void OnMoveToPathChanged(string value)
    {
        OnPropertyChanged(nameof(MoveToFolderName));
        foreach (var g in Groups)
            foreach (var f in g.Files)
                f.MoveToPath = value;
    }

    partial void OnSelectedSortModeChanged(string value)  => ApplySortFilter();
    partial void OnFilterExtensionChanged(string value)   => ApplySortFilter();
    partial void OnFilterMinSizeMbChanged(double value)   => ApplySortFilter();

    private void ApplySortFilter()
    {
        var filtered = _allGroups.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(FilterExtension))
        {
            var ext = FilterExtension.Trim().TrimStart('.');
            filtered = filtered.Where(g => g.Group.Files.Any(f =>
                Path.GetExtension(f.FileName).TrimStart('.')
                    .Equals(ext, StringComparison.OrdinalIgnoreCase)));
        }

        if (FilterMinSizeMb > 0)
        {
            var minBytes = (long)(FilterMinSizeMb * 1_048_576);
            filtered = filtered.Where(g => g.Group.WastedBytes >= minBytes);
        }

        var sorted = SelectedSortMode switch
        {
            "파일 수"  => filtered.OrderByDescending(g => g.Group.Files.Count),
            "파일명"   => filtered.OrderBy(g => g.Group.Files.FirstOrDefault()?.FileName ?? string.Empty),
            "파일 크기" => filtered.OrderByDescending(g => g.Group.SizeBytes),
            _          => filtered.OrderByDescending(g => g.Group.WastedBytes)
        };

        Groups.Clear();
        foreach (var g in sorted)
            Groups.Add(g);
    }

    // ── Preview ───────────────────────────────────────────────────────────
    [RelayCommand]
    private async Task SelectFile(FileEntryViewModel file)
    {
        SelectedFile = file;
        HasSelectedFile = true;
        PreviewBitmap = null;
        IsImagePreview = false;

        if (FileEntryViewModel.IsImageFile(file.FullPath) && File.Exists(file.FullPath))
        {
            IsImagePreview = true;
            try
            {
                PreviewBitmap = await Task.Run(() =>
                {
                    using var stream = File.OpenRead(file.FullPath);
                    return new Bitmap(stream);
                });
            }
            catch
            {
                IsImagePreview = false;
            }
        }
    }

    // ── Commands ──────────────────────────────────────────────────────────
    [RelayCommand]
    private void CancelScan() => _cts?.Cancel();

    /// <summary>모든 파일의 체크박스를 선택합니다.</summary>
    [RelayCommand]
    private void CheckAll()
    {
        foreach (var g in Groups)
            g.IsGroupChecked = true;
    }

    /// <summary>각 그룹에서 추천 파일만 남기고 나머지를 지우는 계획을 만들어, 확인을 받은 뒤 실행합니다.</summary>
    [RelayCommand]
    private Task DeleteDuplicatesAsync() =>
        ConfirmAndRunAsync(g => g.PlanDuplicates(QuarantinePath, MoveToPath), showPlan: true);

    /// <summary>체크된 파일(Keep 제외)을 삭제로 설정합니다.</summary>
    [RelayCommand]
    private void SetAllDelete()
    {
        foreach (var g in Groups)
            foreach (var f in g.Files)
                if (!f.IsDone && f.IsChecked && f.SelectedAction != FileAction.Keep)
                    f.SelectedAction = FileAction.Delete;
    }

    /// <summary>체크된 파일(Keep 제외)을 격리로 설정합니다.</summary>
    [RelayCommand]
    private void SetAllQuarantine()
    {
        foreach (var g in Groups)
            foreach (var f in g.Files)
                if (!f.IsDone && f.IsChecked && f.SelectedAction != FileAction.Keep)
                    f.SelectedAction = FileAction.Quarantine;
    }

    /// <summary>체크된 파일(Keep 제외)을 이동으로 설정합니다.</summary>
    [RelayCommand]
    private void SetAllMove()
    {
        foreach (var g in Groups)
            foreach (var f in g.Files)
                if (!f.IsDone && f.IsChecked && f.SelectedAction != FileAction.Keep)
                    f.SelectedAction = FileAction.MoveToFolder;
    }

    [RelayCommand]
    private Task ApplyAllAsync() =>
        ConfirmAndRunAsync(g => g.PlanChecked(QuarantinePath, MoveToPath), showPlan: false);

    /// <summary>진행 중인 일괄 처리를 멈춘다. 이미 끝난 파일은 그대로이고, 남은 파일은 "취소됨"이 된다.</summary>
    [RelayCommand]
    private void CancelApply() => _applyCts?.Cancel();

    /// <summary>
    /// 동의하기 전에 그룹마다 실행 계획을 확정하고, 동의한 뒤에는 그 계획만 실행합니다.
    /// 확인 창이 떠 있는 동안 선택값이나 파일이 바뀌어도 실행 대상은 바뀌지 않습니다.
    /// </summary>
    private async Task ConfirmAndRunAsync(Func<DuplicateGroupViewModel, GroupPlan?> planFor, bool showPlan)
    {
        if (IsApplying) return;
        IsApplying = true;
        try
        {
            var plans = Groups
                .Select(g => (Group: g, Plan: planFor(g)))
                .Where(x => x.Plan is not null)
                .Select(x => (x.Group, Plan: x.Plan!))
                .ToList();

            var deleteCount = plans.Sum(x => x.Plan.Actions.Count(a => a.Action == FileAction.Delete));
            if (deleteCount > 0 &&
                (ConfirmDeleteAsync is null || !await ConfirmDeleteAsync(deleteCount)))
                return;

            if (showPlan)
                foreach (var (group, plan) in plans)
                    group.ShowPlan(plan);

            using var cts = new CancellationTokenSource();
            _applyCts = cts;
            foreach (var (group, plan) in plans)
                await group.ExecuteAsync(plan, cts.Token);
        }
        finally
        {
            _applyCts = null;
            IsApplying = false;
        }
    }

    public void LoadJob(ScanJob job)
    {
        _currentJob = job;
        ScanName = job.Name;
        IsScanning = false;
        HasScanError = false;
        ErrorMessage = string.Empty;
        HasSkippedFiles = false;
        HasMergedAliases = false;
        Groups.Clear();
        SelectedFile = null;
        PreviewBitmap = null;
        HasSelectedFile = false;

        if (job.Result is { } result)
            LoadResults(result);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }

    private static string FolderDisplayName(string path)
    {
        if (string.IsNullOrEmpty(path)) return "미설정";
        var trimmed = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(trimmed);
        return string.IsNullOrEmpty(name) ? trimmed : name;
    }
}
