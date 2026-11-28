using System.Runtime.InteropServices;
using DupeFinderPro.Domain.Models;
using Microsoft.Win32.SafeHandles;

namespace DupeFinderPro.Infrastructure.FileSystem;

public static class FileIdentityReader
{
    private const uint FileReadAttributes = 0x80;
    private const uint ShareAll = 0x1 | 0x2 | 0x4; // READ | WRITE | DELETE
    private const uint OpenExisting = 3;
    private const uint BackupSemantics = 0x02000000; // 폴더도 열 수 있게 한다(볼륨 비교용)
    private const int FileIdInfoClass = 18;

    /// <summary>실제 파일의 ID를 읽는다. 읽지 못하면 null이며, 호출부는 그 파일을 다른 파일과 다르다고 가정하면 안 된다.</summary>
    public static FileIdentity? TryGet(string path)
    {
        using var handle = CreateFileW(path, FileReadAttributes, ShareAll, IntPtr.Zero, OpenExisting, BackupSemantics, IntPtr.Zero);
        return handle.IsInvalid ? null : TryGet(handle);
    }

    /// <summary>이미 연 핸들의 파일 ID. 확인과 실행 사이에 파일이 바뀌지 않도록 같은 핸들로 읽을 때 쓴다.</summary>
    public static FileIdentity? TryGet(SafeFileHandle handle)
    {
        if (GetFileInformationByHandleEx(handle, FileIdInfoClass, out var idInfo, (uint)Marshal.SizeOf<FileIdInfo>()) &&
            (idInfo.IdLow != 0 || idInfo.IdHigh != 0))
            return new FileIdentity(idInfo.VolumeSerialNumber, idInfo.IdLow, idInfo.IdHigh);

        if (GetFileInformationByHandle(handle, out var info))
        {
            var index = ((ulong)info.FileIndexHigh << 32) | info.FileIndexLow;
            if (index != 0)
                return new FileIdentity(info.VolumeSerialNumber, index, 0);
        }

        return null;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct FileIdInfo
    {
        public ulong VolumeSerialNumber;
        public ulong IdLow;
        public ulong IdHigh;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 4)] // FILETIME은 4바이트 정렬
    private struct ByHandleFileInformation
    {
        public uint FileAttributes;
        public long CreationTime;
        public long LastAccessTime;
        public long LastWriteTime;
        public uint VolumeSerialNumber;
        public uint FileSizeHigh;
        public uint FileSizeLow;
        public uint NumberOfLinks;
        public uint FileIndexHigh;
        public uint FileIndexLow;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string fileName, uint desiredAccess, uint shareMode,
        IntPtr securityAttributes, uint creationDisposition, uint flagsAndAttributes, IntPtr templateFile);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandleEx(SafeFileHandle file, int infoClass, out FileIdInfo info, uint size);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetFileInformationByHandle(SafeFileHandle file, out ByHandleFileInformation info);
}
