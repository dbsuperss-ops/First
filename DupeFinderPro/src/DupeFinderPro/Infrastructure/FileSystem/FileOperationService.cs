using DupeFinderPro.Domain.Interfaces;
using Microsoft.VisualBasic.FileIO;
using System.Runtime.InteropServices;

namespace DupeFinderPro.Infrastructure.FileSystem;

public sealed class FileOperationService : IFileOperationService
{
    public Task<bool> MoveToRecycleBinAsync(string filePath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
                return false;

            // Windows에서만 휴지통 사용, Linux/Mac에서는 .trash 폴더로 이동
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                try
                {
                    Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                        filePath,
                        UIOption.OnlyErrorDialogs,
                        RecycleOption.SendToRecycleBin);
                    return true;
                }
                catch
                {
                    // 실패시 일반 삭제
                    File.Delete(filePath);
                    return true;
                }
            }
            else
            {
                // Linux/Mac: ~/.local/share/Trash 또는 간단히 삭제
                var trashDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local", "share", "Trash", "files");

                try
                {
                    Directory.CreateDirectory(trashDir);
                    var fileName = Path.GetFileName(filePath);
                    var trashPath = Path.Combine(trashDir, fileName);

                    // 이름 충돌 방지
                    if (File.Exists(trashPath))
                    {
                        var nameNoExt = Path.GetFileNameWithoutExtension(fileName);
                        var ext = Path.GetExtension(fileName);
                        trashPath = Path.Combine(trashDir, $"{nameNoExt}_{DateTime.Now:yyyyMMddHHmmss}{ext}");
                    }

                    File.Move(filePath, trashPath);
                    return true;
                }
                catch
                {
                    // Trash 이동 실패시 직접 삭제
                    File.Delete(filePath);
                    return true;
                }
            }
        }, ct);
    }

    public Task<bool> MoveToFolderAsync(string filePath, string destinationFolder, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
                return false;

            Directory.CreateDirectory(destinationFolder);
            var dest = Path.Combine(destinationFolder, Path.GetFileName(filePath));

            // Avoid overwrite collision
            if (File.Exists(dest))
            {
                var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
                var ext = Path.GetExtension(filePath);
                dest = Path.Combine(destinationFolder, $"{nameNoExt}_{Guid.NewGuid():N}{ext}");
            }

            File.Move(filePath, dest);
            return true;
        }, ct);
    }
}
