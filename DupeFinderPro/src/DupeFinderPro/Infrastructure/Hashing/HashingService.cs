using System.Security.Cryptography;
using DupeFinderPro.Domain.Interfaces;

namespace DupeFinderPro.Infrastructure.Hashing;

public sealed class HashingService : IHashingService
{
    private const int PartialReadBytes = 4096;

    public async Task<string> ComputePartialHashAsync(string filePath, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, PartialReadBytes, useAsync: true);
        var buffer = new byte[PartialReadBytes];
        var read = await fs.ReadAsync(buffer, ct);
        var hash = sha.ComputeHash(buffer, 0, read);
        return Convert.ToHexString(hash);
    }

    public async Task<string> ComputeFullHashAsync(string filePath, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash);
    }
}
