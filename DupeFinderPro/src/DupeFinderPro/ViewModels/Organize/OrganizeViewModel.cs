using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class OrganizeViewModel : ViewModelBase
{
    private readonly OrganizeOrchestrator _orchestrator;
    private readonly IScenarioRepository _repo;

    private CancellationTokenSource? _cts;
    private IReadOnlyList<ClassifyResult> _previewResults = [];

    public ObservableCollection<ScenarioItemViewModel> Scenarios { get; } = [];
    public ObservableCollection<ClassifyResultItemViewModel> PreviewItems { get; } = [];

    [ObservableProperty] private ScenarioItemViewModel? _selectedScenario;
    [ObservableProperty] private bool _isBusy;
    [ObservableProperty] private string _statusText = "시나리오를 선택하고 미리보기를 실행하세요.";
    [ObservableProperty] private int _progress;
    [ObservableProperty] private bool _hasPreview;

    public OrganizeViewModel(OrganizeOrchestrator orchestrator, IScenarioRepository repo)
    {
        _orchestrator = orchestrator;
        _repo = repo;
        LoadScenarios();
    }

    public void Refresh() => LoadScenarios();

    [RelayCommand]
    private async Task PreviewAsync()
    {
        var scenario = GetSelectedScenario();
        if (scenario is null) return;

        IsBusy = true;
        StatusText = "미리보기 생성 중…";
        PreviewItems.Clear();
        HasPreview = false;
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            _previewResults = await _orchestrator.PreviewAsync(scenario, _cts.Token);
            foreach (var r in _previewResults)
                PreviewItems.Add(new ClassifyResultItemViewModel(r));

            HasPreview = PreviewItems.Count > 0;
            StatusText = PreviewItems.Count > 0
                ? $"{PreviewItems.Count}개 파일이 분류될 예정입니다."
                : "분류할 파일이 없습니다.";
        }
        catch (OperationCanceledException) { StatusText = "미리보기가 취소되었습니다."; }
        catch { StatusText = "미리보기 중 오류가 발생했습니다."; }
        finally { IsBusy = false; }
    }

    [RelayCommand]
    private async Task ExecuteAsync()
    {
        var scenario = GetSelectedScenario();
        if (scenario is null || _previewResults.Count == 0) return;

        IsBusy = true;
        Progress = 0;
        StatusText = "파일 정리 중…";
        _cts?.Dispose();
        _cts = new CancellationTokenSource();

        try
        {
            var total = _previewResults.Count;
            var progressReport = new Progress<int>(p =>
            {
                Progress = (int)((double)p / total * 100);
                StatusText = $"파일 정리 중… {p}/{total}";
            });

            var count = await _orchestrator.ExecuteAsync(_previewResults, scenario, progressReport, _cts.Token);
            StatusText = $"완료: {count}개 파일이 정리되었습니다.";
            HasPreview = false;
            PreviewItems.Clear();
            _previewResults = [];
        }
        catch (OperationCanceledException) { StatusText = "정리가 취소되었습니다."; }
        catch { StatusText = "정리 중 오류가 발생했습니다."; }
        finally { IsBusy = false; Progress = 0; }
    }

    [RelayCommand]
    private void Cancel()
    {
        _cts?.Cancel();
    }

    private Scenario? GetSelectedScenario()
    {
        if (SelectedScenario is null)
        {
            StatusText = "시나리오를 먼저 선택해주세요.";
            return null;
        }
        return _repo.GetById(SelectedScenario.Id);
    }

    private void LoadScenarios()
    {
        Scenarios.Clear();
        foreach (var s in _repo.GetAll().Where(s => s.IsActive))
            Scenarios.Add(new ScenarioItemViewModel(s));
    }
}
