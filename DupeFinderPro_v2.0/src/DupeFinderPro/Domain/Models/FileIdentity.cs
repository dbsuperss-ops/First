namespace DupeFinderPro.Domain.Models;

/// <summary>
/// 볼륨 일련번호와 파일 ID. 정션·매핑 드라이브·UNC·하드 링크처럼 경로가 달라도 실제 파일이 같으면 값이 같다.
/// </summary>
public readonly record struct FileIdentity(ulong VolumeSerial, ulong IdLow, ulong IdHigh);
