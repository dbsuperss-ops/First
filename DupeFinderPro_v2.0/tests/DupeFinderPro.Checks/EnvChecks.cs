using System.Diagnostics;
using System.Security.Cryptography;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using DupeFinderPro.Infrastructure.Detection;
using DupeFinderPro.Infrastructure.FileSystem;
using DupeFinderPro.Infrastructure.Hashing;
using DupeFinderPro.ViewModels.Duplicate;

/// <summary>
/// 실제 Windows 환경 회귀 검사(4단계). `dotnet run -- --env`로 실행한다.
/// 이 세션에만 쓰는 subst 드라이브와 net use(/persistent:no) 연결을 만들었다가 지운다.
/// 대용량 항목은 F:·D: 같은 실제 디스크에 1GiB 파일을 만들었다가 지운다.
/// </summary>
static class EnvChecks
{
    private static int _failures;

    public static async Task<int> RunAsync()
    {
        var dir = Directory.CreateTempSubdirectory("dupe-env-").FullName;
        var mapped = new List<string>();
        try
        {
            await AliasesAsync(dir, mapped);
            await LargeFilesAsync(dir);
        }
        finally
        {
            foreach (var letter in mapped)
                Run(letter.StartsWith("subst") ? "subst" : "net", letter.StartsWith("subst") ? $"{letter[6..]} /d" : $"use {letter[4..]} /delete /y");
            Directory.Delete(dir, true);
        }
        Console.WriteLine(_failures == 0 ? "ENV ALL PASSED" : $"ENV {_failures} FAILED");
        return _failures;
    }

    // ── 같은 파일을 가리키는 다른 경로: subst, 매핑 드라이브, UNC ──────────
    private static async Task AliasesAsync(string dir, List<string> mapped)
    {
        var root = Path.Combine(dir, "alias-root");
        Directory.CreateDirectory(root);
        var file = Path.Combine(root, "only.bin");
        File.WriteAllBytes(file, Enumerable.Repeat((byte)3, 4096).ToArray());
        var rest = root[2..]; // "\scratch\..." (드라이브 문자 뒤)

        var aliases = new List<(string Label, string Root)>();
        if (FreeLetter() is { } s && Run("subst", $"{s}: \"{root}\"") == 0)
        {
            mapped.Add($"subst {s}:");
            aliases.Add(($"subst {s}:", $@"{s}:\"));
        }
        var unc = $@"\\localhost\{root[0]}${rest}";
        if (Directory.Exists(unc))
        {
            aliases.Add(("UNC", unc));
            if (FreeLetter() is { } n && Run("net", $@"use {n}: \\localhost\{root[0]}$ /persistent:no") == 0)
            {
                mapped.Add($"net {n}:");
                aliases.Add(($"mapped {n}:", $@"{n}:{rest}"));
            }
        }

        var detector = new DuplicateDetector(new HashingService(), new PriorityAutoSelectStrategy());
        var localId = FileIdentityReader.TryGet(file);
        foreach (var (label, aliasRoot) in aliases)
        {
            var aliasFile = Path.Combine(aliasRoot, "only.bin");
            var aliasId = FileIdentityReader.TryGet(aliasFile);
            Console.WriteLine($"INFO {label}: local id={localId} alias id={aliasId}");

            var found = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [root, aliasRoot], ExcludeSystemFiles = false },
                new Progress<ScanProgress>())).Files;
            var (groups, skipped, merged) = await detector.DetectAsync(found, new Progress<ScanProgress>());
            Check($"{label}: one physical file is not reported as its own duplicate (entries={found.Count}, groups={groups.Count}, merged={merged.Count}, skipped={skipped.Count})",
                groups.Count == 0);

            // 오래된 결과로 지우려 해도 같은 실제 파일은 남는 사본이 아니다.
            var ops = new TempDelete(Path.GetPathRoot(file)!);
            var vm = new ResultsViewModel(null!, new CleanupOrchestrator(ops, new FileVerifier(), new MemoryLog()))
            {
                ConfirmDeleteAsync = _ => Task.FromResult(true),
            };
            var entries = new[] { file, aliasFile }.Select(p => new FileEntry(p, "only.bin", 4096, DateTime.Now, DateTime.Now, 0)).ToArray();
            vm.LoadJob(new ScanJob(label, null!) { Result = new ScanResult([new DuplicateGroup(HashOf(file), entries)], 2, 4096, TimeSpan.Zero, []) });
            await vm.Groups[0].Files[0].SetActionDeleteCommand.ExecuteAsync(null);
            Check($"{label}: deleting the local path with only its alias left is held (exists={File.Exists(file)}, status={vm.Groups[0].Files[0].StatusMessage})",
                File.Exists(file) && ops.Deleted == 0);
        }
        if (aliases.Count == 0)
            Console.WriteLine("SKIP aliases (no free drive letter and no admin share)");
    }

    // ── 대용량: 탐지·삭제 전 내용 확인 시간, 다른 볼륨 이동과 중간 취소 ───
    private static async Task LargeFilesAsync(string dir)
    {
        const long size = 1L << 30; // 1 GiB
        var big = Path.Combine(dir, "big");
        Directory.CreateDirectory(big);
        var a = Path.Combine(big, "a.bin");
        var b = Path.Combine(big, "b.bin");
        var sw = Stopwatch.StartNew();
        WriteFile(a, size);
        File.Copy(a, b);
        Console.WriteLine($"INFO created 2 x 1 GiB in {sw.Elapsed.TotalSeconds:F1}s");

        sw.Restart();
        var found = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [big], ExcludeSystemFiles = false },
            new Progress<ScanProgress>())).Files;
        var (groups, _, _) = await new DuplicateDetector(new HashingService(), new PriorityAutoSelectStrategy())
            .DetectAsync(found, new Progress<ScanProgress>());
        Console.WriteLine($"INFO scan + detect 2 GiB: {sw.Elapsed.TotalSeconds:F1}s");
        Check("large: the two copies are found as one group", groups.Count == 1 && groups[0].Files.Count == 2);

        // 삭제 직전 내용 확인: 남길 파일과 지울 파일을 모두 읽는다.
        var ops = new TempDelete(Path.GetPathRoot(a)!);
        var log = new MemoryLog();
        var cleanup = new CleanupOrchestrator(ops, new FileVerifier(), log);
        var entryA = groups[0].Files.First(f => f.FullPath == a);
        var entryB = groups[0].Files.First(f => f.FullPath == b);
        sw.Restart();
        var results = await cleanup.ExecuteAsync(new GroupPlan(groups[0].Hash, [entryA, entryB], [new PlannedAction(entryB, FileAction.Delete, "")], ""));
        Console.WriteLine($"INFO verify + delete 1 GiB (reads 2 GiB): {sw.Elapsed.TotalSeconds:F1}s");
        Check($"large: verified delete succeeds ({results[0].Outcome})", results[0].Outcome == ActionOutcome.Succeeded && File.Exists(a) && !File.Exists(b));

        var otherRoot = new[] { @"D:\", @"C:\" }.FirstOrDefault(r =>
            !string.Equals(r, Path.GetPathRoot(a), StringComparison.OrdinalIgnoreCase) && Directory.Exists(r) &&
            new DriveInfo(r).AvailableFreeSpace > 3 * size);
        if (otherRoot is null)
        {
            Console.WriteLine("SKIP large cross-volume move (no other volume with 3 GiB free)");
            return;
        }

        var target = Path.Combine(otherRoot, $"dupe-env-{Guid.NewGuid():N}");
        try
        {
            var service = new FileOperationService();
            using (var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(400)))
            {
                string? moved = null;
                var cancelled = false;
                try { moved = await service.TransferAsync(a, target, markAsQuarantine: false, cts.Token); }
                catch (OperationCanceledException) { cancelled = true; }
                var leftovers = Directory.Exists(target) ? Directory.GetFiles(target) : [];
                Check($"large: a cross-volume move cancelled mid-copy keeps the original and leaves no copy (cancelled={cancelled}, moved={moved}, leftovers={leftovers.Length})",
                    cancelled && File.Exists(a) && new FileInfo(a).Length == size && leftovers.Length == 0);
            }

            sw.Restart();
            var dest = await service.TransferAsync(a, target, markAsQuarantine: false);
            Console.WriteLine($"INFO cross-volume move 1 GiB ({Path.GetPathRoot(a)} -> {otherRoot}): {sw.Elapsed.TotalSeconds:F1}s");
            Check($"large: a cross-volume move completes and verifies (dest={dest})",
                dest is not null && File.Exists(dest) && !File.Exists(a) && new FileInfo(dest).Length == size);
        }
        finally
        {
            if (Directory.Exists(target))
                Directory.Delete(target, true);
        }
    }

    private static void WriteFile(string path, long size)
    {
        var buffer = new byte[1 << 20];
        new Random(7).NextBytes(buffer);
        using var fs = new FileStream(path, FileMode.CreateNew, FileAccess.Write);
        for (long written = 0; written < size; written += buffer.Length)
            fs.Write(buffer, 0, buffer.Length);
    }

    private static string HashOf(string path)
    {
        using var fs = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(fs));
    }

    private static char? FreeLetter()
    {
        var used = DriveInfo.GetDrives().Select(d => char.ToUpperInvariant(d.Name[0])).ToHashSet();
        for (var c = 'Z'; c >= 'H'; c--)
            if (!used.Contains(c))
                return c;
        return null;
    }

    private static int Run(string exe, string arguments)
    {
        using var p = Process.Start(new ProcessStartInfo(exe, arguments)
        {
            UseShellExecute = false, RedirectStandardOutput = true, RedirectStandardError = true,
        })!;
        p.WaitForExit();
        return p.ExitCode;
    }

    private static void Check(string label, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {label}");
        if (!ok) _failures++;
    }
}
