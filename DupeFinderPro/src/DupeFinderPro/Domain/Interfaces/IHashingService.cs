namespace DupeFinderPro.Domain.Interfaces;

public interface IHashingService
{
    Task<string> ComputePartialHashAsync(string filePath, CancellationToken ct = default);
    Task<string> ComputeFullHashAsync(string filePath, CancellationToken ct = default);
}
