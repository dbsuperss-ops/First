using System.IO.Hashing;
using System.Security.Cryptography;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using Microsoft.Extensions.Options;

namespace DupeFinderPro.Infrastructure.Hashing;

public sealed class HashingService : IHashingService
{
    private readonly int _bufferSize;
    public HashingService(IOptions<DupeFinderOptions> options) => _bufferSize = options.Value.PartialHashBytes > 0 ? options.Value.PartialHashBytes : 4096;

    public async Task<string> ComputePartialHashAsync(string filePath, CancellationToken ct = default)
    {
        await using var fs = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, _bufferSize, useAsync: true);
        var buffer = new byte[_bufferSize];
        
        int totalRead = 0;
        while (totalRead < _bufferSize)
        {
            int read = await fs.ReadAsync(buffer.AsMemory(totalRead, _bufferSize - totalRead), ct);
            if (read == 0) break;
            totalRead += read;
        }

        // System.IO.Hashing의 초고속 알고리즘 적용
        var hash = XxHash64.Hash(buffer.AsSpan(0, totalRead));
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
