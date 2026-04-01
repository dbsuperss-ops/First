using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IDuplicateDetector
{
    Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileEntry> files,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default);
}
