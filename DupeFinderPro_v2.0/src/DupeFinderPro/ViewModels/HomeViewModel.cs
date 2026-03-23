using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;

namespace DupeFinderPro.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
{
    private readonly HomeStatsService _statsService;

    // ── Navigation events wired by MainWindowViewModel ───────────────────
    public event Action? NavigateToScenarios;
    public event Action? NavigateToOrganize;
    public event Action? NavigateToDuplicateScan;
    public event Action? NavigateToDuplicateResults;

    // ── Organize stats ───────────────────────────────────────────────────
    [ObservableProperty] private int    _activeScenarioCount;
    [ObservableProperty] private int    _totalScenariosCount;
    [ObservableProperty] private string _totalFilesOrganized  = "0";
    [ObservableProperty] private string _totalBytesOrganized  = "0 B";
    [ObservableProperty] private string _lastOrganizeTime     = "없음";

    // ── Duplicate stats ──────────────────────────────────────────────────
    [ObservableProperty] private string _totalDuplicatesFound = "0";
    [ObservableProperty] private string _totalWastedBytes     = "0 B";
    [ObservableProperty] private int    _totalScansRun;
    [ObservableProperty] private string _lastScanTime         = "없음";

    public HomeViewModel(HomeStatsService statsService)
    {
        _statsService = statsService;
        Refresh();
    }

    public void Refresh()
    {
        var s = _statsService.GetStats();

        ActiveScenarioCount  = s.ActiveScenarioCount;
        TotalScenariosCount  = s.TotalScenariosCount;
        TotalFilesOrganized  = s.TotalFilesOrganized.ToString("N0");
        TotalBytesOrganized  = FormatBytes(s.TotalBytesOrganized);
        LastOrganizeTime     = s.LastOrganizeTime;

        TotalDuplicatesFound = s.TotalDuplicatesFound.ToString("N0");
        TotalWastedBytes     = FormatBytes(s.TotalWastedBytes);
        TotalScansRun        = s.TotalScansRun;
        LastScanTime         = s.LastScanTime;
    }

    // ── Commands ─────────────────────────────────────────────────────────
    [RelayCommand] private void GoToScenarios()        => NavigateToScenarios?.Invoke();
    [RelayCommand] private void GoToOrganize()         => NavigateToOrganize?.Invoke();
    [RelayCommand] private void GoToDuplicateScan()    => NavigateToDuplicateScan?.Invoke();
    [RelayCommand] private void GoToDuplicateResults() => NavigateToDuplicateResults?.Invoke();

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
