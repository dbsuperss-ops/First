using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Application;

/// <summary>
/// 동의한 <see cref="GroupPlan"/>을 실행한다. 모든 삭제·이동·격리는 여기를 지난다.
/// 최우선 규칙: 어떤 조작을 해도 그룹의 마지막 사본은 사라지지 않는다.
/// </summary>
public sealed class CleanupOrchestrator
{
    public const string LastCopyMessage = "남는 사본이 없어 삭제하지 않음";
    public const string UnknownIdentityMessage = "같은 파일인지 확인할 수 없어 삭제하지 않음";
    public const string ChangedMessage = "내용이 검사 때와 달라 삭제하지 않음";

    private readonly IFileOperationService _fileOp;
    private readonly IFileVerifier _verifier;

    public CleanupOrchestrator(IFileOperationService fileOp, IFileVerifier verifier)
    {
        _fileOp = fileOp;
        _verifier = verifier;
    }

    /// <summary>계획의 작업마다 결과를 하나씩 돌려준다. 취소되면 남은 작업은 "취소됨"이다.</summary>
    public async Task<IReadOnlyList<ActionResult>> ExecuteAsync(GroupPlan plan, CancellationToken ct = default)
    {
        var results = new List<ActionResult>();
        var pending = plan.Actions.Where(a => a.Action != FileAction.Keep).ToList();
        var candidates = plan.Candidates.Where(c => !pending.Any(a => SamePath(a.File, c))).ToList();

        // 1. 이동: 원래 위치에 사본을 남기지 않아도 된다. 옮긴 파일은 옮긴 위치에서 다시 확인될 때만 남는 사본이 된다.
        foreach (var move in pending.Where(a => a.Action == FileAction.MoveToFolder))
        {
            var result = await TransferAsync(move, plan, quarantine: false, ct);
            results.Add(result);
            candidates.Add(result.NewPath is { } moved ? move.File with { FullPath = moved } : move.File);
        }

        // 2. 삭제·격리: 이 작업 밖에, 검사 때와 내용이 같은 사본이 남아 있어야 한다.
        var removals = pending.Where(a => a.Action is FileAction.Delete or FileAction.Quarantine).ToList();
        if (removals.Count == 0)
            return results;

        var present = new List<PlannedAction>();
        var removalIds = new HashSet<FileIdentity>();
        foreach (var removal in removals)
        {
            if (!_verifier.Exists(removal.File.FullPath))
            {
                results.Add(new(removal.File, ActionOutcome.Skipped, "건너뜀: 파일이 없음"));
                continue;
            }
            using var probe = _verifier.TryOpenLocked(removal.File.FullPath, allowDelete: true);
            if (probe?.Identity is not { } id)
            {
                // 같은 실제 파일인지 모르면 남는 사본을 셀 수 없다. 이 그룹의 삭제·격리를 모두 보류한다.
                var reported = results.Select(r => r.File).ToHashSet();
                return [.. results, .. Hold(removals.Where(r => !reported.Contains(r.File)), UnknownIdentityMessage)];
            }
            removalIds.Add(id);
            present.Add(removal);
        }
        if (present.Count == 0)
            return results;

        ILockedFile? keeper;
        try
        {
            keeper = await FindSurvivorAsync(candidates, removalIds, plan.ContentHash, ct);
        }
        catch (OperationCanceledException)
        {
            return [.. results, .. present.Select(Cancelled)];
        }

        using (keeper)
        {
            if (keeper is null)
                return [.. results, .. Hold(present, LastCopyMessage)];

            // 남는 사본은 이 블록이 끝날 때까지 쓰기·삭제를 막은 채 열려 있다.
            foreach (var removal in present)
            {
                results.Add(ct.IsCancellationRequested
                    ? Cancelled(removal)
                    : removal.Action == FileAction.Delete
                        ? await DeleteAsync(removal, plan.ContentHash, ct)
                        : await TransferAsync(removal, plan, quarantine: true, ct));
            }
        }

        return results;
    }

    /// <summary>쓰기·삭제를 막은 채 열고, 지울 파일과 다른 실제 파일이며 내용이 검사 때와 같은 후보를 찾는다.</summary>
    private async Task<ILockedFile?> FindSurvivorAsync(
        IEnumerable<FileEntry> candidates, IReadOnlySet<FileIdentity> removalIds, string contentHash, CancellationToken ct)
    {
        foreach (var candidate in candidates)
        {
            var locked = _verifier.TryOpenLocked(candidate.FullPath, allowDelete: false);
            if (locked is null)
                continue;
            try
            {
                if (locked.Identity is { } id && !removalIds.Contains(id) &&
                    await Task.Run(() => locked.ComputeHash(ct), ct) == contentHash)
                    return locked;
            }
            catch
            {
                locked.Dispose();
                throw;
            }
            locked.Dispose();
        }
        return null;
    }

    private async Task<ActionResult> DeleteAsync(PlannedAction removal, string contentHash, CancellationToken ct)
    {
        var path = removal.File.FullPath;
        // 대상도 쓰기를 막은 채 열고, 그 핸들로 내용을 확인한 뒤 연 채로 지운다. 확인한 파일과 지운 파일이 같아진다.
        using var target = _verifier.TryOpenLocked(path, allowDelete: true);
        if (target is null)
            return _verifier.Exists(path)
                ? new(removal.File, ActionOutcome.Held, "파일을 열 수 없어 삭제하지 않음")
                : new(removal.File, ActionOutcome.Skipped, "건너뜀: 파일이 없음");

        try
        {
            if (await Task.Run(() => target.ComputeHash(ct), ct) != contentHash)
                return new(removal.File, ActionOutcome.Held, ChangedMessage);

            if (!await _fileOp.MoveToRecycleBinAsync(path, ct))
                return new(removal.File, ActionOutcome.Skipped, "건너뜀: 파일이 없음");
        }
        catch (OperationCanceledException)
        {
            return new(removal.File, ActionOutcome.Cancelled, ct.IsCancellationRequested ? "취소됨" : "중단됨");
        }
        catch (Exception ex)
        {
            return new(removal.File, ActionOutcome.Failed, $"실패: {ex.Message}");
        }

        var now = target.CurrentPath();
        return now is not null && now.Contains(@"\$RECYCLE.BIN\", StringComparison.OrdinalIgnoreCase)
            ? new(removal.File, ActionOutcome.Succeeded, "완료")
            : new(removal.File, ActionOutcome.Succeeded, "완료 (휴지통에서 찾지 못함: 영구 삭제되었을 수 있음)");
    }

    private async Task<ActionResult> TransferAsync(PlannedAction action, GroupPlan plan, bool quarantine, CancellationToken ct)
    {
        if (ct.IsCancellationRequested)
            return Cancelled(action);

        var folder = quarantine ? plan.QuarantineFolder : action.Destination;
        if (DestinationProblem(action.File.FullPath, folder, quarantine ? null : plan.QuarantineFolder) is { } problem)
            return new(action.File, ActionOutcome.Failed, $"실패: {problem}");

        try
        {
            var newPath = await _fileOp.TransferAsync(action.File.FullPath, Path.GetFullPath(folder), quarantine, ct);
            return newPath is null
                ? new(action.File, ActionOutcome.Skipped, "건너뜀: 파일이 없음")
                : new(action.File, ActionOutcome.Succeeded, "완료", newPath);
        }
        catch (OperationCanceledException)
        {
            return new(action.File, ActionOutcome.Cancelled, ct.IsCancellationRequested ? "취소됨" : "중단됨");
        }
        catch (Exception ex)
        {
            return new(action.File, ActionOutcome.Failed, $"실패: {ex.Message}");
        }
    }

    private static string? DestinationProblem(string filePath, string folder, string? quarantineFolder)
    {
        if (string.IsNullOrWhiteSpace(folder))
            return "옮길 폴더가 지정되지 않음";
        try
        {
            var target = Normalize(folder);
            // 원본과 같은 폴더로 옮기면 이름만 바뀌고 중복은 그대로다.
            if (string.Equals(target, Normalize(Path.GetDirectoryName(filePath)!), StringComparison.OrdinalIgnoreCase))
                return "원본과 같은 폴더로는 옮기지 않음";
            // 격리 폴더의 파일은 검사에서 빠지므로, 이동 목적지로 쓰면 이동한 파일이 보이지 않게 된다.
            if (quarantineFolder is not null && !string.IsNullOrWhiteSpace(quarantineFolder) &&
                string.Equals(target, Normalize(quarantineFolder), StringComparison.OrdinalIgnoreCase))
                return "이동 폴더가 격리 폴더와 같음";
            return null;
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return $"폴더 경로가 올바르지 않음 ({ex.Message})";
        }
    }

    private static string Normalize(string folder) =>
        Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);

    private static bool SamePath(FileEntry a, FileEntry b) =>
        string.Equals(a.FullPath, b.FullPath, StringComparison.OrdinalIgnoreCase);

    private static IEnumerable<ActionResult> Hold(IEnumerable<PlannedAction> actions, string reason) =>
        actions.Select(a => new ActionResult(a.File, ActionOutcome.Held, reason));

    private static ActionResult Cancelled(PlannedAction action) =>
        new(action.File, ActionOutcome.Cancelled, "취소됨");
}
