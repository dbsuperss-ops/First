namespace DupeFinderPro.Domain.Models;

/// <summary>
/// SkippedFiles: 읽을 수 없거나, 클라우드에만 있거나, 파일 ID를 확인하지 못해 비교하지 못한 파일.
/// MergedAliases: 먼저 수집한 항목과 같은 실제 파일(정션·매핑 드라이브·UNC·하드 링크)이라 한 번만 센 경로.
/// </summary>
public sealed record DetectionResult(
    IReadOnlyList<DuplicateGroup> Groups,
    IReadOnlyList<FileEntry> SkippedFiles,
    IReadOnlyList<FileEntry> MergedAliases);
