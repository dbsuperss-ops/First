using System.Runtime.CompilerServices;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Infrastructure.FileSystem;

public sealed class FileScanner : IFileScanner
{
    private static readonly string[] ProtectedSegments =
    [
        @"\Windows\", @"\Program Files\", @"\Program Files (x86)\",
        @"\AppData\Local\Temp\", @"\$RECYCLE.BIN\", @"\System Volume Information\"
    ];

    public async IAsyncEnumerable<FileEntry> ScanAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var allowedExtensions = filter.GetAllowedExtensions();
        var excludedSegments = filter.ExcludePaths.Select(p => p.TrimEnd('\\', '/')).ToList();
        int count = 0;

        var paths = filter.IncludePaths
            .Select((p, i) => (Path: p, Priority: i))
            .Where(x => Directory.Exists(x.Path));

        foreach (var (path, priority) in paths)
        {
            var options = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = filter.Recursive,
                AttributesToSkip = FileAttributes.ReparsePoint
            };

            using var enumerator = Directory.EnumerateFiles(path, "*", options).GetEnumerator();
            bool hasNext = true;

            while (hasNext)
            {
                ct.ThrowIfCancellationRequested();
                string filePath = string.Empty;

                try
                {
                    hasNext = enumerator.MoveNext();
                    if (hasNext) filePath = enumerator.Current;
                }
                catch { continue; } // 접근 불가 폴더/파일 무시

                if (!hasNext) break;

                var info = new FileInfo(filePath);
                if (!PassesFilter(filePath, info, filter, allowedExtensions, excludedSegments))
                    continue;

                var entry = new FileEntry(
                    FullPath: filePath,
                    FileName: info.Name,
                    SizeBytes: info.Length,
                    LastModified: info.LastWriteTime,
                    CreatedAt: info.CreationTime,
                    SourcePriority: priority);

                count++;
                if (count % 100 == 0)
                    progress.Report(new ScanProgress(ScanPhase.Collecting, count, 0, 0, filePath));

                yield return entry; // 리스트에 담지 않고 즉시 반환
            }
        }

        progress.Report(new ScanProgress(ScanPhase.Collecting, count, count, 0));
    }

    private static bool PassesFilter(
        string fullPath,
        FileInfo info,
        ScanFilter filter,
        IReadOnlySet<string> allowedExtensions,
        IReadOnlyList<string> excludedSegments)
    {
        // System file protection
        if (filter.ExcludeSystemFiles)
        {
            if (info.Attributes.HasFlag(FileAttributes.Hidden) || info.Attributes.HasFlag(FileAttributes.System))
                return false;

            foreach (var seg in ProtectedSegments)
            {
                if (fullPath.Contains(seg, StringComparison.OrdinalIgnoreCase))
                    return false;
            }
        }

        // Exclude paths
        foreach (var excluded in excludedSegments)
        {
            if (fullPath.StartsWith(excluded, StringComparison.OrdinalIgnoreCase))
                return false;
        }

        // Exclude extensions
        var ext = info.Extension.ToLowerInvariant();
        if (filter.ExcludeExtensions.Any(e =>
            string.Equals(e.StartsWith('.') ? e : "." + e, ext, StringComparison.OrdinalIgnoreCase)))
            return false;

        // Include extensions / file types filter
        if (filter.HasFileTypeFilter && !allowedExtensions.Contains(ext))
            return false;

        // File size
        if (info.Length < filter.MinSizeBytes)
            return false;
        if (filter.MaxSizeBytes.HasValue && info.Length > filter.MaxSizeBytes.Value)
            return false;

        // Date filters
        if (filter.CreatedFrom.HasValue && info.CreationTime < filter.CreatedFrom.Value)
            return false;
        if (filter.CreatedTo.HasValue && info.CreationTime > filter.CreatedTo.Value)
            return false;
        if (filter.ModifiedFrom.HasValue && info.LastWriteTime < filter.ModifiedFrom.Value)
            return false;
        if (filter.ModifiedTo.HasValue && info.LastWriteTime > filter.ModifiedTo.Value)
            return false;

        // Include keywords (filename must contain at least one)
        if (filter.IncludeKeywords.Count > 0)
        {
            var name = info.Name;
            if (!filter.IncludeKeywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        // Exclude keywords (filename must not contain any)
        if (filter.ExcludeKeywords.Count > 0)
        {
            var name = info.Name;
            if (filter.ExcludeKeywords.Any(kw => name.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }
}
