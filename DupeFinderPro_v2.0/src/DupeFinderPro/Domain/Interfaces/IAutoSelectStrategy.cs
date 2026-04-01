using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IAutoSelectStrategy
{
    FileEntry SelectKeeper(IReadOnlyList<FileEntry> duplicates);
}
