using System.Security.Cryptography;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using DupeFinderPro.ViewModels.Duplicate;
using DupeFinderPro.Infrastructure.Detection;
using DupeFinderPro.Infrastructure.Hashing;
using DupeFinderPro.Infrastructure.FileSystem;

// 실제 Windows 환경 검사(subst·매핑 드라이브·UNC·대용량)는 따로 실행한다: dotnet run -- --env
if (args.Contains("--env"))
    return await EnvChecks.RunAsync();
// 탐지 성능 측정: dotnet run -- --perf
if (args.Contains("--perf"))
    return await PerfChecks.RunAsync();

// 실제 휴지통 대신 임시 폴더의 파일만 지우는 대역(TempDelete)을 쓴다. 이동·격리는 실제 구현을 쓴다.
var dir = Directory.CreateTempSubdirectory("dupe-checks-").FullName;
var otherVolumeDir = OtherVolumeDir(dir);
var log = new MemoryLog();
var failures = 0;
try
{
    FileEntry Make(string name, int size, byte fill)
    {
        var path = Path.Combine(dir, name);
        File.WriteAllBytes(path, Enumerable.Repeat(fill, size).ToArray());
        return new FileEntry(path, name, size, DateTime.Now, DateTime.Now, 0);
    }
    string HashOf(string path) => Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));

    void Check(string label, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {label}");
        if (!ok) failures++;
    }

    // ── 탐지 ───────────────────────────────────────────────────────────
    var files = new[]
    {
        Make("locked-A.bin", 100, 1), Make("other-100.bin", 100, 2),
        Make("locked-B.bin", 200, 3), Make("other-200.bin", 200, 4),
        Make("same-1.bin", 300, 5),   Make("same-2.bin", 300, 5),
    };

    var detector = new DuplicateDetector(new HashingService(), new PriorityAutoSelectStrategy());
    using (new FileStream(files[0].FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
    using (new FileStream(files[2].FullPath, FileMode.Open, FileAccess.Read, FileShare.None))
    {
        var (groups, skipped, _) = await detector.DetectAsync(files, new Progress<ScanProgress>());
        Check("unreadable files are never grouped", !groups.Any(g => g.Files.Any(f => f.FileName.StartsWith("locked"))));
        Check("identical files are still found", groups.Any(g => g.Files.Select(f => f.FileName).Order().SequenceEqual(["same-1.bin", "same-2.bin"])));
        Check("unreadable files are reported as skipped",
            skipped.Select(f => f.FileName).Order().SequenceEqual(["locked-A.bin", "locked-B.bin"]));
    }

    // ── 결과 화면 준비 ─────────────────────────────────────────────────
    (ResultsViewModel Vm, FileEntry[] Copies, TempDelete Ops) Setup(string tag, int count, bool? consent, int keep = 0)
    {
        var copies = Enumerable.Range(0, count).Select(i => Make($"{tag}-{i}.bin", 50, 9)).ToArray();
        var group = new DuplicateGroup(HashOf(copies[0].FullPath), copies) { SuggestedKeep = copies[keep] };
        var ops = new TempDelete(dir);
        var vm = new ResultsViewModel(null!, new CleanupOrchestrator(ops, new FileVerifier(), log))
        {
            QuarantinePath = Path.Combine(dir, "quarantine"),
        };
        if (consent is { } yes)
            vm.ConfirmDeleteAsync = _ => Task.FromResult(yes);
        vm.LoadJob(new ScanJob(tag, null!) { Result = new ScanResult([group], count, 100, TimeSpan.Zero, []) });
        return (vm, copies, ops);
    }
    int Alive(FileEntry[] c) => c.Count(f => File.Exists(f.FullPath));
    FileEntryViewModel F(ResultsViewModel vm, int i) => vm.Groups[0].Files[i];
    Task Delete(ResultsViewModel vm, int i) => F(vm, i).SetActionDeleteCommand.ExecuteAsync(null);
    void SelectAllDelete(ResultsViewModel vm) { foreach (var f in vm.Groups[0].Files) f.SelectedAction = FileAction.Delete; }

    // ── 일괄 삭제 확인 ─────────────────────────────────────────────────
    async Task<(string[] Left, int Asked)> BulkDelete(string tag, bool? answer, int? deleteFirst)
    {
        var s = Setup(tag, 3, answer, keep: 1);
        var asked = 0;
        if (answer is { } yes) s.Vm.ConfirmDeleteAsync = n => { asked = n; return Task.FromResult(yes); };
        if (deleteFirst is { } i)
            await Delete(s.Vm, i);
        await s.Vm.DeleteDuplicatesCommand.ExecuteAsync(null);
        return (s.Copies.Where(f => File.Exists(f.FullPath)).Select(f => f.FileName[(tag.Length + 1)..^4]).ToArray(), asked);
    }

    var r = await BulkDelete("yes", true, null);
    Check($"confirmed delete keeps only the recommended file (asked={r.Asked})", r.Left.SequenceEqual(["1"]) && r.Asked == 2);
    r = await BulkDelete("no", false, null);
    Check("declined confirmation deletes nothing", r.Left.Length == 3 && r.Asked == 2);
    r = await BulkDelete("noview", null, null);
    Check("without a confirmation dialog nothing is deleted", r.Left.Length == 3);
    r = await BulkDelete("gone", true, 1);
    Check($"after the recommended copy is gone, one copy survives (left={string.Join(",", r.Left)})", r.Left.SequenceEqual(["0"]));

    // ── 처음 선택값(I16) ───────────────────────────────────────────────
    var s = Setup("initial", 3, true);
    Check($"I16 initial selection is keep for every file ({string.Join(",", s.Vm.Groups[0].Files.Select(f => f.SelectedAction))})",
        s.Vm.Groups[0].Files.All(f => f.IsKeep));
    await s.Vm.ApplyAllCommand.ExecuteAsync(null);
    Check($"I16 apply all right after a scan deletes nothing (alive={Alive(s.Copies)})", Alive(s.Copies) == 3);

    // ── 마지막 사본 보존 ───────────────────────────────────────────────
    s = Setup("keeper-gone", 3, true);
    await Delete(s.Vm, 0);
    SelectAllDelete(s.Vm);
    await s.Vm.ApplyAllCommand.ExecuteAsync(null);
    Check($"keeper deleted, then apply all: a copy survives (alive={Alive(s.Copies)})", Alive(s.Copies) >= 1);

    s = Setup("single", 2, true);
    await Delete(s.Vm, 0);
    await Delete(s.Vm, 1);
    Check($"per-file delete cannot remove the last copy (alive={Alive(s.Copies)})", Alive(s.Copies) == 1);
    Check("held file is not marked done and says why", !F(s.Vm, 1).IsDone && F(s.Vm, 1).Outcome == ActionOutcome.Held);

    s = Setup("race", 2, true);
    s.Ops.Gate = new TaskCompletionSource();
    var first = Delete(s.Vm, 0);
    var second = Delete(s.Vm, 1);
    s.Ops.Gate.SetResult();
    await Task.WhenAll(first, second);
    Check($"two overlapping per-file deletes keep one copy (alive={Alive(s.Copies)})", Alive(s.Copies) == 1);

    s = Setup("quarantine-mix", 2, true);
    SelectAllDelete(s.Vm);
    F(s.Vm, 0).SelectedAction = FileAction.Quarantine;
    await s.Vm.ApplyAllCommand.ExecuteAsync(null);
    Check($"quarantine one + delete the rest in one batch: nothing is removed (alive={Alive(s.Copies)})", Alive(s.Copies) == 2);

    s = Setup("normal", 3, true);
    s.Vm.Groups[0].AutoSelectCommand.Execute(null);
    await s.Vm.ApplyAllCommand.ExecuteAsync(null);
    Check($"normal apply all still deletes the other copies (alive={Alive(s.Copies)})", Alive(s.Copies) == 1 && File.Exists(s.Copies[0].FullPath));

    s = Setup("declined", 3, false);
    {
        var f = s.Vm.Groups[0].Files;
        f[0].SelectedAction = FileAction.Delete; f[1].SelectedAction = FileAction.Keep;
        f[2].SelectedAction = FileAction.Quarantine; f[2].IsChecked = false;
        string State() => string.Join(" ", f.Select(x => $"{x.SelectedAction}/{x.IsChecked}"));
        var before = State();
        await s.Vm.DeleteDuplicatesCommand.ExecuteAsync(null);
        Check($"declined delete-duplicates keeps selections (after={State()})", State() == before && Alive(s.Copies) == 3);
    }

    // ── 검색 폴더 겹침 ─────────────────────────────────────────────────
    var root = Path.Combine(dir, "scan");
    var sub = Path.Combine(root, "sub");
    Directory.CreateDirectory(sub);
    File.WriteAllBytes(Path.Combine(sub, "only.bin"), new byte[64]);
    foreach (var (label, paths) in new (string, string[])[]
    {
        ("parent+child", [root, sub]), ("child+parent", [sub, root]), ("same twice", [root, root]),
        ("case + trailing slash", [root, root.ToUpperInvariant() + "\\"]),
        ("slashes + dot segments", [root, root.Replace('\\', '/') + "/sub/../sub"]),
    })
    {
        var found = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = paths, ExcludeSystemFiles = false }, new Progress<ScanProgress>())).Files;
        var (dupes, _, _) = await detector.DetectAsync(found, new Progress<ScanProgress>());
        Check($"overlap {label}: one file is collected once (entries={found.Count}, groups={dupes.Count})", found.Count == 1 && dupes.Count == 0);
        Check($"overlap {label}: highest priority is kept (priority={found[0].SourcePriority})", found[0].SourcePriority == 0);
    }

    // ── 같은 실제 파일(I1) ─────────────────────────────────────────────
    void Mklink(string args)
    {
        using var mk = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo("cmd.exe", $"/c mklink {args} >nul") { UseShellExecute = false })!;
        mk.WaitForExit();
    }

    var link = Path.Combine(dir, "scan-junction");
    Mklink($"/J \"{link}\" \"{root}\"");
    var linked = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [root, link], ExcludeSystemFiles = false }, new Progress<ScanProgress>())).Files;
    var (jGroups, _, jAliases) = await detector.DetectAsync(linked, new Progress<ScanProgress>());
    Check($"I1 junction root: one physical file is not its own duplicate (junction={Directory.Exists(link)}, entries={linked.Count}, groups={jGroups.Count}, merged={jAliases.Count})",
        Directory.Exists(link) && linked.Count == 2 && jGroups.Count == 0 && jAliases.Count == 1);
    Check("I1 junction root: the path from the higher-priority folder is kept", jAliases.Count == 1 && jAliases[0].FullPath.StartsWith(link));
    if (Directory.Exists(link)) Directory.Delete(link);

    var hardDir = Path.Combine(dir, "hard");
    Directory.CreateDirectory(hardDir);
    var hardA = Path.Combine(hardDir, "a.bin");
    var hardB = Path.Combine(hardDir, "b.bin");
    File.WriteAllBytes(hardA, new byte[64]);
    Mklink($"/H \"{hardB}\" \"{hardA}\"");
    var hardFound = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [hardDir], ExcludeSystemFiles = false }, new Progress<ScanProgress>())).Files;
    var (hGroups, _, hAliases) = await detector.DetectAsync(hardFound, new Progress<ScanProgress>());
    Check($"I1 hard links are merged, not grouped (link={File.Exists(hardB)}, entries={hardFound.Count}, groups={hGroups.Count}, merged={hAliases.Count})",
        File.Exists(hardB) && hardFound.Count == 2 && hGroups.Count == 0 && hAliases.Count == 1);
    {
        var entries = new[] { hardA, hardB }.Select(p => new FileEntry(p, Path.GetFileName(p), 64, DateTime.Now, DateTime.Now, 0)).ToArray();
        var ops = new TempDelete(dir);
        var vm = new ResultsViewModel(null!, new CleanupOrchestrator(ops, new FileVerifier(), log)) { ConfirmDeleteAsync = _ => Task.FromResult(true) };
        vm.LoadJob(new ScanJob("hard", null!) { Result = new ScanResult([new DuplicateGroup(HashOf(hardA), entries)], 2, 64, TimeSpan.Zero, []) });
        await Delete(vm, 0);
        Check($"I1 a hard link is not a surviving copy of its twin (deleted={ops.Deleted}, status={F(vm, 0).StatusMessage})",
            ops.Deleted == 0 && F(vm, 0).Outcome == ActionOutcome.Held && File.Exists(hardA));
    }

    // ── 항상 제외하는 폴더(I19)·빈 파일(I7) ────────────────────────────
    var q = Path.Combine(dir, "q-scan");
    var marked = Path.Combine(q, "my-quarantine");
    var recycle = Path.Combine(q, "$RECYCLE.BIN", "S-1");
    Directory.CreateDirectory(Path.Combine(marked, "nested"));
    Directory.CreateDirectory(recycle);
    File.WriteAllText(Path.Combine(marked, QuarantineFolder.MarkerFileName), "");
    foreach (var p in new[] { Path.Combine(q, "orig.bin"), Path.Combine(marked, "nested", "orig.bin"), Path.Combine(recycle, "orig.bin") })
        File.WriteAllBytes(p, Enumerable.Repeat((byte)7, 128).ToArray());
    var qFound = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [q, marked], ExcludeSystemFiles = false }, new Progress<ScanProgress>())).Files;
    Check($"I19 quarantine and recycle bin folders are never collected (found={string.Join(",", qFound.Select(f => Path.GetRelativePath(q, f.FullPath)))})",
        qFound.Count == 1 && qFound[0].FullPath == Path.Combine(q, "orig.bin"));

    var empty = Path.Combine(dir, "empty");
    Directory.CreateDirectory(empty);
    File.WriteAllBytes(Path.Combine(empty, ".gitkeep"), []);
    File.WriteAllBytes(Path.Combine(empty, "__init__.py"), []);
    var eDefault = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [empty], ExcludeSystemFiles = false }, new Progress<ScanProgress>())).Files;
    var eIncluded = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [empty], ExcludeSystemFiles = false, IncludeEmptyFiles = true }, new Progress<ScanProgress>())).Files;
    Check($"I7 empty files are excluded by default and included on request ({eDefault.Count}/{eIncluded.Count})", eDefault.Count == 0 && eIncluded.Count == 2);

    // ── 파일별 삭제 확인(I6)·중복 실행(I4)·결과 표시(I3) ───────────────
    s = Setup("confirm", 2, false);
    await Delete(s.Vm, 0);
    Check($"I6 declined per-file delete changes nothing (alive={Alive(s.Copies)}, action={F(s.Vm, 0).SelectedAction})",
        Alive(s.Copies) == 2 && F(s.Vm, 0).IsKeep && s.Ops.Deleted == 0);

    s = Setup("overlap", 3, true);
    s.Ops.Gate = new TaskCompletionSource();
    var running = Delete(s.Vm, 0);
    var again = Delete(s.Vm, 0);
    s.Ops.Gate.SetResult();
    await Task.WhenAll(running, again);
    Check($"I4 an overlapping call on the same file does not run twice (calls={s.Ops.Calls}, outcome={F(s.Vm, 0).Outcome})",
        s.Ops.Calls == 1 && F(s.Vm, 0).Outcome == ActionOutcome.Succeeded);

    s = Setup("fail", 2, true);
    await F(s.Vm, 0).SetActionMoveCommand.ExecuteAsync(null); // 이동 경로 기본값은 빈 문자열
    Check($"I3 a failed move is not shown as done (status={F(s.Vm, 0).StatusMessage})",
        !F(s.Vm, 0).IsDone && F(s.Vm, 0).Outcome == ActionOutcome.Failed && File.Exists(s.Copies[0].FullPath));

    s = Setup("missing", 3, true);
    File.Delete(s.Copies[0].FullPath);
    await Delete(s.Vm, 0);
    Check($"I3 a file that is already gone is reported as skipped (status={F(s.Vm, 0).StatusMessage})",
        !F(s.Vm, 0).IsDone && F(s.Vm, 0).Outcome == ActionOutcome.Skipped);

    s = Setup("aborted", 2, true);
    s.Ops.ThrowCancel = true;
    await Delete(s.Vm, 0);
    Check($"I3 an aborted delete is not swallowed as an error or shown as done (status={F(s.Vm, 0).StatusMessage})",
        !F(s.Vm, 0).IsDone && F(s.Vm, 0).Outcome == ActionOutcome.Cancelled);

    // ── 2단계: 삭제 직전 내용 확인(I2) ─────────────────────────────────
    s = Setup("keeper-changed", 2, true);
    File.WriteAllBytes(s.Copies[1].FullPath, Enumerable.Repeat((byte)8, 50).ToArray()); // 같은 크기, 다른 내용
    await Delete(s.Vm, 0);
    Check($"I2 a keeper whose content changed since the scan is not a surviving copy (alive={Alive(s.Copies)}, status={F(s.Vm, 0).StatusMessage})",
        Alive(s.Copies) == 2 && F(s.Vm, 0).Outcome == ActionOutcome.Held);

    s = Setup("target-changed", 2, true);
    File.WriteAllBytes(s.Copies[0].FullPath, Enumerable.Repeat((byte)8, 50).ToArray());
    await Delete(s.Vm, 0);
    Check($"I2 a target whose content changed since the scan is not deleted (alive={Alive(s.Copies)}, status={F(s.Vm, 0).StatusMessage})",
        Alive(s.Copies) == 2 && F(s.Vm, 0).StatusMessage == CleanupOrchestrator.ChangedMessage);

    s = Setup("keeper-locked", 2, true);
    string? writeToKeeper = null;
    s.Ops.BeforeDelete = _ =>
    {
        try { using var w = new FileStream(s.Copies[1].FullPath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite | FileShare.Delete); writeToKeeper = "allowed"; }
        catch (IOException) { writeToKeeper = "denied"; }
    };
    await Delete(s.Vm, 0);
    Check($"I2 the keeper cannot be written while a copy is being deleted (write={writeToKeeper}, alive={Alive(s.Copies)})",
        writeToKeeper == "denied" && Alive(s.Copies) == 1);

    // ── 2단계: 동의한 계획만 실행(I5)·취소 ────────────────────────────
    s = Setup("plan-fixed", 3, true);
    F(s.Vm, 1).SelectedAction = FileAction.Delete;
    s.Vm.ConfirmDeleteAsync = _ => { F(s.Vm, 2).SelectedAction = FileAction.Delete; return Task.FromResult(true); };
    await s.Vm.ApplyAllCommand.ExecuteAsync(null);
    Check($"I5 a selection changed while the dialog is open is not executed (alive={string.Join(",", s.Copies.Select(c => File.Exists(c.FullPath)))})",
        File.Exists(s.Copies[0].FullPath) && !File.Exists(s.Copies[1].FullPath) && File.Exists(s.Copies[2].FullPath));

    s = Setup("cancel", 4, true);
    s.Vm.Groups[0].AutoSelectCommand.Execute(null);
    var cancelVm = s.Vm;
    s.Ops.BeforeDelete = _ => cancelVm.CancelApplyCommand.Execute(null); // 첫 파일을 지우는 도중에 "처리 중단"
    await s.Vm.ApplyAllCommand.ExecuteAsync(null);
    var outcomes = s.Vm.Groups[0].Files.Skip(1).Select(f => f.Outcome).ToList();
    Check($"I5 cancel stops the remaining files ({string.Join(",", outcomes)}, alive={Alive(s.Copies)}, applying={s.Vm.IsApplying})",
        outcomes.Count(o => o == ActionOutcome.Succeeded) == 1 && outcomes.Count(o => o == ActionOutcome.Cancelled) == 2 &&
        Alive(s.Copies) == 3 && !s.Vm.IsApplying);

    // ── 2단계: "유지"가 잠그지 않음(I13) ───────────────────────────────
    s = Setup("keep-then-delete", 2, true);
    F(s.Vm, 0).SetActionKeepCommand.Execute(null);
    await Delete(s.Vm, 0);
    Check($"I13 a file marked keep can still be deleted later (alive={Alive(s.Copies)}, outcome={F(s.Vm, 0).Outcome})",
        !File.Exists(s.Copies[0].FullPath) && F(s.Vm, 0).Outcome == ActionOutcome.Succeeded);

    // ── 2단계: 이동·격리(I20) ──────────────────────────────────────────
    s = Setup("move-then-delete", 2, true);
    s.Vm.MoveToPath = Path.Combine(dir, "archive");
    await F(s.Vm, 0).SetActionMoveCommand.ExecuteAsync(null);
    var movedPath = F(s.Vm, 0).CurrentPath;
    await Delete(s.Vm, 1);
    Check($"I20 a verified moved copy counts as the surviving copy (moved={File.Exists(movedPath)}, original={File.Exists(s.Copies[0].FullPath)}, other={File.Exists(s.Copies[1].FullPath)})",
        movedPath.StartsWith(Path.Combine(dir, "archive")) && File.Exists(movedPath) && !File.Exists(s.Copies[0].FullPath) &&
        !File.Exists(s.Copies[1].FullPath) && F(s.Vm, 1).Outcome == ActionOutcome.Succeeded);

    s = Setup("quarantine-last", 2, true);
    await F(s.Vm, 0).SetActionQuarantineCommand.ExecuteAsync(null);
    await F(s.Vm, 1).SetActionQuarantineCommand.ExecuteAsync(null);
    Check($"I20 the last copy is not quarantined (first={F(s.Vm, 0).Outcome}, second={F(s.Vm, 1).Outcome}, alive={Alive(s.Copies)})",
        F(s.Vm, 0).Outcome == ActionOutcome.Succeeded && F(s.Vm, 1).Outcome == ActionOutcome.Held && File.Exists(s.Copies[1].FullPath) &&
        File.Exists(Path.Combine(dir, "quarantine", QuarantineFolder.MarkerFileName)));

    s = Setup("move-same-folder", 2, true);
    s.Vm.MoveToPath = dir;
    await F(s.Vm, 0).SetActionMoveCommand.ExecuteAsync(null);
    Check($"I20 moving into the file's own folder is refused (status={F(s.Vm, 0).StatusMessage})",
        F(s.Vm, 0).Outcome == ActionOutcome.Failed && File.Exists(s.Copies[0].FullPath));

    s = Setup("move-into-quarantine", 2, true);
    s.Vm.MoveToPath = s.Vm.QuarantinePath;
    await F(s.Vm, 0).SetActionMoveCommand.ExecuteAsync(null);
    Check($"I20 moving into the quarantine folder is refused (status={F(s.Vm, 0).StatusMessage})",
        F(s.Vm, 0).Outcome == ActionOutcome.Failed && File.Exists(s.Copies[0].FullPath));

    if (otherVolumeDir is not null)
    {
        s = Setup("cross-volume", 2, true);
        s.Vm.MoveToPath = otherVolumeDir;
        await F(s.Vm, 0).SetActionMoveCommand.ExecuteAsync(null);
        var target = F(s.Vm, 0).CurrentPath;
        Check($"I20 a move to another volume copies, verifies, then removes the original (dest={target})",
            F(s.Vm, 0).Outcome == ActionOutcome.Succeeded && File.Exists(target) && HashOf(target) == HashOf(s.Copies[1].FullPath) &&
            !File.Exists(s.Copies[0].FullPath) && !Directory.EnumerateFiles(otherVolumeDir, "*.dfp-partial").Any());
    }
    else
    {
        Console.WriteLine("SKIP I20 cross-volume move (no other volume found)");
    }

    // ── 5단계: 처리 후 요약 통계(I18) ──────────────────────────────────
    s = Setup("stats", 3, true);
    var wastedBefore = s.Vm.TotalWasted;
    await F(s.Vm, 0).SetActionQuarantineCommand.ExecuteAsync(null);
    var afterQuarantine = (s.Vm.TotalWasted, s.Vm.HasFreedSpace);
    await Delete(s.Vm, 1);
    Check($"I18 summary counts only deleted files as freed (before={wastedBefore}, after quarantine={afterQuarantine}, after delete={s.Vm.TotalWasted}/{s.Vm.FreedSpace}, groups={s.Vm.TotalGroups})",
        wastedBefore == "100 B" && afterQuarantine == ("100 B", false) && s.Vm.TotalWasted == "50 B" && s.Vm.FreedSpace == "50 B" && s.Vm.TotalGroups == 1);

    // ── 5단계: 작은 파일 해시 재사용의 경계 ────────────────────────────
    var edge = Path.Combine(dir, "edge");
    Directory.CreateDirectory(edge);
    byte[] Bytes(int n, byte last) { var b = Enumerable.Repeat((byte)1, n).ToArray(); b[^1] = last; return b; }
    File.WriteAllBytes(Path.Combine(edge, "4096-a.bin"), Bytes(4096, 9));
    File.WriteAllBytes(Path.Combine(edge, "4096-b.bin"), Bytes(4096, 9));
    File.WriteAllBytes(Path.Combine(edge, "4097-a.bin"), Bytes(4097, 1)); // 앞 4096바이트는 같고 마지막 1바이트만 다르다
    File.WriteAllBytes(Path.Combine(edge, "4097-b.bin"), Bytes(4097, 2));
    var edgeFiles = (await new FileScanner().ScanAsync(ScanFilter.Empty with { IncludePaths = [edge], ExcludeSystemFiles = false }, new Progress<ScanProgress>())).Files;
    var (edgeGroups, _, _) = await detector.DetectAsync(edgeFiles, new Progress<ScanProgress>());
    Check($"5 exactly 4KB duplicates are found and 4KB+1 files differing in the last byte are not ({string.Join(" | ", edgeGroups.Select(g => string.Join(",", g.Files.Select(f => f.FileName))))})",
        edgeGroups.Count == 1 && edgeGroups[0].Files.All(f => f.FileName.StartsWith("4096")) && edgeGroups[0].Hash == HashOf(Path.Combine(edge, "4096-a.bin")));

    // ── 3단계: 제외 경로는 폴더 경계로(I8) ────────────────────────────
    var exRoot = Path.Combine(dir, "exclude");
    foreach (var (folder, name) in new[] { ("data", "a.bin"), ("database", "b.bin") })
    {
        Directory.CreateDirectory(Path.Combine(exRoot, folder));
        File.WriteAllBytes(Path.Combine(exRoot, folder, name), new byte[10]);
    }
    var exFound = (await new FileScanner().ScanAsync(ScanFilter.Empty with
    {
        IncludePaths = [exRoot], ExcludePaths = [Path.Combine(exRoot, "data") + "\\"], ExcludeSystemFiles = false,
    }, new Progress<ScanProgress>())).Files;
    Check($"I8 excluding a folder does not exclude a sibling with a longer name (found={string.Join(",", exFound.Select(f => f.FileName))})",
        exFound.Select(f => f.FileName).SequenceEqual(["b.bin"]));

    // ── 3단계: 휴지통에 넣을 수 없는 위치(I6) ──────────────────────────
    // 로컬 디스크를 관리 공유(\\localhost\X$)로 열면 네트워크 경로라 휴지통에 들어가지 않는다.
    var unc = $@"\\localhost\{dir[0]}${dir[2..]}";
    if (Directory.Exists(unc))
    {
        s = Setup("unc", 2, true);
        var real = new ResultsViewModel(null!, new CleanupOrchestrator(new FileOperationService(), new FileVerifier(), log))
        {
            ConfirmDeleteAsync = _ => Task.FromResult(true),
            QuarantinePath = Path.Combine(dir, "quarantine"),
        };
        var uncCopies = new[] { s.Copies[0] with { FullPath = Path.Combine(unc, s.Copies[0].FileName) }, s.Copies[1] };
        real.LoadJob(new ScanJob("unc", null!) { Result = new ScanResult([new DuplicateGroup(HashOf(s.Copies[0].FullPath), uncCopies)], 2, 50, TimeSpan.Zero, []) });
        await Delete(real, 0);
        Check($"I6 a file that cannot go to the recycle bin is not deleted (exists={File.Exists(s.Copies[0].FullPath)}, status={F(real, 0).StatusMessage})",
            File.Exists(s.Copies[0].FullPath) && F(real, 0).StatusMessage == CleanupOrchestrator.NotRecyclableMessage);
    }
    else
    {
        Console.WriteLine($"SKIP I6 recycle bin refusal ({unc} not reachable)");
    }

    // ── 3단계: 같은 실체의 폴더 거부·작업 기록·되돌리기(I14) ──────────
    s = Setup("alias-folder", 2, true);
    var dirAlias = Path.Combine(Path.GetDirectoryName(dir)!, $"alias-{Guid.NewGuid():N}");
    Mklink($"/J \"{dirAlias}\" \"{dir}\"");
    s.Vm.MoveToPath = dirAlias;
    await F(s.Vm, 0).SetActionMoveCommand.ExecuteAsync(null);
    Check($"I14 moving into the file's own folder through a junction is refused (junction={Directory.Exists(dirAlias)}, status={F(s.Vm, 0).StatusMessage})",
        Directory.Exists(dirAlias) && F(s.Vm, 0).Outcome == ActionOutcome.Failed && File.Exists(s.Copies[0].FullPath));
    if (Directory.Exists(dirAlias)) Directory.Delete(dirAlias);

    s = Setup("logged", 2, true);
    var logBefore = log.Entries.Count;
    await Delete(s.Vm, 0);
    var logged = log.Entries.Skip(logBefore).ToList();
    Check($"I14 every action is written to the cleanup log ({string.Join(",", logged.Select(e => $"{e.Action}/{e.Outcome}"))})",
        logged.Count == 1 && logged[0].Action == "Delete" && logged[0].Outcome == "Succeeded" && logged[0].OriginalPath == s.Copies[0].FullPath);

    s = Setup("restore", 2, true);
    await F(s.Vm, 0).SetActionQuarantineCommand.ExecuteAsync(null);
    var quarantined = F(s.Vm, 0).CurrentPath;
    var canRestore = F(s.Vm, 0).CanRestore;
    await F(s.Vm, 0).RestoreCommand.ExecuteAsync(null);
    Check($"I14 a quarantined file can be restored to its original path (canRestore={canRestore}, back={File.Exists(s.Copies[0].FullPath)}, status={F(s.Vm, 0).StatusMessage})",
        canRestore && File.Exists(s.Copies[0].FullPath) && !File.Exists(quarantined) && !F(s.Vm, 0).IsDone && !F(s.Vm, 0).CanRestore &&
        log.Entries[^1].Action == "Restore");

    s = Setup("restore-blocked", 2, true);
    await F(s.Vm, 0).SetActionQuarantineCommand.ExecuteAsync(null);
    File.WriteAllText(s.Copies[0].FullPath, "new file at the old path");
    await F(s.Vm, 0).RestoreCommand.ExecuteAsync(null);
    Check($"I14 restore never overwrites a file at the original path (status={F(s.Vm, 0).StatusMessage})",
        F(s.Vm, 0).Outcome == ActionOutcome.Failed && File.ReadAllText(s.Copies[0].FullPath) == "new file at the old path" &&
        File.Exists(F(s.Vm, 0).CurrentPath));

    using var cancelled = new CancellationTokenSource();
    cancelled.Cancel();
    try
    {
        await detector.DetectAsync(files, new Progress<ScanProgress>(), cancelled.Token);
        Check("cancellation propagates", false);
    }
    catch (OperationCanceledException) { Check("cancellation propagates", true); }
}
finally
{
    Directory.Delete(dir, true);
    if (otherVolumeDir is not null && Directory.Exists(otherVolumeDir))
        Directory.Delete(otherVolumeDir, true);
}
Console.WriteLine(failures == 0 ? "ALL PASSED" : $"{failures} FAILED");
return failures;

// 검사 폴더와 다른 볼륨의 임시 폴더. 없으면 null.
static string? OtherVolumeDir(string dir)
{
    var root = Path.GetPathRoot(dir);
    foreach (var baseDir in new[] { Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"C:\Windows\Temp" })
    {
        if (string.IsNullOrEmpty(baseDir) || string.Equals(Path.GetPathRoot(baseDir), root, StringComparison.OrdinalIgnoreCase))
            continue;
        var candidate = Path.Combine(baseDir, "Temp", $"dupe-checks-{Guid.NewGuid():N}");
        try { Directory.CreateDirectory(candidate); return candidate; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { }
    }
    return null;
}

/// <summary>작업 기록을 파일 대신 메모리에 모은다.</summary>
sealed class MemoryLog : ICleanupLog
{
    public List<CleanupLogEntry> Entries { get; } = [];
    public void Record(CleanupLogEntry entry) { lock (Entries) Entries.Add(entry); }
}

/// <summary>휴지통 대신 임시 폴더의 파일을 바로 지운다. 이동·격리·되돌리기는 실제 구현을 쓴다.</summary>
sealed class TempDelete(string root) : IFileOperationService
{
    private readonly FileOperationService _real = new();

    public TaskCompletionSource? Gate { get; set; }
    public Action<string>? BeforeDelete { get; set; }
    public bool ThrowCancel { get; set; }
    public int Deleted { get; private set; }
    public int Calls { get; private set; }

    public async Task<RecycleOutcome> MoveToRecycleBinAsync(string filePath, CancellationToken ct = default)
    {
        if (!filePath.StartsWith(root)) throw new InvalidOperationException(filePath);
        Calls++;
        if (Gate is not null) await Gate.Task;
        BeforeDelete?.Invoke(filePath);
        if (ThrowCancel) throw new OperationCanceledException(); // 작업이 중단된 경우
        if (!File.Exists(filePath)) return RecycleOutcome.Missing; // 실제 서비스와 같다
        File.Delete(filePath);
        Deleted++;
        return RecycleOutcome.Recycled;
    }

    public Task<string?> TransferAsync(string filePath, string destinationFolder, bool markAsQuarantine, CancellationToken ct = default) =>
        _real.TransferAsync(filePath, destinationFolder, markAsQuarantine, ct);

    public Task<bool> RestoreAsync(string currentPath, string originalPath, CancellationToken ct = default) =>
        _real.RestoreAsync(currentPath, originalPath, ct);

    public Task<int> DeleteEmptyFoldersAsync(string rootPath, CancellationToken ct = default) => throw new NotSupportedException();
}
