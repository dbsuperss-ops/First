using System.Runtime.InteropServices;
using DupeFinderPro.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.VisualBasic.FileIO;

namespace DupeFinderPro.Infrastructure.FileSystem;

public sealed class FileOperationService : IFileOperationService
{
    private readonly ILogger<FileOperationService> _logger;
    private const int MaxRetries = 3;
    private const int RetryDelayMs = 500;

    public FileOperationService(ILogger<FileOperationService> logger)
    {
        _logger = logger;
    }

    public async Task<bool> MoveToRecycleBinAsync(string filePath, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("휴지통 이동 실패 (파일 없음): {FilePath}", filePath);
            return false;
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await Task.Run(() => Microsoft.VisualBasic.FileIO.FileSystem.DeleteFile(
                    filePath,
                    UIOption.OnlyErrorDialogs,
                    RecycleOption.SendToRecycleBin), ct);

                _logger.LogInformation("휴지통 이동 성공: {FilePath}", filePath);
                return true;
            }
            catch (IOException ex) when (IsFileLocked(ex))
            {
                _logger.LogWarning(ex, "파일 점유 감지 (시도 {Attempt}/{MaxRetries}): {FilePath}", attempt, MaxRetries, filePath);
                if (attempt == MaxRetries) throw;
                await Task.Delay(RetryDelayMs * attempt, ct); // 점진적 대기
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "휴지통 이동 중 치명적 오류: {FilePath}", filePath);
                throw;
            }
        }
        return false;
    }

    public async Task<bool> MoveToFolderAsync(string filePath, string destinationFolder, CancellationToken ct = default)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogWarning("이동 실패 (파일 없음): {FilePath}", filePath);
            return false;
        }

        Directory.CreateDirectory(destinationFolder);
        var dest = Path.Combine(destinationFolder, Path.GetFileName(filePath));

        if (File.Exists(dest))
        {
            var nameNoExt = Path.GetFileNameWithoutExtension(filePath);
            var ext = Path.GetExtension(filePath);
            dest = Path.Combine(destinationFolder, $"{nameNoExt}_{Guid.NewGuid():N}{ext}");
        }

        for (int attempt = 1; attempt <= MaxRetries; attempt++)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                await Task.Run(() => File.Move(filePath, dest), ct);
                _logger.LogInformation("파일 이동 성공: {Source} -> {Dest}", filePath, dest);
                return true;
            }
            catch (IOException ex) when (IsFileLocked(ex))
            {
                _logger.LogWarning(ex, "파일 점유 감지 (시도 {Attempt}/{MaxRetries}): {FilePath}", attempt, MaxRetries, filePath);
                if (attempt == MaxRetries) throw;
                await Task.Delay(RetryDelayMs * attempt, ct); // 점진적 대기
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "파일 이동 중 치명적 오류: {FilePath}", filePath);
                throw;
            }
        }
        return false;
    }

    public async Task<int> DeleteEmptyFoldersAsync(string rootPath, CancellationToken ct = default)
    {
        if (!Directory.Exists(rootPath))
        {
            _logger.LogWarning("빈 폴더 삭제 실패 (경로 없음): {RootPath}", rootPath);
            return 0;
        }

        try
        {
            int deletedCount = await Task.Run(() => DeleteEmptyRecursive(rootPath, ct), ct);
            _logger.LogInformation("빈 폴더 삭제 완료: {Count}개의 폴더 삭제됨 ({RootPath})", deletedCount, rootPath);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "빈 폴더 삭제 중 오류 발생: {RootPath}", rootPath);
            throw;
        }
    }

    private int DeleteEmptyRecursive(string path, CancellationToken ct)
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
                _logger.LogDebug("빈 폴더 삭제: {Path}", path);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "폴더 삭제 오류 또는 접근 불가: {Path}", path);
            // skip inaccessible or already-deleted folders
        }

        return count;
    }

    private static bool IsFileLocked(IOException exception)
    {
        int errorCode = Marshal.GetHRForException(exception) & ((1 << 16) - 1);
        return errorCode == 32 || errorCode == 33; // ERROR_SHARING_VIOLATION 또는 ERROR_LOCK_VIOLATION
    }
}
