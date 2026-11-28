namespace DupeFinderPro.Domain.Models;

/// <summary>
/// 격리 폴더 규칙. 격리본은 원래 위치의 남는 사본이 아니므로 검사 대상이 되면 안 된다.
/// 기본 경로와, 격리할 때 폴더에 남기는 표식 파일 이름이 있는 폴더는 설정과 무관하게 수집하지 않는다.
/// </summary>
public static class QuarantineFolder
{
    public const string MarkerFileName = ".dupefinderpro-quarantine";

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "DupeFinderPro", "Quarantine");
}
