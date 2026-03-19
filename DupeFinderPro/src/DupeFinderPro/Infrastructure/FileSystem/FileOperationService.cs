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
}
