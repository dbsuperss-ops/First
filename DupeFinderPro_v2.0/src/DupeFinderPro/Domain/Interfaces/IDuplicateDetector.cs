using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IDuplicateDetector
{
    Task<DetectionResult> DetectAsync(
        IReadOnlyList<FileEntry> files,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default);
}
