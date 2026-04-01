using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class ScenarioListViewModel : ViewModelBase
{
    private readonly IScenarioRepository _repo;
    private readonly IWatcherService     _watcher;
    private readonly ISchedulerService   _scheduler;

    // ── Navigation events wired by MainWindowViewModel ───────────────────
    public event Action<Scenario>? NavigateToEdit;
    public event Action?           NavigateToCreate;
    public event Action?           NavigateToOrganize;

    public ObservableCollection<ScenarioItemViewModel> Scenarios { get; } = [];

    public ScenarioListViewModel(
        IScenarioRepository repo,
        IWatcherService     watcher,
        ISchedulerService   scheduler)
    {
        _repo      = repo;
        _watcher   = watcher;
        _scheduler = scheduler;
        LoadScenarios();
    }

    public void Refresh() => LoadScenarios();

    [RelayCommand]
    private void NewScenario() => NavigateToCreate?.Invoke();

    [RelayCommand]
    private void EditScenario(ScenarioItemViewModel item)
    {
        var scenario = _repo.GetById(item.Id);
        if (scenario is not null) NavigateToEdit?.Invoke(scenario);
    }

    [RelayCommand]
    private void RunOrganize() => NavigateToOrganize?.Invoke();

    [RelayCommand]
    private void DeleteScenario(ScenarioItemViewModel item)
    {
        var all = _repo.GetAll().Where(s => s.Id != item.Id).ToList();
        _repo.Save(all);
        _watcher.Stop(item.Id);
        _scheduler.DeleteTask(item.ScenarioName);
        LoadScenarios();
    }

    [RelayCommand]
    private void ToggleWatch(ScenarioItemViewModel item)
    {
        var scenario = _repo.GetById(item.Id);
        if (scenario is null) return;

        if (_watcher.IsWatching(item.Id))
            _watcher.Stop(item.Id);
        else
            _watcher.Start(scenario);

        LoadScenarios();
    }

    [RelayCommand]
    private void ToggleSchedule(ScenarioItemViewModel item)
    {
        var scenario = _repo.GetById(item.Id);
        if (scenario is null) return;

        var updated = scenario with { IsScheduled = !scenario.IsScheduled };
        var all     = _repo.GetAll().Select(s => s.Id == updated.Id ? updated : s).ToList();
        _repo.Save(all);

        if (updated.IsScheduled)
            _scheduler.RegisterTask(updated);
        else
            _scheduler.DeleteTask(updated.Name);

        LoadScenarios();
    }

    private void LoadScenarios()
    {
        Scenarios.Clear();
        foreach (var s in _repo.GetAll())
            Scenarios.Add(new ScenarioItemViewModel(s, _watcher.IsWatching(s.Id)));
    }
}
