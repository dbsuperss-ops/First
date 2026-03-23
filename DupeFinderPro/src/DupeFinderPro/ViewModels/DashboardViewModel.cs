using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;
using System.IO;

namespace DupeFinderPro.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ScanJobService _scanJobService;
    private readonly IFileOperationService _fileOps;
    private readonly IScenarioRepository _scenarioRepo;
    private readonly IClassifyRecordRepository _classifyRepo;

    // ── 파일 분류 통계 ──────────────────────────────────────────────────
    [ObservableProperty] private int    _totalScenariosCount;
    [ObservableProperty] private int    _activeScenarioCount;
    [ObservableProperty] private int    _totalFilesOrganized;
    [ObservableProperty] private string _totalBytesOrganizedText = "0 B";
    [ObservableProperty] private string _lastOrganizeTime = "없음";

    // ── 중복 파일 스캔 통계 ──────────────────────────────────────────────
    [ObservableProperty] private int    _totalDuplicatesFound;
    [ObservableProperty] private long   _totalWastedBytes;
    [ObservableProperty] private int    _totalScansRun;
    [ObservableProperty] private string _lastScanTime = "없음";
    [ObservableProperty] private bool   _hasRecentScan;
    [ObservableProperty] private string _recentScanName = string.Empty;
    [ObservableProperty] private string _recentScanStatus = string.Empty;
    [ObservableProperty] private string _recentScanSummary = string.Empty;

    // ── 빈 폴더 삭제 ─────────────────────────────────────────────────
    [ObservableProperty] private string _emptyFolderRoot = string.Empty;
    [ObservableProperty] private bool   _isDeletingEmptyFolders;
    [ObservableProperty] private string _emptyFolderResult = string.Empty;
    [ObservableProperty] private bool   _hasEmptyFolderResult;

    public ObservableCollection<ScanJobSummaryViewModel> RecentJobs { get; } = [];

    public string TotalWastedFormatted => FormatBytes(TotalWastedBytes);

    // ── 탐색 이벤트 ────────────────────────────────────────────────────
    public event Action? NavigateToScenarios;
    public event Action? NavigateToOrganize;
    public event Action? NavigateToNewScan;
    public event Action? NavigateToHistory;
    public event Action? NavigateToResults;

    public DashboardViewModel(
        ScanJobService scanJobService,
        IFileOperationService fileOps,
        IScenarioRepository scenarioRepo,
        IClassifyRecordRepository classifyRepo)
    {
        _scanJobService = scanJobService;
        _fileOps = fileOps;
        _scenarioRepo = scenarioRepo;
        _classifyRepo = classifyRepo;
    }

    [RelayCommand] private void GoToScenarios() => NavigateToScenarios?.Invoke();
    [RelayCommand] private void GoToOrganize()  => NavigateToOrganize?.Invoke();
    [RelayCommand] private void StartNewScan()  => NavigateToNewScan?.Invoke();
    [RelayCommand] private void ViewHistory()   => NavigateToHistory?.Invoke();
    [RelayCommand] private void ViewResults()   => NavigateToResults?.Invoke();

    [RelayCommand]
    private async Task DeleteEmptyFolders()
    {
        var root = EmptyFolderRoot.Trim();
        if (string.IsNullOrEmpty(root) || !Directory.Exists(root))
        {
            EmptyFolderResult = "유효한 폴더 경로를 입력하세요.";
            HasEmptyFolderResult = true;
            return;
        }

        IsDeletingEmptyFolders = true;
        HasEmptyFolderResult = false;
        try
        {
            var count = await _fileOps.DeleteEmptyFoldersAsync(root);
            EmptyFolderResult = count == 0 ? "빈 폴더가 없습니다." : $"{count}개의 빈 폴더를 삭제했습니다.";
        }
        catch (Exception ex)
        {
            EmptyFolderResult = $"오류: {ex.Message}";
        }
        finally
        {
            IsDeletingEmptyFolders = false;
            HasEmptyFolderResult = true;
        }
    }

    public void Refresh()
    {
        RefreshClassifyStats();
        RefreshScanStats();
    }

    private void RefreshClassifyStats()
    {
        var scenarios = _scenarioRepo.GetAll();
        TotalScenariosCount = scenarios.Count;
        ActiveScenarioCount = scenarios.Count(s => s.IsActive);

        var records = _classifyRepo.GetAll();
        TotalFilesOrganized = records.Sum(r => r.FileCount);
        TotalBytesOrganizedText = FormatBytes(records.Sum(r => r.TotalBytes));
        LastOrganizeTime = records.Count > 0
            ? records.Max(r => r.ExecutedAt).ToString("g")
            : "없음";
    }

    private void RefreshScanStats()
    {
        var jobs = _scanJobService.GetAllJobs();
        TotalScansRun = jobs.Count;

        var completed = jobs.Where(j => j.Status == ScanJobStatus.Completed).ToList();
        TotalDuplicatesFound = completed.Sum(j => j.Result?.DuplicateGroups.Count ?? 0);
        TotalWastedBytes = completed.Sum(j => j.Result?.TotalWastedBytes ?? 0);
        OnPropertyChanged(nameof(TotalWastedFormatted));

        var latest = _scanJobService.GetLatestCompleted();
        if (latest is not null)
        {
            HasRecentScan = true;
            RecentScanName = latest.Name;
            RecentScanStatus = latest.Status switch
            {
                ScanJobStatus.Completed => "완료",
                ScanJobStatus.Running   => "실행 중",
                ScanJobStatus.Cancelled => "취소됨",
                ScanJobStatus.Failed    => "실패",
                _                       => latest.Status.ToString()
            };
            LastScanTime = latest.CreatedAt.ToString("g");
            RecentScanSummary = latest.Result is { } r
                ? $"{r.DuplicateGroups.Count}개 그룹 · {FormatBytes(r.TotalWastedBytes)} 낭비"
                : string.Empty;
        }
        else
        {
            HasRecentScan = false;
            LastScanTime = "없음";
        }

        RecentJobs.Clear();
        foreach (var job in jobs.OrderByDescending(j => j.CreatedAt).Take(5))
            RecentJobs.Add(new ScanJobSummaryViewModel(job));
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}

public sealed class ScanJobSummaryViewModel(ScanJob job)
{
    public string Name       => job.Name;
    public string Status     => job.Status switch
    {
        ScanJobStatus.Completed => "완료",
        ScanJobStatus.Running   => "실행 중",
        ScanJobStatus.Cancelled => "취소됨",
        ScanJobStatus.Failed    => "실패",
        _                       => job.Status.ToString()
    };
    public string CreatedAt  => job.CreatedAt.ToString("g");
    public string Summary    => job.Result is { } r
        ? $"{r.DuplicateGroups.Count}개 그룹 · {FormatBytes(r.TotalWastedBytes)} 낭비"
        : job.ErrorMessage ?? string.Empty;
    public bool IsCompleted  => job.Status == ScanJobStatus.Completed;
    public bool IsFailed     => job.Status == ScanJobStatus.Failed;
    public bool IsCancelled  => job.Status == ScanJobStatus.Cancelled;
    public bool IsRunning    => job.Status == ScanJobStatus.Running;

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
