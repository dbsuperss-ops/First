using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels.Duplicate;

public sealed partial class ScanHistoryViewModel : ViewModelBase
{
    private readonly ScanJobService _scanJobService;

    public event Action<ScanJob>? ViewResultsRequested;

    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _selectedStatusFilter = "전체";
    [ObservableProperty] private ScanHistoryItemViewModel? _selectedItem;

    public ObservableCollection<ScanHistoryItemViewModel> Items { get; } = [];

    public static IReadOnlyList<string> StatusFilters { get; } =
        ["전체", "완료", "실행 중", "취소됨", "실패"];

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

    private static ScanJobStatus? ParseStatusFilter(string filter) => filter switch
    {
        "완료"   => ScanJobStatus.Completed,
        "실행 중" => ScanJobStatus.Running,
        "취소됨"  => ScanJobStatus.Cancelled,
        "실패"   => ScanJobStatus.Failed,
        _        => null
    };

    private void ApplyFilter()
    {
        var jobs = _scanJobService.GetAllJobs()
            .OrderByDescending(j => j.CreatedAt);

        var statusFilter = ParseStatusFilter(SelectedStatusFilter);

        Items.Clear();
        foreach (var job in jobs)
        {
            if (!string.IsNullOrWhiteSpace(SearchText) &&
                !job.Name.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                continue;

            if (statusFilter.HasValue && job.Status != statusFilter.Value)
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
    public string Status => job.Status switch
    {
        ScanJobStatus.Completed => "완료",
        ScanJobStatus.Running   => "실행 중",
        ScanJobStatus.Cancelled => "취소됨",
        ScanJobStatus.Failed    => "실패",
        _                       => job.Status.ToString()
    };
    public string CreatedAt => job.CreatedAt.ToString("g");
    public string Paths     => job.PathsSummary;
    public string FileTypes => job.FileTypesSummary;

    public string ResultSummary => job.Result is { } r
        ? $"중복 그룹 {r.DuplicateGroups.Count}개 · 낭비 {FormatBytes(r.TotalWastedBytes)} · {r.FilesScanned}개 파일 스캔"
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
