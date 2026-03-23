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

    // ── Cleanup options ────────────────────────────────────────────────────
    [ObservableProperty] private string _quarantinePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DupeFinderPro", "Quarantine");
    [ObservableProperty] private string _moveToPath = string.Empty;
    [ObservableProperty] private bool   _isApplying;

    // ── Groups ────────────────────────────────────────────────────────────
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
        Groups.Clear();
        PhaseLabel = "준비 중…";
        CurrentFile = string.Empty;
        ProgressValue = 0;
        IsIndeterminate = true;

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
        {
            LoadResults(result);
        }
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

        Groups.Clear();
        foreach (var g in result.DuplicateGroups.OrderByDescending(g => g.WastedBytes))
            Groups.Add(new DuplicateGroupViewModel(g, _cleanup));

        HasResults = TotalGroups > 0;
        IsEmpty    = TotalGroups == 0;
    }

    [RelayCommand]
    private void CancelScan()
    {
        _cts?.Cancel();
    }

    [RelayCommand]
    private void AutoSelectAll()
    {
        foreach (var g in Groups)
            g.AutoSelectCommand.Execute(null);
    }

    // ── 일괄 작업 설정 ─────────────────────────────────────────────────
    /// <summary>Keep 파일을 제외한 모든 파일을 삭제로 설정합니다.</summary>
    [RelayCommand]
    private void SetAllDelete()
    {
        foreach (var g in Groups)
            foreach (var f in g.Files)
                if (!f.IsDone && f.SelectedAction != FileAction.Keep)
                    f.SelectedAction = FileAction.Delete;
    }

    /// <summary>Keep 파일을 제외한 모든 파일을 격리로 설정합니다.</summary>
    [RelayCommand]
    private void SetAllQuarantine()
    {
        foreach (var g in Groups)
            foreach (var f in g.Files)
                if (!f.IsDone && f.SelectedAction != FileAction.Keep)
                    f.SelectedAction = FileAction.Quarantine;
    }

    /// <summary>Keep 파일을 제외한 모든 파일을 이동으로 설정합니다.</summary>
    [RelayCommand]
    private void SetAllMove()
    {
        foreach (var g in Groups)
            foreach (var f in g.Files)
                if (!f.IsDone && f.SelectedAction != FileAction.Keep)
                    f.SelectedAction = FileAction.MoveToFolder;
    }

    [RelayCommand]
    private async Task ApplyAllAsync()
    {
        if (IsApplying) return;
        IsApplying = true;
        try
        {
            using var cts = new CancellationTokenSource();
            foreach (var g in Groups)
                await g.ApplyAsync(QuarantinePath, MoveToPath, cts.Token);
        }
        finally
        {
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
        Groups.Clear();

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
}
