using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Infrastructure.Organize;

public sealed class WatcherService : IWatcherService
{
    private readonly IClassifyService _classify;
    private readonly Dictionary<Guid, FileSystemWatcher> _watchers = new();
    private readonly Lock _lock = new();

    public WatcherService(IClassifyService classify)
    {
        _classify = classify;
    }

    public bool Start(Scenario scenario)
    {
        if (!scenario.IsActive) return false;

        lock (_lock)
        {
            if (_watchers.ContainsKey(scenario.Id)) return true;
            if (string.IsNullOrEmpty(scenario.SourceFolder) || !Directory.Exists(scenario.SourceFolder))
                return false;

            try
            {
                var watcher = new FileSystemWatcher(scenario.SourceFolder)
                {
                    NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite,
                    IncludeSubdirectories = scenario.IncludeSubfolders,
                    EnableRaisingEvents = true
                };

                watcher.Created += (_, e) => OnCreated(e.FullPath, scenario);
                _watchers[scenario.Id] = watcher;
                return true;
            }
            catch { return false; }
        }
    }

    public void Stop(Guid scenarioId)
    {
        lock (_lock)
        {
            if (!_watchers.TryGetValue(scenarioId, out var watcher)) return;
            watcher.EnableRaisingEvents = false;
            watcher.Dispose();
            _watchers.Remove(scenarioId);
        }
    }

    public void StopAll()
    {
        lock (_lock)
        {
            foreach (var (_, watcher) in _watchers)
            {
                watcher.EnableRaisingEvents = false;
                watcher.Dispose();
            }
            _watchers.Clear();
        }
    }

    public bool IsWatching(Guid scenarioId)
    {
        lock (_lock) { return _watchers.ContainsKey(scenarioId); }
    }

    public void Dispose() => StopAll();

    private void OnCreated(string fullPath, Scenario scenario)
    {
        _ = Task.Run(async () =>
        {
            await Task.Delay(1500);
            try
            {
                if (!File.Exists(fullPath)) return;
                var fi = new FileInfo(fullPath);
                foreach (var rule in scenario.Rules)
                {
                    if (!_classify.MatchesRule(fi, rule)) continue;
                    var result = new ClassifyResult(fi.Name, fullPath,
                        _classify.BuildTargetPath(fi, rule, scenario.TargetFolder), rule.RuleName, fi.Length);
                    await _classify.ExecuteAsync([result], scenario);
                    break;
                }
            }
            catch { /* swallow — watcher fires on background thread, no UI to report to */ }
        });
    }
}
