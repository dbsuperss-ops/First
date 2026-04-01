using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Domain.Interfaces;

public interface IOrganizeLogRepository
{
    IReadOnlyList<OrganizeLogEntry> GetAll();
    bool AddRange(IReadOnlyList<OrganizeLogEntry> entries);
    (int success, int fail) UndoBatch(Guid batchId);
    Guid GetLastBatchId();
    void Clear();
}
