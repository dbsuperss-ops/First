namespace DupeFinderPro.Domain.Interfaces;

public interface IFileOperationService
{
    Task<bool> MoveToRecycleBinAsync(string filePath, CancellationToken ct = default);

    /// <summary>
    /// 폴더로 옮기고 파일의 실제 새 경로를 돌려준다. 파일이 없으면 null.
    /// 같은 볼륨은 이름 바꾸기 뒤 파일 ID를 확인하고, 다른 볼륨은 복사 → 내용 확인 → 원본 삭제 순서로 옮긴다.
    /// 실패·취소되면 원본은 그대로 남는다.
    /// <paramref name="markAsQuarantine"/>이면 폴더에 격리 표식(<see cref="Models.QuarantineFolder.MarkerFileName"/>)을 남긴다.
    /// </summary>
    Task<string?> TransferAsync(string filePath, string destinationFolder, bool markAsQuarantine, CancellationToken ct = default);

    Task<int> DeleteEmptyFoldersAsync(string rootPath, CancellationToken ct = default);
}
