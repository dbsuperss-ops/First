using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using Microsoft.Extensions.Logging;

namespace DupeFinderPro.Application;

public sealed class CleanupOrchestrator
{
    private readonly IFileOperationService _fileOp;
    private readonly ILogger<CleanupOrchestrator> _logger;

    public CleanupOrchestrator(IFileOperationService fileOp, ILogger<CleanupOrchestrator> logger)
    {
        _fileOp = fileOp;
        _logger = logger;
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

    private async Task<CleanupResult> ExecuteAsync(
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
                // 침묵하던 에러를 명시적으로 기록
                _logger.LogError(ex, "작업 수행 실패: {Path}", file.FullPath);
                errors.Add($"{file.FullPath}: {ex.Message}");
            }

            progress?.Report(i + 1);
        }

        _logger.LogInformation("정리 작업 완료 - 처리: {Count}건, 확보: {Bytes} Bytes, 오류: {ErrorCount}건", 
            deletedCount, freedBytes, errors.Count);

        return new CleanupResult(deletedCount, freedBytes, errors);
    }
}
