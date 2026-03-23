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
        var files = await _scanner.ScanAsync(filter, progress, ct);
        var groups = await _detector.DetectAsync(files, progress, ct);
        sw.Stop();

        var wastedBytes = groups.Sum(g => g.WastedBytes);
        return new ScanResult(groups, files.Count, wastedBytes, sw.Elapsed);
    }
}
