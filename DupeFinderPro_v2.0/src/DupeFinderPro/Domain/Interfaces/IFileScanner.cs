using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IFileScanner
{
    Task<IReadOnlyList<FileEntry>> ScanAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default);
}
