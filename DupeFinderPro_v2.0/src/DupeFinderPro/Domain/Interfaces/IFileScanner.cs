using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

/// <param name="UnreadablePaths">
/// 목록에는 있었지만 정보를 읽지 못한 파일. 접근할 수 없는 폴더는 열거 자체가 건너뛰므로 그 안의 파일은 여기에도 없다.
/// </param>
public sealed record ScanCollection(IReadOnlyList<FileEntry> Files, IReadOnlyList<string> UnreadablePaths);

public interface IFileScanner
{
    Task<ScanCollection> ScanAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default);
}
