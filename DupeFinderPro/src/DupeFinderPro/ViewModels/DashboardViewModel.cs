using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels;

public sealed partial class DashboardViewModel : ViewModelBase
{
    private readonly ScanJobService _scanJobService;

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

    public ObservableCollection<ScanJobSummaryViewModel> RecentJobs { get; } = [];

    public string TotalWastedFormatted => FormatBytes(TotalWastedBytes);

    public DashboardViewModel(ScanJobService scanJobService)
    {
        _scanJobService = scanJobService;
    }

    [RelayCommand]
    private void StartNewScan() => NavigateToNewScan?.Invoke();

    [RelayCommand]
    private void ViewHistory() => NavigateToHistory?.Invoke();

    [RelayCommand]
    private void ViewResults() => NavigateToResults?.Invoke();

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
