using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Application;

public sealed class OrganizeOrchestrator
{
    private readonly IClassifyService _classify;
    private readonly IScenarioRepository _scenarioRepo;

    public OrganizeOrchestrator(IClassifyService classify, IScenarioRepository scenarioRepo)
    {
        _classify = classify;
        _scenarioRepo = scenarioRepo;
    }

    public Task<IReadOnlyList<ClassifyResult>> PreviewAsync(Scenario scenario, CancellationToken ct = default)
        => _classify.PreviewAsync(scenario, ct);

    public Task<int> ExecuteAsync(IReadOnlyList<ClassifyResult> results, Scenario scenario,
        IProgress<int>? progress = null, CancellationToken ct = default)
        => _classify.ExecuteAsync(results, scenario, progress, ct);

    public async Task<int> RunScenarioAsync(Scenario scenario,
        IProgress<int>? progress = null, CancellationToken ct = default)
    {
        var results = await _classify.PreviewAsync(scenario, ct);
        return await _classify.ExecuteAsync(results, scenario, progress, ct);
    }

    public IReadOnlyList<Scenario> GetScenarios() => _scenarioRepo.GetAll();

    public bool SaveScenarios(IReadOnlyList<Scenario> scenarios) => _scenarioRepo.Save(scenarios);
}
