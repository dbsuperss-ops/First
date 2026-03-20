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
    private readonly IWatcherService _watcher;
    private readonly ISchedulerService _scheduler;
    private readonly ScenarioEditViewModel _editVm;

    public ObservableCollection<ScenarioItemViewModel> Scenarios { get; } = [];

    [ObservableProperty] private ScenarioItemViewModel? _selectedScenario;
    [ObservableProperty] private bool _isEditing;

    public ScenarioEditViewModel EditViewModel => _editVm;

    public ScenarioListViewModel(
        IScenarioRepository repo,
        IWatcherService watcher,
        ISchedulerService scheduler,
        ScenarioEditViewModel editVm)
    {
        _repo = repo;
        _watcher = watcher;
        _scheduler = scheduler;
        _editVm = editVm;

        _editVm.Saved += OnScenarioSaved;
        _editVm.Cancelled += () => IsEditing = false;

        LoadScenarios();
    }

    [RelayCommand]
    private void NewScenario()
    {
        _editVm.Initialize();
        IsEditing = true;
    }

    [RelayCommand]
    private void EditScenario(ScenarioItemViewModel item)
    {
        var scenario = _repo.GetById(item.Id);
        if (scenario is null) return;
        _editVm.Initialize(scenario);
        IsEditing = true;
    }

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
        var all = _repo.GetAll().Select(s => s.Id == updated.Id ? updated : s).ToList();
        _repo.Save(all);

        if (updated.IsScheduled)
            _scheduler.RegisterTask(updated);
        else
            _scheduler.DeleteTask(updated.Name);

        LoadScenarios();
    }

    private void OnScenarioSaved(Scenario scenario)
    {
        var all = _repo.GetAll().ToList();
        var idx = all.FindIndex(s => s.Id == scenario.Id);
        if (idx >= 0)
            all[idx] = scenario;
        else
            all.Add(scenario);

        _repo.Save(all);

        // Restart watcher so the updated scenario snapshot is used
        if (_watcher.IsWatching(scenario.Id))
        {
            _watcher.Stop(scenario.Id);
            if (scenario.IsActive) _watcher.Start(scenario);
        }

        if (scenario.IsScheduled)
            _scheduler.RegisterTask(scenario);
        else
            _scheduler.DeleteTask(scenario.Name);

        IsEditing = false;
        LoadScenarios();
    }

    private void LoadScenarios()
    {
        Scenarios.Clear();
        foreach (var s in _repo.GetAll())
            Scenarios.Add(new ScenarioItemViewModel(s, _watcher.IsWatching(s.Id)));
    }
}
