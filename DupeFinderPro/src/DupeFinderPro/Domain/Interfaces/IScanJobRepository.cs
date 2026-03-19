using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IScanJobRepository
{
    void Add(ScanJob job);
    void Update(ScanJob job);
    IReadOnlyList<ScanJob> GetAll();
    ScanJob? GetById(Guid id);
    ScanJob? GetLatestCompleted();
}
