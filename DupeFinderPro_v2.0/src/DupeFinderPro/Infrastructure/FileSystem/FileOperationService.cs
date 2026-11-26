using System.Security.Cryptography;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using Microsoft.VisualBasic.FileIO;

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

            Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                filePath,
                UIOption.OnlyErrorDialogs,
                RecycleOption.SendToRecycleBin);

            return true;
        }, ct);
    }

    public Task<string?> TransferAsync(string filePath, string destinationFolder, bool markAsQuarantine, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (!File.Exists(filePath))
                return null;

            Directory.CreateDirectory(destinationFolder);
            if (markAsQuarantine)
                EnsureQuarantineMarker(destinationFolder);

            var dest = FreeName(filePath, destinationFolder);
            var sourceId = FileIdentityReader.TryGet(filePath)
                           ?? throw new IOException("원본 파일 ID를 확인할 수 없어 옮기지 않음");
            var folderId = FileIdentityReader.TryGet(destinationFolder);

            if (folderId is { } f && f.VolumeSerial == sourceId.VolumeSerial)
            {
                // 같은 볼륨: 이름만 바뀌므로 내용은 그대로다. 옮긴 파일이 같은 파일인지만 확인한다.
                File.Move(filePath, dest);
                if (FileIdentityReader.TryGet(dest) != sourceId)
                    throw new IOException($"옮긴 뒤 같은 파일인지 확인하지 못함: {dest}");
                return dest;
            }

            CopyVerifiedThenRemove(filePath, dest, ct);
            return dest;
        }, ct);
    }

    /// <summary>다른 볼륨: 복사본의 내용을 원본과 비교한 뒤에만 원본을 지운다. 실패·취소되면 복사본을 지우고 원본을 남긴다.</summary>
    private static void CopyVerifiedThenRemove(string filePath, string dest, CancellationToken ct)
    {
        var partial = dest + ".dfp-partial";
        // 복사하는 동안 원본에 쓰지 못하게 한다. 삭제는 허용해야 확인 뒤 이 핸들을 연 채로 지울 수 있다.
        using var source = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete, 1, FileOptions.SequentialScan);
        try
        {
            string sourceHash;
            using (var output = new FileStream(partial, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var sha = IncrementalHash.CreateHash(HashAlgorithmName.SHA256))
            {
                var buffer = new byte[1 << 20];
                int read;
                while ((read = source.Read(buffer, 0, buffer.Length)) > 0)
                {
                    ct.ThrowIfCancellationRequested();
                    sha.AppendData(buffer, 0, read);
                    output.Write(buffer, 0, read);
                }
                output.Flush(flushToDisk: true);
                sourceHash = Convert.ToHexString(sha.GetHashAndReset());
            }

            string copyHash;
            using (var copy = new FileStream(partial, FileMode.Open, FileAccess.Read, FileShare.None, 1, FileOptions.SequentialScan))
                copyHash = Convert.ToHexString(SHA256.HashData(copy));

            if (copyHash != sourceHash)
                throw new IOException("복사본 내용이 원본과 달라 옮기지 않음");

            File.Move(partial, dest);
        }
        catch
        {
            try { File.Delete(partial); } catch (IOException) { }
            throw;
        }

        File.Delete(filePath);
    }

    private static void EnsureQuarantineMarker(string quarantineFolder)
    {
        // 격리본이 다음 검사에서 남는 사본으로 세어지면 원본이 삭제될 수 있다. 표식이 있는 폴더는 검사에서 뺀다.
        var marker = Path.Combine(quarantineFolder, QuarantineFolder.MarkerFileName);
        if (File.Exists(marker))
            return;
        File.WriteAllText(marker, "DupeFinderPro 격리 폴더입니다. 이 폴더는 중복 검사에서 제외됩니다.");
        File.SetAttributes(marker, FileAttributes.Hidden);
    }

    private static string FreeName(string filePath, string destinationFolder)
    {
        var dest = Path.Combine(destinationFolder, Path.GetFileName(filePath));

        // Avoid overwrite collision
        if (File.Exists(dest))
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            dest = Path.Combine(destinationFolder, $"{nameNoExt}_{Guid.NewGuid():N}{ext}");
        }

        return dest;
    }

    public Task<int> DeleteEmptyFoldersAsync(string rootPath, CancellationToken ct = default)
    {
        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();

            if (!Directory.Exists(rootPath))
                return 0;

            return DeleteEmptyRecursive(rootPath, ct);
        }, ct);
    }

    private static int DeleteEmptyRecursive(string path, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();

        int count = 0;
        try
        {
            foreach (var sub in Directory.GetDirectories(path))
                count += DeleteEmptyRecursive(sub, ct);

            // Delete if now empty (no files, no subdirectories)
            if (Directory.GetFiles(path).Length == 0 &&
                Directory.GetDirectories(path).Length == 0)
            {
                Directory.Delete(path);
                count++;
            }
        }
        catch { /* skip inaccessible or already-deleted folders */ }

        return count;
    }
}
