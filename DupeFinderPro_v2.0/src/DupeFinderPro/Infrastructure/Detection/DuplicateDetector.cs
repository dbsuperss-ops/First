using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using Microsoft.Extensions.Options;

namespace DupeFinderPro.Infrastructure.Detection;

public sealed class DuplicateDetector : IDuplicateDetector
{
    private readonly IHashingService _hasher;
    private readonly IAutoSelectStrategy _autoSelect;
    private readonly DupeFinderOptions _options;

    public DuplicateDetector(
        IHashingService hasher, 
        IAutoSelectStrategy autoSelect,
        IOptions<DupeFinderOptions> options)
    {
        _hasher = hasher;
        _autoSelect = autoSelect;
        _options = options.Value;
    }

    public async Task<(IReadOnlyList<DuplicateGroup> Groups, int TotalFiles)> DetectAsync(
        IAsyncEnumerable<FileEntry> files,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        // Step 1: 스트림을 소비하며 실시간으로 크기별 그룹화 진행 (메모리 방어)
        var sizeGroups = new Dictionary<long, List<FileEntry>>();
        int totalFiles = 0;

        await foreach (var file in files.WithCancellation(ct))
        {
            if (!sizeGroups.TryGetValue(file.SizeBytes, out var list))
            {
                list = [];
                sizeGroups[file.SizeBytes] = list;
            }
            list.Add(file);
            totalFiles++;
        }

        var sizeFiltered = sizeGroups.Values
            .Where(g => g.Count > 1)
            .SelectMany(g => g)
            .ToList();

        int degree = _options.MaxDegreeOfParallelism > 0 ? _options.MaxDegreeOfParallelism : Math.Max(2, Environment.ProcessorCount / 2);
        var po = new ParallelOptions { MaxDegreeOfParallelism = degree, CancellationToken = ct };

        // 부분 해시 -> 전체 해시 로직 (병렬 처리 압축 표현)
        var partials = await ProcessHashes(sizeFiltered, f => _hasher.ComputePartialHashAsync(f.FullPath, ct), (f, h) => f with { PartialHash = h }, po);
        var partialFiltered = partials.GroupBy(f => f.PartialHash!).Where(g => g.Count() > 1).SelectMany(g => g).ToList();
        var fulls = await ProcessHashes(partialFiltered, f => _hasher.ComputeFullHashAsync(f.FullPath, ct), (f, h) => f with { FullHash = h }, po);

        // Step 4: 최종 그룹 및 자동 선택
        var groups = fulls
            .GroupBy(f => f.FullHash!)
            .Where(g => g.Count() > 1)
            .Select(g =>
            {
                var fileList = g.ToList();
                var keeper = _autoSelect.SelectKeeper(fileList);
                return new DuplicateGroup(g.Key, fileList) { SuggestedKeep = keeper };
            })
            .ToList();

        progress.Report(new ScanProgress(ScanPhase.Completed, totalFiles, totalFiles, groups.Count));
        return (groups, totalFiles);
    }

    private async Task<List<FileEntry>> ProcessHashes(List<FileEntry> source, Func<FileEntry, Task<string>> hashFn, Func<FileEntry, string, FileEntry> updateFn, ParallelOptions po)
    {
        var results = new FileEntry[source.Count];
        await Parallel.ForEachAsync(source.Select((f, i) => (f, i)), po, async (item, _) =>
        {
            try { results[item.i] = updateFn(item.f, await hashFn(item.f)); } catch { results[item.i] = null!; }
        });
        return results.Where(x => x is not null).ToList();
    }
}
