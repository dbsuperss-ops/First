using System.Security.Cryptography;
using DupeFinderPro.Domain.Interfaces;

namespace DupeFinderPro.Infrastructure.Hashing;

public sealed class HashingService : IHashingService
{
    private const int PartialReadBytes = 4096;

    public async Task<PartialHash> ComputePartialHashAsync(string filePath, CancellationToken ct = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, PartialReadBytes, useAsync: true);
        var buffer = new byte[PartialReadBytes];
        // ReadAsync는 요청한 바이트 수를 한 번에 반환하지 않을 수 있으므로
        // EOF에 도달할 때까지 버퍼를 채운다
        int totalRead = 0;
        var reachedEnd = false;
        while (totalRead < PartialReadBytes)
        {
            int read = await fs.ReadAsync(buffer.AsMemory(totalRead, PartialReadBytes - totalRead), ct);
            if (read == 0) { reachedEnd = true; break; }
            totalRead += read;
        }
        // 정확히 버퍼만큼 읽었으면 한 번 더 읽어 봐야 끝인지 안다. 이 바이트는 해시에 넣지 않는다.
        if (!reachedEnd)
            reachedEnd = await fs.ReadAsync(new byte[1], ct) == 0;

        var hash = SHA256.HashData(buffer.AsSpan(0, totalRead));
        return new PartialHash(Convert.ToHexString(hash), reachedEnd);
    }

    public async Task<string> ComputeFullHashAsync(string filePath, CancellationToken ct = default)
    {
        using var sha = SHA256.Create();
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 65536, useAsync: true);
        var hash = await sha.ComputeHashAsync(fs, ct);
        return Convert.ToHexString(hash);
    }
}
