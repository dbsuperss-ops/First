using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class OrganizeStatisticsViewModel : ViewModelBase
{
    private readonly IClassifyRecordRepository _repo;

    public ObservableCollection<StatRecordViewModel> Records { get; } = [];

    [ObservableProperty] private int _totalExecutions;
    [ObservableProperty] private int _totalFilesMoved;
    [ObservableProperty] private string _totalBytesMovedText = "0 B";
    [ObservableProperty] private string _lastExecutedAt = "없음";

    public OrganizeStatisticsViewModel(IClassifyRecordRepository repo)
    {
        _repo = repo;
        LoadStats();
    }

    public void Refresh() => LoadStats();

    [RelayCommand]
    private void ClearHistory()
    {
        _repo.Clear();
        LoadStats();
    }

    private void LoadStats()
    {
        Records.Clear();
        var all = _repo.GetAll();

        TotalExecutions = all.Count;
        TotalFilesMoved = all.Sum(r => r.FileCount);
        TotalBytesMovedText = FormatSize(all.Sum(r => r.TotalBytes));
        LastExecutedAt = all.Count > 0
            ? all.Max(r => r.ExecutedAt).ToString("yyyy-MM-dd HH:mm")
            : "없음";

        foreach (var rec in all)
            Records.Add(new StatRecordViewModel(rec));
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824.0:F1} GB" :
        bytes >= 1_048_576     ? $"{bytes / 1_048_576.0:F1} MB"     :
        bytes >= 1_024         ? $"{bytes / 1_024.0:F1} KB"         :
                                 $"{bytes} B";
}

public sealed class StatRecordViewModel : ViewModelBase
{
    public string ScenarioName { get; }
    public string ExecutedAt { get; }
    public int FileCount { get; }
    public string TotalBytesText { get; }

    public StatRecordViewModel(DupeFinderPro.Domain.Models.Organize.ClassifyRecord record)
    {
        ScenarioName = record.ScenarioName;
        ExecutedAt = record.ExecutedAt.ToString("yyyy-MM-dd HH:mm");
        FileCount = record.FileCount;
        TotalBytesText = FormatSize(record.TotalBytes);
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824.0:F1} GB" :
        bytes >= 1_048_576     ? $"{bytes / 1_048_576.0:F1} MB"     :
        bytes >= 1_024         ? $"{bytes / 1_024.0:F1} KB"         :
                                 $"{bytes} B";
}
