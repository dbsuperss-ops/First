namespace DupeFinderPro.Domain.Interfaces;

/// <param name="Hash">앞부분의 SHA-256(16진 문자열).</param>
/// <param name="CoversWholeFile">
/// 읽다가 실제로 파일 끝에 닿았다. 이때 <paramref name="Hash"/>는 전체 해시와 같다.
/// 수집 때 기록한 크기가 아니라 실제로 읽은 결과로 판단한다.
/// </param>
public sealed record PartialHash(string Hash, bool CoversWholeFile);

public interface IHashingService
{
    Task<PartialHash> ComputePartialHashAsync(string filePath, CancellationToken ct = default);
    Task<string> ComputeFullHashAsync(string filePath, CancellationToken ct = default);
}
