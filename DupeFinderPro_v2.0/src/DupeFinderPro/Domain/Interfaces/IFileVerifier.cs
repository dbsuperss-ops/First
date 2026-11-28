using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Domain.Interfaces;

/// <summary>실행 직전에 파일을 잠그고 실제 내용과 ID를 확인한다.</summary>
public interface IFileVerifier
{
    bool Exists(string path);

    /// <summary>파일이나 폴더의 실제 ID. 경로가 달라도 같은 폴더인지 비교할 때 쓴다. 없거나 읽지 못하면 null.</summary>
    FileIdentity? IdentityOf(string path);

    /// <summary>
    /// 다른 프로세스의 쓰기를 막은 채 연다. <paramref name="allowDelete"/>가 false면 삭제·이름 바꾸기도 막는다.
    /// 열지 못하면 null.
    /// </summary>
    ILockedFile? TryOpenLocked(string path, bool allowDelete);
}

public interface ILockedFile : IDisposable
{
    FileIdentity? Identity { get; }

    /// <summary>이 핸들로 전체 내용을 읽어 SHA-256을 계산한다(검사 때와 같은 16진 문자열).</summary>
    string ComputeHash(CancellationToken ct);

    /// <summary>이 핸들이 지금 가리키는 파일의 경로. 지운 뒤 휴지통으로 갔는지 확인할 때 쓴다. 알 수 없으면 null.</summary>
    string? CurrentPath();
}
