using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Domain.Interfaces;

public interface IClassifyService
{
    Task<IReadOnlyList<ClassifyResult>> PreviewAsync(Scenario scenario, CancellationToken ct = default);
    Task<int> ExecuteAsync(IReadOnlyList<ClassifyResult> results, Scenario scenario,
        IProgress<int>? progress = null, CancellationToken ct = default);
    bool MatchesRule(FileInfo file, ClassifyRule rule);
    string BuildTargetPath(FileInfo file, ClassifyRule rule, string baseTarget);
}
