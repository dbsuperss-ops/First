using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Domain.Interfaces;

public interface IClassifyRecordRepository
{
    IReadOnlyList<ClassifyRecord> GetAll();
    bool Add(ClassifyRecord record);
    void Clear();
}
