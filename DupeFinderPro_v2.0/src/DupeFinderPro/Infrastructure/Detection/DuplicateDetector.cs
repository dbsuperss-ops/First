using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using DupeFinderPro.Infrastructure.FileSystem;

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

    public async Task<DetectionResult> DetectAsync(
        IReadOnlyList<FileEntry> files,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        // Step 1: group by size
        var sameSize = SameSizeOnly(files);

        // Step 1b: 경로가 달라도 실제 파일이 같으면(정션·매핑 드라이브·UNC·하드 링크) 한 항목만 남긴다.
        // 둘 다 남기면 하나뿐인 파일이 자기 자신의 중복이 되어 삭제될 수 있다.
        // ID를 읽지 못한 파일은 다른 파일과 같은지 알 수 없으므로 비교하지 않는다.
        var identified = await Task.Run(() => sameSize
            .AsParallel().AsOrdered()
            .WithDegreeOfParallelism(4)
            .WithCancellation(ct)
            .Select(f => f with { Identity = FileIdentityReader.TryGet(f.FullPath) })
            .ToList(), ct);

        var skipped = identified.Where(f => f.Identity is null).ToList();
        var aliases = new List<FileEntry>();
        var unique = new List<FileEntry>();
        foreach (var byId in identified.Where(f => f.Identity is not null).GroupBy(f => f.Identity))
        {
            // 스캐너가 우선순위 높은 폴더부터 수집하므로, 같은 우선순위 안에서는 먼저 수집한 경로를 남긴다.
            var ordered = byId.OrderBy(f => f.SourcePriority).ToList();
            unique.Add(ordered[0]);
            aliases.AddRange(ordered.Skip(1));
        }

        var sizeFiltered = SameSizeOnly(unique);

        // Step 2: partial hash
        var withPartialHash = await ComputeHashesAsync(
            sizeFiltered,
            f => _hasher.ComputePartialHashAsync(f.FullPath, ct),
            (f, h) => f with { PartialHash = h },
            ScanPhase.PartialHashing,
            progress,
            sizeFiltered.Count,
            ct);

        skipped.AddRange(withPartialHash.Where(f => f.PartialHash is null));

        var partialFiltered = withPartialHash
            .Where(f => f.PartialHash is not null)
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

        skipped.AddRange(withFullHash.Where(f => f.FullHash is null));

        // Step 4: final grouping + auto-select
        var groups = withFullHash
            .Where(f => f.FullHash is not null)
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
        return new DetectionResult(groups, skipped, aliases);
    }

    private static List<FileEntry> SameSizeOnly(IEnumerable<FileEntry> files) =>
        files
            .GroupBy(f => f.SizeBytes)
            .Where(g => g.Count() > 1)
            .SelectMany(g => g)
            .ToList();

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
                    if (IsCloudOnlyFile(item.f.FullPath))
                    {
                        results[item.i] = item.f;
                    }
                    else
                    {
                        var hash = await hashFunc(item.f);
                        results[item.i] = updater(item.f, hash);
                    }
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    // 해시가 없는 항목은 호출부에서 비교 대상에서 빼고 SkippedFiles로 보고한다.
                    results[item.i] = item.f;
                }

                var current = Interlocked.Increment(ref processed);
                if (current % 50 == 0)
                    progress.Report(new ScanProgress(phase, current, total, 0, item.f.FullPath));
            });

        return results.ToList();
    }

    private static bool IsCloudOnlyFile(string filePath)
    {
        try
        {
            var attrs = File.GetAttributes(filePath);
            const FileAttributes offline = FileAttributes.Offline;
            const int recallOnDataAccess = 0x400000; // FILE_ATTRIBUTE_RECALL_ON_DATA_ACCESS
            return (attrs & offline) != 0 || ((int)attrs & recallOnDataAccess) != 0;
        }
        catch
        {
            return false;
        }
    }
}
