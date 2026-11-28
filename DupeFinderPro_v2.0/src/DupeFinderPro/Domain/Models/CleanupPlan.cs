namespace DupeFinderPro.Domain.Models;

public enum FileAction { Keep, MoveToFolder, Quarantine, Delete }

/// <summary>처리 결과. 성공만 "완료"로 표시한다.</summary>
public enum ActionOutcome { None, Succeeded, Failed, Skipped, Held, Cancelled }

/// <summary>
/// 한 중복 그룹에 대해 사용자가 동의한 작업. 동의하기 전에 만들고, 동의한 뒤에는 이 계획만 실행한다.
/// </summary>
/// <param name="ContentHash">검사 때 계산한 그룹의 전체 해시. 삭제 직전에 남는 사본과 대상의 내용을 이 값과 비교한다.</param>
/// <param name="Candidates">남는 사본 후보: 지금 위치에 있는 그룹의 파일(이전에 옮긴 파일은 옮긴 위치).</param>
/// <param name="Actions">실행할 작업. 유지(Keep)는 넣지 않는다.</param>
/// <param name="QuarantineFolder">격리 폴더. 이동 목적지가 이 폴더면 거부한다.</param>
public sealed record GroupPlan(
    string ContentHash,
    IReadOnlyList<FileEntry> Candidates,
    IReadOnlyList<PlannedAction> Actions,
    string QuarantineFolder);

/// <param name="Destination">이동(MoveToFolder)일 때의 목적지 폴더. 그 밖에는 무시한다.</param>
public sealed record PlannedAction(FileEntry File, FileAction Action, string Destination);

/// <param name="NewPath">이동·격리에 성공했을 때 파일의 실제 위치.</param>
public sealed record ActionResult(FileEntry File, ActionOutcome Outcome, string Message, string? NewPath = null);
