using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.ViewModels.Organize;

namespace DupeFinderPro.ViewModels;

public enum AppPage { Dashboard, NewScan, ScanHistory, Results, ScenarioList, Organize, OrganizeLog, OrganizeStats }

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboardVm;
    private readonly NewScanViewModel _newScanVm;
    private readonly ScanHistoryViewModel _historyVm;
    private readonly ResultsViewModel _resultsVm;
    private readonly ScenarioListViewModel _scenarioListVm;
    private readonly OrganizeViewModel _organizeVm;
    private readonly OrganizeLogViewModel _organizeLogVm;
    private readonly OrganizeStatisticsViewModel _organizeStatsVm;

    [ObservableProperty]
    private AppPage _activePage = AppPage.Dashboard;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel(
        DashboardViewModel dashboardVm,
        NewScanViewModel newScanVm,
        ScanHistoryViewModel historyVm,
        ResultsViewModel resultsVm,
        ScenarioListViewModel scenarioListVm,
        OrganizeViewModel organizeVm,
        OrganizeLogViewModel organizeLogVm,
        OrganizeStatisticsViewModel organizeStatsVm)
    {
        _dashboardVm = dashboardVm;
        _newScanVm = newScanVm;
        _historyVm = historyVm;
        _resultsVm = resultsVm;
        _scenarioListVm = scenarioListVm;
        _organizeVm = organizeVm;
        _organizeLogVm = organizeLogVm;
        _organizeStatsVm = organizeStatsVm;
        _currentView = dashboardVm;

        // Wire up cross-VM navigation
        _newScanVm.ScanStarted += OnScanStarted;
        _dashboardVm.NavigateToNewScan   += () => NavigateTo(AppPage.NewScan);
        _dashboardVm.NavigateToHistory   += () => NavigateTo(AppPage.ScanHistory);
        _dashboardVm.NavigateToResults   += () => NavigateTo(AppPage.Results);
        _dashboardVm.NavigateToScenarios += () => NavigateTo(AppPage.ScenarioList);
        _dashboardVm.NavigateToOrganize  += () =>
        {
            _organizeVm.Refresh();
            NavigateTo(AppPage.Organize);
        };
    }

    public bool IsDashboardActive    => ActivePage == AppPage.Dashboard;
    public bool IsNewScanActive      => ActivePage == AppPage.NewScan;
    public bool IsHistoryActive      => ActivePage == AppPage.ScanHistory;
    public bool IsResultsActive      => ActivePage == AppPage.Results;
    public bool IsScenarioListActive => ActivePage == AppPage.ScenarioList;
    public bool IsOrganizeActive     => ActivePage == AppPage.Organize;
    public bool IsOrganizeLogActive  => ActivePage == AppPage.OrganizeLog;
    public bool IsOrganizeStatsActive => ActivePage == AppPage.OrganizeStats;

    [RelayCommand]
    private void NavigateToDashboard() => NavigateTo(AppPage.Dashboard);

    [RelayCommand]
    private void NavigateToNewScan() => NavigateTo(AppPage.NewScan);

    [RelayCommand]
    private void NavigateToHistory() => NavigateTo(AppPage.ScanHistory);

    [RelayCommand]
    private void NavigateToResults() => NavigateTo(AppPage.Results);

    [RelayCommand]
    private void NavigateToScenarioList() => NavigateTo(AppPage.ScenarioList);

    [RelayCommand]
    private void NavigateToOrganize()
    {
        _organizeVm.Refresh();
        NavigateTo(AppPage.Organize);
    }

    [RelayCommand]
    private void NavigateToOrganizeLog()
    {
        _organizeLogVm.Refresh();
        NavigateTo(AppPage.OrganizeLog);
    }

    [RelayCommand]
    private void NavigateToOrganizeStats()
    {
        _organizeStatsVm.Refresh();
        NavigateTo(AppPage.OrganizeStats);
    }

    private void NavigateTo(AppPage page)
    {
        ActivePage = page;
        CurrentView = page switch
        {
            AppPage.Dashboard    => _dashboardVm,
            AppPage.NewScan      => _newScanVm,
            AppPage.ScanHistory  => _historyVm,
            AppPage.Results      => _resultsVm,
            AppPage.ScenarioList => _scenarioListVm,
            AppPage.Organize     => _organizeVm,
            AppPage.OrganizeLog  => _organizeLogVm,
            AppPage.OrganizeStats => _organizeStatsVm,
            _                    => _dashboardVm
        };
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsNewScanActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsResultsActive));
        OnPropertyChanged(nameof(IsScenarioListActive));
        OnPropertyChanged(nameof(IsOrganizeActive));
        OnPropertyChanged(nameof(IsOrganizeLogActive));
        OnPropertyChanged(nameof(IsOrganizeStatsActive));
    }

    private void OnScanStarted()
    {
        NavigateTo(AppPage.Results);
    }
}
