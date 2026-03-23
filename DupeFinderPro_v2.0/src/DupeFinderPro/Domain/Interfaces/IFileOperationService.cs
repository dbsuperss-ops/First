namespace DupeFinderPro.Domain.Interfaces;

public interface IFileOperationService
{
    Task<bool> MoveToRecycleBinAsync(string filePath, CancellationToken ct = default);
    Task<bool> MoveToFolderAsync(string filePath, string destinationFolder, CancellationToken ct = default);
    Task<int> DeleteEmptyFoldersAsync(string rootPath, CancellationToken ct = default);
}
