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

    // 설정과 무관하게 수집하지 않는다. 여기 있는 파일을 남는 사본으로 세면, 비울 때 마지막 사본이 사라진다.
    private static readonly string[] AlwaysExcludedSegments = [@"\$RECYCLE.BIN\", @"\System Volume Information\"];
    private static readonly string DefaultQuarantinePrefix =
        QuarantineFolder.DefaultPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public async Task<IReadOnlyList<FileEntry>> ScanAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        var results = new List<FileEntry>();
        var allowedExtensions = filter.GetAllowedExtensions();

        var excludedSegments = filter.ExcludePaths
            .Select(p => p.TrimEnd('\\', '/'))
            .ToList();

        await Task.Run(() =>
        {
            var paths = filter.IncludePaths
                .Select((p, i) => (Path: p, Priority: i))
                .Where(x => Directory.Exists(x.Path))
                .Select(x => (Path: Path.GetFullPath(x.Path), x.Priority))
                .ToList();

            // 검색 폴더가 겹쳐도 파일은 한 번만 수집한다. 두 번 수집하면 하나뿐인 파일이 자기 자신의 중복이 된다.
            // 우선순위가 높은 폴더부터 훑으므로 먼저 수집한 항목이 남는다.
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var quarantineDirs = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

            foreach (var (path, priority) in paths)
            {
                var options = new EnumerationOptions
                {
                    IgnoreInaccessible = true,
                    RecurseSubdirectories = filter.Recursive,
                    AttributesToSkip = FileAttributes.ReparsePoint
                };

                var enumerator = Directory.EnumerateFiles(path, "*", options);

                foreach (var filePath in enumerator)
                {
                    ct.ThrowIfCancellationRequested();

                    if (!seen.Add(filePath) || IsAlwaysExcluded(filePath, quarantineDirs))
                        continue;

                    try
                    {
                        var info = new FileInfo(filePath);

                        if (!PassesFilter(filePath, info, filter, allowedExtensions, excludedSegments))
                            continue;

                        results.Add(new FileEntry(
                            FullPath: filePath,
                            FileName: info.Name,
                            SizeBytes: info.Length,
                            LastModified: info.LastWriteTime,
                            CreatedAt: info.CreationTime,
                            SourcePriority: priority));

                        if (results.Count % 100 == 0)
                            progress.Report(new ScanProgress(ScanPhase.Collecting, results.Count, 0, 0, filePath));
                    }
                    catch
                    {
                        // skip inaccessible files
                    }
                }
            }

            progress.Report(new ScanProgress(ScanPhase.Collecting, results.Count, results.Count, 0));
        }, ct);

        return results;
    }

    /// <summary>휴지통·시스템 볼륨 정보·격리 폴더(기본 경로 또는 격리 표식이 있는 폴더와 그 하위) 안의 파일인지.</summary>
    private static bool IsAlwaysExcluded(string filePath, Dictionary<string, bool> quarantineDirs)
    {
        if (AlwaysExcludedSegments.Any(seg => filePath.Contains(seg, StringComparison.OrdinalIgnoreCase)) ||
            filePath.StartsWith(DefaultQuarantinePrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        for (var dir = Path.GetDirectoryName(filePath); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (!quarantineDirs.TryGetValue(dir, out var marked))
            {
                marked = File.Exists(Path.Combine(dir, QuarantineFolder.MarkerFileName));
                quarantineDirs[dir] = marked;
            }
            if (marked)
                return true;
        }
        return false;
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

        // File size. 빈 파일은 내용이 모두 같아 서로 무관한 표식 파일(.gitkeep 등)이 한 그룹이 되므로 기본 제외한다.
        if (info.Length == 0 && !filter.IncludeEmptyFiles)
            return false;
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
