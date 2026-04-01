using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Application;

public sealed class CleanupOrchestrator
{
    private readonly IFileOperationService _fileOp;

    public CleanupOrchestrator(IFileOperationService fileOp)
    {
        _fileOp = fileOp;
    }

    public async Task<CleanupResult> DeleteAsync(
        IEnumerable<FileEntry> filesToDelete,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(filesToDelete, f => _fileOp.MoveToRecycleBinAsync(f.FullPath, ct), progress, ct);
    }

    public async Task<CleanupResult> MoveToFolderAsync(
        IEnumerable<FileEntry> filesToMove,
        string destinationFolder,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        return await ExecuteAsync(filesToMove, f => _fileOp.MoveToFolderAsync(f.FullPath, destinationFolder, ct), progress, ct);
    }

    private static async Task<CleanupResult> ExecuteAsync(
        IEnumerable<FileEntry> files,
        Func<FileEntry, Task<bool>> operation,
        IProgress<int>? progress,
        CancellationToken ct)
    {
        var fileList = files.ToList();
        var deletedCount = 0;
        var freedBytes = 0L;
        var errors = new List<string>();

        for (var i = 0; i < fileList.Count; i++)
        {
            ct.ThrowIfCancellationRequested();
            var file = fileList[i];

            try
            {
                var success = await operation(file);
                if (success)
                {
                    deletedCount++;
                    freedBytes += file.SizeBytes;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"{file.FullPath}: {ex.Message}");
            }

            progress?.Report(i + 1);
        }

        return new CleanupResult(deletedCount, freedBytes, errors);
    }
}
