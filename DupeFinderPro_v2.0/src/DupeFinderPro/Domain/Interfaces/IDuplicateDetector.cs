using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IDuplicateDetector
{
    Task<(IReadOnlyList<DuplicateGroup> Groups, int TotalFiles)> DetectAsync(
        IAsyncEnumerable<FileEntry> files,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default);
}
