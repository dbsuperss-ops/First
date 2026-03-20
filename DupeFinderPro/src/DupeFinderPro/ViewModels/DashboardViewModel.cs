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

    public event Action? NavigateToNewScan;
    public event Action? NavigateToHistory;
    public event Action? NavigateToResults;

    [ObservableProperty] private int _totalDuplicatesFound;
    [ObservableProperty] private long _totalWastedBytes;
    [ObservableProperty] private int _totalScansRun;
    [ObservableProperty] private string _lastScanTime = "Never";
    [ObservableProperty] private bool _hasRecentScan;
    [ObservableProperty] private string _recentScanName = string.Empty;
    [ObservableProperty] private string _recentScanStatus = string.Empty;
    [ObservableProperty] private string _recentScanSummary = string.Empty;

    // ── 빈 폴더 삭제 ─────────────────────────────────────────────────
    [ObservableProperty] private string _emptyFolderRoot = string.Empty;
    [ObservableProperty] private bool _isDeletingEmptyFolders;
    [ObservableProperty] private string _emptyFolderResult = string.Empty;
    [ObservableProperty] private bool _hasEmptyFolderResult;

    public ObservableCollection<ScanJobSummaryViewModel> RecentJobs { get; } = [];

    public string TotalWastedFormatted => FormatBytes(TotalWastedBytes);

    public DashboardViewModel(ScanJobService scanJobService, IFileOperationService fileOps)
    {
        _scanJobService = scanJobService;
        _fileOps = fileOps;
    }

    [RelayCommand]
    private void StartNewScan() => NavigateToNewScan?.Invoke();

    [RelayCommand]
    private void ViewHistory() => NavigateToHistory?.Invoke();

    [RelayCommand]
    private void ViewResults() => NavigateToResults?.Invoke();

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
            RecentScanStatus = latest.Status.ToString();
            LastScanTime = latest.CreatedAt.ToString("g");
            RecentScanSummary = latest.Result is { } r
                ? $"{r.DuplicateGroups.Count} groups · {FormatBytes(r.TotalWastedBytes)} wasted"
                : string.Empty;
        }
        else
        {
            HasRecentScan = false;
            LastScanTime = "Never";
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
    public string Status     => job.Status.ToString();
    public string CreatedAt  => job.CreatedAt.ToString("g");
    public string Summary    => job.Result is { } r
        ? $"{r.DuplicateGroups.Count} groups · {FormatBytes(r.TotalWastedBytes)} wasted"
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
