using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Domain.Interfaces;

public interface IWatcherService : IDisposable
{
    bool Start(Scenario scenario);
    void Stop(Guid scenarioId);
    void StopAll();
    bool IsWatching(Guid scenarioId);
}
