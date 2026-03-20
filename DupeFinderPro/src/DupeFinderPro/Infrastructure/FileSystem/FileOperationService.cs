using DupeFinderPro.Domain.Interfaces;
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
