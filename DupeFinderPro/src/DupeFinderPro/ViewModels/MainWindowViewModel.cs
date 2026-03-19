using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace DupeFinderPro.ViewModels;

public enum AppPage { Dashboard, NewScan, ScanHistory, Results }

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly DashboardViewModel _dashboardVm;
    private readonly NewScanViewModel _newScanVm;
    private readonly ScanHistoryViewModel _historyVm;
    private readonly ResultsViewModel _resultsVm;

    [ObservableProperty]
    private AppPage _activePage = AppPage.Dashboard;

    [ObservableProperty]
    private ViewModelBase _currentView;

    public MainWindowViewModel(
        DashboardViewModel dashboardVm,
        NewScanViewModel newScanVm,
        ScanHistoryViewModel historyVm,
        ResultsViewModel resultsVm)
    {
        _dashboardVm = dashboardVm;
        _newScanVm = newScanVm;
        _historyVm = historyVm;
        _resultsVm = resultsVm;
        _currentView = dashboardVm;

        // Wire up cross-VM navigation
        _newScanVm.ScanStarted += OnScanStarted;
        _dashboardVm.NavigateToNewScan += () => NavigateTo(AppPage.NewScan);
        _dashboardVm.NavigateToHistory += () => NavigateTo(AppPage.ScanHistory);
        _dashboardVm.NavigateToResults += () => NavigateTo(AppPage.Results);
    }

    public bool IsDashboardActive => ActivePage == AppPage.Dashboard;
    public bool IsNewScanActive   => ActivePage == AppPage.NewScan;
    public bool IsHistoryActive   => ActivePage == AppPage.ScanHistory;
    public bool IsResultsActive   => ActivePage == AppPage.Results;

    [RelayCommand]
    private void NavigateToDashboard() => NavigateTo(AppPage.Dashboard);

    [RelayCommand]
    private void NavigateToNewScan() => NavigateTo(AppPage.NewScan);

    [RelayCommand]
    private void NavigateToHistory() => NavigateTo(AppPage.ScanHistory);

    [RelayCommand]
    private void NavigateToResults() => NavigateTo(AppPage.Results);

    private void NavigateTo(AppPage page)
    {
        ActivePage = page;
        CurrentView = page switch
        {
            AppPage.Dashboard   => _dashboardVm,
            AppPage.NewScan     => _newScanVm,
            AppPage.ScanHistory => _historyVm,
            AppPage.Results     => _resultsVm,
            _                   => _dashboardVm
        };
        OnPropertyChanged(nameof(IsDashboardActive));
        OnPropertyChanged(nameof(IsNewScanActive));
        OnPropertyChanged(nameof(IsHistoryActive));
        OnPropertyChanged(nameof(IsResultsActive));
    }

    private void OnScanStarted()
    {
        NavigateTo(AppPage.Results);
    }
}
