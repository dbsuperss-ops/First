using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;
using System.Diagnostics;

namespace DupeFinderPro.Application;

public sealed class ScanOrchestrator
{
    private readonly IFileScanner _scanner;
    private readonly IDuplicateDetector _detector;

    public ScanOrchestrator(IFileScanner scanner, IDuplicateDetector detector)
    {
        _scanner = scanner;
        _detector = detector;
    }

    public async Task<ScanResult> RunAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        
        // 스캐너가 생성한 비동기 스트림을 그대로 탐지기에 전달
        var fileStream = _scanner.ScanAsync(filter, progress, ct);
        var (groups, totalFiles) = await _detector.DetectAsync(fileStream, progress, ct);
        
        sw.Stop();

        var wastedBytes = groups.Sum(g => g.WastedBytes);
        return new ScanResult(groups, totalFiles, wastedBytes, sw.Elapsed);
    }
}
