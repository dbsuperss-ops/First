using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.ViewModels.Duplicate;
using DupeFinderPro.ViewModels.Organize;

namespace DupeFinderPro.ViewModels;

public enum AppPage
{
    // 공통
    Home,

    // 파일 분류 (핵심)
    ScenarioList,
    ScenarioEdit,
    OrganizeRun,
    OrganizeLog,
    OrganizeStats,

    // 중복 탐지 (보조)
    DuplicateScan,
    DuplicateResults,
    DuplicateHistory,
}

public sealed partial class MainWindowViewModel : ViewModelBase
{
    private readonly HomeViewModel              _homeVm;
    private readonly ScenarioListViewModel      _scenarioListVm;
    private readonly ScenarioEditViewModel      _scenarioEditVm;
    private readonly OrganizeRunViewModel       _organizeRunVm;
    private readonly OrganizeLogViewModel       _organizeLogVm;
    private readonly OrganizeStatisticsViewModel _organizeStatsVm;
    private readonly DuplicateScanViewModel     _duplicateScanVm;
    private readonly ResultsViewModel           _resultsVm;
    private readonly ScanHistoryViewModel       _historyVm;

    [ObservableProperty] private AppPage      _activePage = AppPage.Home;
    [ObservableProperty] private ViewModelBase _currentView;

    public MainWindowViewModel(
        HomeViewModel              homeVm,
        ScenarioListViewModel      scenarioListVm,
        ScenarioEditViewModel      scenarioEditVm,
        OrganizeRunViewModel       organizeRunVm,
        OrganizeLogViewModel       organizeLogVm,
        OrganizeStatisticsViewModel organizeStatsVm,
        DuplicateScanViewModel     duplicateScanVm,
        ResultsViewModel           resultsVm,
        ScanHistoryViewModel       historyVm)
    {
        _homeVm          = homeVm;
        _scenarioListVm  = scenarioListVm;
        _scenarioEditVm  = scenarioEditVm;
        _organizeRunVm   = organizeRunVm;
        _organizeLogVm   = organizeLogVm;
        _organizeStatsVm = organizeStatsVm;
        _duplicateScanVm = duplicateScanVm;
        _resultsVm       = resultsVm;
        _historyVm       = historyVm;
        _currentView     = homeVm;

        // Home → 파일 분류 네비게이션
        _homeVm.NavigateToScenarios += () => NavigateTo(AppPage.ScenarioList);
        _homeVm.NavigateToOrganize  += () => { _organizeRunVm.Refresh(); NavigateTo(AppPage.OrganizeRun); };

        // Home → 중복 탐지 네비게이션
        _homeVm.NavigateToDuplicateScan    += () => NavigateTo(AppPage.DuplicateScan);
        _homeVm.NavigateToDuplicateResults += () => NavigateTo(AppPage.DuplicateResults);

        // 시나리오 편집 완료 시 목록으로 복귀
        _scenarioEditVm.SaveCompleted += () => { _scenarioListVm.Refresh(); NavigateTo(AppPage.ScenarioList); };
        _scenarioEditVm.Cancelled     += () => NavigateTo(AppPage.ScenarioList);

        // 시나리오 목록 → 편집 진입
        _scenarioListVm.NavigateToEdit += scenario =>
        {
            _scenarioEditVm.Load(scenario);
            NavigateTo(AppPage.ScenarioEdit);
        };
        _scenarioListVm.NavigateToCreate += () =>
        {
            _scenarioEditVm.LoadNew();
            NavigateTo(AppPage.ScenarioEdit);
        };
        _scenarioListVm.NavigateToOrganize += () =>
        {
            _organizeRunVm.Refresh();
            NavigateTo(AppPage.OrganizeRun);
        };

        // 중복 스캔 시작 시 결과 화면으로 이동
        _duplicateScanVm.ScanStarted += () => NavigateTo(AppPage.DuplicateResults);
    }

    // ── 파일 분류 활성 상태 ─────────────────────────────────────────────
    public bool IsHomeActive         => ActivePage == AppPage.Home;
    public bool IsScenarioListActive => ActivePage == AppPage.ScenarioList;
    public bool IsScenarioEditActive => ActivePage == AppPage.ScenarioEdit;
    public bool IsOrganizeRunActive  => ActivePage == AppPage.OrganizeRun;
    public bool IsOrganizeLogActive  => ActivePage == AppPage.OrganizeLog;
    public bool IsOrganizeStatsActive => ActivePage == AppPage.OrganizeStats;

    // ── 중복 탐지 활성 상태 ─────────────────────────────────────────────
    public bool IsDuplicateScanActive    => ActivePage == AppPage.DuplicateScan;
    public bool IsDuplicateResultsActive => ActivePage == AppPage.DuplicateResults;
    public bool IsDuplicateHistoryActive => ActivePage == AppPage.DuplicateHistory;

    // ── 파일 분류 네비게이션 ────────────────────────────────────────────
    [RelayCommand] private void NavigateToHome() => NavigateTo(AppPage.Home);

    [RelayCommand]
    private void NavigateToScenarioList() => NavigateTo(AppPage.ScenarioList);

    [RelayCommand]
    private void NavigateToOrganizeRun()
    {
        _organizeRunVm.Refresh();
        NavigateTo(AppPage.OrganizeRun);
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

    // ── 중복 탐지 네비게이션 ────────────────────────────────────────────
    [RelayCommand] private void NavigateToDuplicateScan()    => NavigateTo(AppPage.DuplicateScan);
    [RelayCommand] private void NavigateToDuplicateResults() => NavigateTo(AppPage.DuplicateResults);

    [RelayCommand]
    private void NavigateToDuplicateHistory()
    {
        _historyVm.Refresh();
        NavigateTo(AppPage.DuplicateHistory);
    }

    private void NavigateTo(AppPage page)
    {
        ActivePage   = page;
        CurrentView  = page switch
        {
            AppPage.Home           => _homeVm,
            AppPage.ScenarioList   => _scenarioListVm,
            AppPage.ScenarioEdit   => _scenarioEditVm,
            AppPage.OrganizeRun    => _organizeRunVm,
            AppPage.OrganizeLog    => _organizeLogVm,
            AppPage.OrganizeStats  => _organizeStatsVm,
            AppPage.DuplicateScan  => _duplicateScanVm,
            AppPage.DuplicateResults => _resultsVm,
            AppPage.DuplicateHistory => _historyVm,
            _                      => _homeVm
        };

        OnPropertyChanged(nameof(IsHomeActive));
        OnPropertyChanged(nameof(IsScenarioListActive));
        OnPropertyChanged(nameof(IsScenarioEditActive));
        OnPropertyChanged(nameof(IsOrganizeRunActive));
        OnPropertyChanged(nameof(IsOrganizeLogActive));
        OnPropertyChanged(nameof(IsOrganizeStatsActive));
        OnPropertyChanged(nameof(IsDuplicateScanActive));
        OnPropertyChanged(nameof(IsDuplicateResultsActive));
        OnPropertyChanged(nameof(IsDuplicateHistoryActive));
    }
}
