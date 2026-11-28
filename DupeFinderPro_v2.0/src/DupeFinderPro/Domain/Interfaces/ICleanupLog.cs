namespace DupeFinderPro.Domain.Interfaces;

/// <param name="Action">Delete, MoveToFolder, Quarantine, Restore 중 하나.</param>
/// <param name="NewPath">이동·격리·되돌리기 뒤 파일의 실제 위치. 삭제면 null.</param>
public sealed record CleanupLogEntry(
    DateTime Time,
    string Action,
    string OriginalPath,
    string? NewPath,
    string ContentHash,
    string Outcome,
    string Message);

/// <summary>삭제·이동·격리의 결과를 남긴다. 앱을 닫은 뒤에도 파일이 어디로 갔는지 찾을 수 있게 한다.</summary>
public interface ICleanupLog
{
    void Record(CleanupLogEntry entry);
}
