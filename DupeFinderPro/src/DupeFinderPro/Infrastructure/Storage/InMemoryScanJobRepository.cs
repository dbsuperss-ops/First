using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Infrastructure.Storage;

public sealed class InMemoryScanJobRepository : IScanJobRepository
{
    private readonly List<ScanJob> _jobs = [];
    private readonly object _lock = new();

    public void Add(ScanJob job)
    {
        lock (_lock)
            _jobs.Add(job);
    }

    public void Update(ScanJob job)
    {
        // In-memory: the job object is already mutated in-place, no-op needed.
        // This method exists to allow future persistence implementations.
    }

    public IReadOnlyList<ScanJob> GetAll()
    {
        lock (_lock)
            return _jobs.AsReadOnly();
    }

    public ScanJob? GetById(Guid id)
    {
        lock (_lock)
            return _jobs.FirstOrDefault(j => j.Id == id);
    }

    public ScanJob? GetLatestCompleted()
    {
        lock (_lock)
            return _jobs
                .Where(j => j.Status == ScanJobStatus.Completed)
                .OrderByDescending(j => j.CreatedAt)
                .FirstOrDefault();
    }
}
