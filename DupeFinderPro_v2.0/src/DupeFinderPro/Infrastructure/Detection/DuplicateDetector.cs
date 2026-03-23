using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Infrastructure.Detection;

public sealed class DuplicateDetector : IDuplicateDetector
{
    private readonly IHashingService _hasher;
    private readonly IAutoSelectStrategy _autoSelect;

    public DuplicateDetector(IHashingService hasher, IAutoSelectStrategy autoSelect)
    {
        _hasher = hasher;
        _autoSelect = autoSelect;
    }

    public async Task<IReadOnlyList<DuplicateGroup>> DetectAsync(
        IReadOnlyList<FileEntry> files,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        // Step 1: group by size
        var sizeFiltered = files
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        // Step 2: partial hash
        var withPartialHash = await ComputeHashesAsync(
            sizeFiltered,
            f => _hasher.ComputePartialHashAsync(f.FullPath, ct),
            (f, h) => f with { PartialHash = h },
            ScanPhase.PartialHashing,
            progress,
            sizeFiltered.Count,
            ct);

        var partialFiltered = withPartialHash
            .GroupBy(f => f.PartialHash!)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

        // Step 3: full hash
        var withFullHash = await ComputeHashesAsync(
            partialFiltered,
            f => _hasher.ComputeFullHashAsync(f.FullPath, ct),
            (f, h) => f with { FullHash = h },
            ScanPhase.FullHashing,
            progress,
            partialFiltered.Count,
            ct);

        // Step 4: final grouping + auto-select
        var groups = withFullHash
            .GroupBy(f => f.FullHash!)
            .Where(g => g.Count() > 1)
            .Select(g =>
            {
                var fileList = g.ToList();
                var keeper = _autoSelect.SelectKeeper(fileList);
                return new DuplicateGroup(g.Key, fileList) { SuggestedKeep = keeper };
            })
            .ToList();

        progress.Report(new ScanProgress(ScanPhase.Completed, files.Count, files.Count, groups.Count));
        return groups;
    }

    private static async Task<List<FileEntry>> ComputeHashesAsync(
        List<FileEntry> files,
        Func<FileEntry, Task<string>> hashFunc,
        Func<FileEntry, string, FileEntry> updater,
        ScanPhase phase,
        IProgress<ScanProgress> progress,
        int total,
        CancellationToken ct)
    {
        var results = new FileEntry[files.Count];
        var processed = 0;

        await Parallel.ForEachAsync(
            files.Select((f, i) => (f, i)),
            new ParallelOptions { MaxDegreeOfParallelism = 4, CancellationToken = ct },
            async (item, _) =>
            {
                try
                {
                    var hash = await hashFunc(item.f);
                    results[item.i] = updater(item.f, hash);
                }
                catch
                {
                    results[item.i] = item.f;
                }

                var current = Interlocked.Increment(ref processed);
                if (current % 50 == 0)
                    progress.Report(new ScanProgress(phase, current, total, 0, item.f.FullPath));
            });

        return results.Where(f => f is not null).ToList();
    }
}
