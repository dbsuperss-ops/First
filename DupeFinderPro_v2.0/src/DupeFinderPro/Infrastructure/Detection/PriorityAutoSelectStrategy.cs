using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Infrastructure.Detection;

public sealed class PriorityAutoSelectStrategy : IAutoSelectStrategy
{
    public FileEntry SelectKeeper(IReadOnlyList<FileEntry> duplicates)
    {
        return duplicates
            .OrderBy(f => f.SourcePriority)
            .ThenByDescending(f => f.LastModified)
            .First();
    }
}
