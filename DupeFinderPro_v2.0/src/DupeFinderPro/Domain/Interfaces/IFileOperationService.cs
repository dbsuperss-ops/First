namespace DupeFinderPro.Domain.Interfaces;

public enum RecycleOutcome
{
    Recycled,
    /// <summary>파일이 없다.</summary>
    Missing,
    /// <summary>휴지통에 넣을 수 없는 위치라 지우지 않았다(네트워크 경로 등). 파일은 그대로 있다.</summary>
    NotRecyclable,
}

public interface IFileOperationService
{
    /// <summary>휴지통으로 보낸다. 휴지통에 넣을 수 없으면 영구 삭제하지 않고 <see cref="RecycleOutcome.NotRecyclable"/>을 돌려준다.</summary>
    Task<RecycleOutcome> MoveToRecycleBinAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// 폴더로 옮기고 파일의 실제 새 경로를 돌려준다. 파일이 없으면 null.
    /// 같은 볼륨은 이름 바꾸기 뒤 파일 ID를 확인하고, 다른 볼륨은 복사 → 내용 확인 → 원본 삭제 순서로 옮긴다.
    /// 실패·취소되면 원본은 그대로 남는다.
    /// <paramref name="markAsQuarantine"/>이면 폴더에 격리 표식(<see cref="Models.QuarantineFolder.MarkerFileName"/>)을 남긴다.
    /// </summary>
    Task<string?> TransferAsync(string filePath, string destinationFolder, bool markAsQuarantine, CancellationToken ct = default);

    /// <summary>
    /// 옮겼던 파일을 원래 경로로 되돌린다(같은 방식으로 확인하며 옮긴다). 현재 위치에 파일이 없으면 false.
    /// 원래 경로에 파일이 있으면 덮어쓰지 않고 실패한다.
    /// </summary>
    Task<bool> RestoreAsync(string currentPath, string originalPath, CancellationToken ct = default);

    Task<int> DeleteEmptyFoldersAsync(string rootPath, CancellationToken ct = default);
}
