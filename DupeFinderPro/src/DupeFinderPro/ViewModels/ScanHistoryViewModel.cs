using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels;

public sealed partial class ScanHistoryViewModel : ViewModelBase
{
    private readonly ScanJobService _scanJobService;

    public event Action<ScanJob>? ViewResultsRequested;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatusFilter = "All";
    [ObservableProperty] private ScanHistoryItemViewModel? _selectedItem;

    public ObservableCollection<ScanHistoryItemViewModel> Items { get; } = [];

    public static IReadOnlyList<string> StatusFilters { get; } =
        ["All", "Completed", "Running", "Cancelled", "Failed"];

    public ScanHistoryViewModel(ScanJobService scanJobService)
    {
        _scanJobService = scanJobService;
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();
    partial void OnSelectedStatusFilterChanged(string value) => ApplyFilter();

    public void Refresh()
    {
        var jobs = _scanJobService.GetAllJobs()
            .OrderByDescending(j => j.CreatedAt);

        Items.Clear();
        foreach (var job in jobs)
            Items.Add(new ScanHistoryItemViewModel(job));

        ApplyFilter();
    }

    private void ApplyFilter()
    {
        var jobs = _scanJobService.GetAllJobs()
            .OrderByDescending(j => j.CreatedAt);

        Items.Clear();
        foreach (var job in jobs)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !job.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            if (SelectedStatusFilter != "All" &&
                !job.Status.ToString().Equals(SelectedStatusFilter, StringComparison.OrdinalIgnoreCase))
                continue;

            Items.Add(new ScanHistoryItemViewModel(job));
        }
    }

    [RelayCommand]
    private void ViewResults(ScanHistoryItemViewModel? item)
    {
        if (item is not null)
            ViewResultsRequested?.Invoke(item.Job);
    }
}

public sealed class ScanHistoryItemViewModel(ScanJob job)
{
    public ScanJob Job => job;

    public string Name      => job.Name;
    public string Status    => job.Status.ToString();
    public string CreatedAt => job.CreatedAt.ToString("g");
    public string Paths     => job.PathsSummary;
    public string FileTypes => job.FileTypesSummary;

    public string ResultSummary => job.Result is { } r
        ? $"{r.DuplicateGroups.Count} duplicate groups · {FormatBytes(r.TotalWastedBytes)} wasted · {r.FilesScanned} files scanned"
        : job.ErrorMessage ?? string.Empty;

    public string Duration => job.Result is { } r
        ? $"{r.ElapsedTime.TotalSeconds:F1}s"
        : string.Empty;

    public bool IsCompleted => job.Status == ScanJobStatus.Completed;
    public bool IsFailed    => job.Status == ScanJobStatus.Failed;
    public bool IsCancelled => job.Status == ScanJobStatus.Cancelled;
    public bool IsRunning   => job.Status == ScanJobStatus.Running;

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
