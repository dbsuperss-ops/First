using System.Collections.Generic;
using System.Threading;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

public interface IFileScanner
{
    IAsyncEnumerable<FileEntry> ScanAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default);
}
