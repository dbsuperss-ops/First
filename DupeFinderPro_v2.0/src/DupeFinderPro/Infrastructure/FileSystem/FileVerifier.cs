using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using Microsoft.Win32.SafeHandles;

namespace DupeFinderPro.Infrastructure.FileSystem;

public sealed class FileVerifier : IFileVerifier
{
    public bool Exists(string path) => File.Exists(path);

    public ILockedFile? TryOpenLocked(string path, bool allowDelete)
    {
        try
        {
            // 쓰기는 항상 막는다. 지울 파일은 휴지통으로 보낼 수 있게 삭제만 허용한다(셸 삭제는 이 핸들을 연 채로도 된다).
            var share = allowDelete ? FileShare.Read | FileShare.Delete : FileShare.Read;
            var stream = new FileStream(path, FileMode.Open, FileAccess.Read, share, 1, FileOptions.SequentialScan);
            return new LockedFile(stream);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private sealed class LockedFile(FileStream stream) : ILockedFile
    {
        public FileIdentity? Identity { get; } = FileIdentityReader.TryGet(stream.SafeFileHandle);

        public string ComputeHash(CancellationToken ct)
        {
            using var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
            var buffer = new byte[1 << 20];
            stream.Position = 0;
            int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            {
                ct.ThrowIfCancellationRequested();
                sha.AppendData(buffer, 0, read);
            }
            return Convert.ToHexString(sha.GetHashAndReset());
        }

        public string? CurrentPath()
        {
            var sb = new StringBuilder(1024);
            var length = GetFinalPathNameByHandleW(stream.SafeFileHandle, sb, (uint)sb.Capacity, 0);
            return length == 0 || length > sb.Capacity ? null : sb.ToString();
        }

        public void Dispose() => stream.Dispose();
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint GetFinalPathNameByHandleW(SafeFileHandle file, StringBuilder path, uint length, uint flags);
}
