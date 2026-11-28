using System.Diagnostics;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using DupeFinderPro.Infrastructure.Detection;
using DupeFinderPro.Infrastructure.FileSystem;
using DupeFinderPro.Infrastructure.Hashing;

/// <summary>
/// 탐지 성능 측정(5단계). `dotnet run -- --perf`로 실행한다. 결과가 맞는지와 함께 전체 해시 횟수·읽은 양·시간을 보여 준다.
/// - 작은 파일: 4KB 이하 동일 쌍 2,000개(4,000 파일)
/// - 머리가 같은 파일: 앞 4KB가 크기가 다른 파일과만 같은 64KB 파일 1,000개(같은 크기끼리는 다름, 중복 없음)
/// </summary>
static class PerfChecks
{
    public static async Task<int> RunAsync()
    {
        var dir = Directory.CreateTempSubdirectory("dupe-perf-").FullName;
        var failures = 0;
        try
        {
            var rnd = new Random(11);
            var small = Path.Combine(dir, "small");
            Directory.CreateDirectory(small);
            for (var i = 0; i < 2000; i++)
            {
                var bytes = new byte[rnd.Next(100, 4097)];
                rnd.NextBytes(bytes);
                File.WriteAllBytes(Path.Combine(small, $"{i}-a.bin"), bytes);
                File.WriteAllBytes(Path.Combine(small, $"{i}-b.bin"), bytes);
            }

            var header = Path.Combine(dir, "header");
            Directory.CreateDirectory(header);
            var patterns = Enumerable.Range(0, 500).Select(_ => { var p = new byte[4096]; rnd.NextBytes(p); return p; }).ToArray();
            for (var k = 0; k < 500; k++)
            {
                for (var j = 0; j < 2; j++)
                {
                    var bytes = new byte[65536 + k * 16];
                    rnd.NextBytes(bytes);
                    patterns[(k + j) % 500].CopyTo(bytes, 0);
                    File.WriteAllBytes(Path.Combine(header, $"{k}-{j}.bin"), bytes);
                }
            }

            // 화면 없이 결과 ViewModel만 만들 때의 메모리(그룹 3,000개). 화면이 쓰는 메모리와 나눠 보기 위함.
            {
                var many = Enumerable.Range(0, 3000).Select(i =>
                {
                    var files = new[] { "a", "b" }.Select(s => new FileEntry($@"C:\none\g{i}-{s}.bin", $"g{i}-{s}.bin", 1000 + i, DateTime.Now, DateTime.Now, 0)).ToArray();
                    return new DuplicateGroup($"{i:X64}", files) { SuggestedKeep = files[0] };
                }).ToList();
                GC.Collect(); GC.WaitForPendingFinalizers(); GC.Collect();
                var before = GC.GetTotalMemory(true);
                var sw = Stopwatch.StartNew();
                var vm = new DupeFinderPro.ViewModels.Duplicate.ResultsViewModel(null!,
                    new DupeFinderPro.Application.CleanupOrchestrator(new TempDelete(dir), new FileVerifier(), new MemoryLog()));
                vm.LoadJob(new ScanJob("many", null!) { Result = new ScanResult(many, 6000, 0, TimeSpan.Zero, []) });
                sw.Stop();
                var after = GC.GetTotalMemory(true);
                Console.WriteLine($"PERF view models for 3000 groups: {(after - before) / 1024 / 1024}MB managed, load {sw.ElapsedMilliseconds}ms, groups={vm.Groups.Count}");
                GC.KeepAlive(vm);
            }

            foreach (var (label, folder, expectedGroups) in new[] { ("small pairs", small, 2000), ("shared headers", header, 0) })
            {
                var files = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [folder], ExcludeSystemFiles = false },
                    new Progress<ScanProgress>())).Files;
                for (var run = 1; run <= 2; run++)
                {
                    var hasher = new CountingHasher(new HashingService());
                    var sw = Stopwatch.StartNew();
                    var (groups, skipped, _) = await new DuplicateDetector(hasher, new PriorityAutoSelectStrategy())
                        .DetectAsync(files, new Progress<ScanProgress>());
                    sw.Stop();
                    Console.WriteLine($"PERF {label} run {run}: files={files.Count} groups={groups.Count} skipped={skipped.Count} " +
                                      $"partial={hasher.PartialCalls} full={hasher.FullCalls} fullBytes={hasher.FullBytes / 1024}KB time={sw.ElapsedMilliseconds}ms");
                    if (groups.Count != expectedGroups || skipped.Count != 0)
                    {
                        Console.WriteLine($"FAIL {label}: expected {expectedGroups} groups and no skipped files");
                        failures++;
                    }
                }
            }
        }
        finally
        {
            Directory.Delete(dir, true);
        }
        Console.WriteLine(failures == 0 ? "PERF RESULTS CORRECT" : $"PERF {failures} FAILED");
        return failures;
    }

    /// <summary>해시 호출 횟수와 전체 해시로 읽은 양을 센다.</summary>
    private sealed class CountingHasher(IHashingService inner) : IHashingService
    {
        public int PartialCalls;
        public int FullCalls;
        public long FullBytes;

        public Task<PartialHash> ComputePartialHashAsync(string filePath, CancellationToken ct = default)
        {
            Interlocked.Increment(ref PartialCalls);
            return inner.ComputePartialHashAsync(filePath, ct);
        }

        public Task<string> ComputeFullHashAsync(string filePath, CancellationToken ct = default)
        {
            Interlocked.Increment(ref FullCalls);
            Interlocked.Add(ref FullBytes, new FileInfo(filePath).Length);
            return inner.ComputeFullHashAsync(filePath, ct);
        }
    }
}
