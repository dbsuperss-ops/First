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
        // ReadAsync는 요청한 바이트 수를 한 번에 반환하지 않을 수 있으므로
        // EOF에 도달할 때까지 버퍼를 채운다
        int totalRead = 0;
        while (totalRead < PartialReadBytes)
        {
            int read = await fs.ReadAsync(buffer.AsMemory(totalRead, PartialReadBytes - totalRead), ct);
            if (read == 0) break;
            totalRead += read;
        }
        var hash = sha.ComputeHash(buffer, 0, totalRead);
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
