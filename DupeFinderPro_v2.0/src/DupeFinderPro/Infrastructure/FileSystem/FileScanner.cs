using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Infrastructure.FileSystem;

public sealed class FileScanner : IFileScanner
{
    private static readonly string[] ProtectedSegments =
    [
        @"\Windows\", @"\Program Files\", @"\Program Files (x86)\", @"\ProgramData\",
        @"\AppData\", @"\$RECYCLE.BIN\", @"\System Volume Information\"
    ];

    // 설정과 무관하게 수집하지 않는다. 휴지통·격리 폴더 파일을 남는 사본으로 세면, 비울 때 마지막 사본이 사라진다.
    // 패키지·빌드 폴더와 Git 저장소는 일부러 같은 파일을 여러 곳에 두므로, 한쪽을 지우면 프로그램이 깨진다
    // (2026-11-26 실측: node_modules·.venv·uv 캐시의 사본과 저장소의 잠금 파일·그림이 지워져 빌드와 테스트가 깨졌다).
    private static readonly string[] AlwaysExcludedSegments =
    [
        @"\$RECYCLE.BIN\", @"\System Volume Information\",
        @"\.git\", @"\node_modules\", @"\.venv\", @"\venv\", @"\site-packages\", @"\__pycache__\", @"\.next\"
    ];
    private static readonly string DefaultQuarantinePrefix =
        QuarantineFolder.DefaultPath.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;

    public async Task<ScanCollection> ScanAsync(
        ScanFilter filter,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        var results = new List<FileEntry>();
        var unreadable = new List<string>();
        var allowedExtensions = filter.GetAllowedExtensions();

        // 제외 폴더는 수집 경로와 같은 형식(절대 경로, 끝 구분자 없음)으로 맞춰 폴더 단위로 비교한다.
        var excludedFolders = filter.ExcludePaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(NormalizeFolder)
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
            var blockedDirs = new Dictionary<string, bool>(StringComparer.OrdinalIgnoreCase);

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

                    if (!seen.Add(filePath) || IsAlwaysExcluded(filePath, blockedDirs))
                        continue;

                    try
                    {
                        var info = new FileInfo(filePath);

                        if (!PassesFilter(filePath, info, filter, allowedExtensions, excludedFolders))
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
                    catch (Exception ex) when (IsFileAccessError(ex))
                    {
                        // 정보를 읽지 못한 파일은 비교하지 못했다고 결과에 알린다. 그 밖의 예외는 버그이므로 숨기지 않는다.
                        unreadable.Add(filePath);
                    }
                }
            }

            progress.Report(new ScanProgress(ScanPhase.Collecting, results.Count, results.Count, 0));
        }, ct);

        return new ScanCollection(results, unreadable);
    }

    /// <summary>건너뛰어도 되는 파일 접근 오류. 그 밖의 예외는 작업 실패로 드러낸다.</summary>
    public static bool IsFileAccessError(Exception ex) =>
        ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or NotSupportedException;

    private static string NormalizeFolder(string folder)
    {
        try
        {
            return Path.GetFullPath(folder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return folder.TrimEnd('\\', '/');
        }
    }

    /// <summary>
    /// 휴지통·시스템 볼륨 정보·패키지 폴더, 격리 폴더(기본 경로 또는 격리 표식이 있는 폴더),
    /// Git 저장소(.git 폴더나 워크트리의 .git 파일이 있는 폴더)와 그 하위의 파일인지.
    /// </summary>
    private static bool IsAlwaysExcluded(string filePath, Dictionary<string, bool> blockedDirs)
    {
        if (AlwaysExcludedSegments.Any(seg => filePath.Contains(seg, StringComparison.OrdinalIgnoreCase)) ||
            filePath.StartsWith(DefaultQuarantinePrefix, StringComparison.OrdinalIgnoreCase))
            return true;

        for (var dir = Path.GetDirectoryName(filePath); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (!blockedDirs.TryGetValue(dir, out var marked))
            {
                var git = Path.Combine(dir, ".git");
                marked = File.Exists(Path.Combine(dir, QuarantineFolder.MarkerFileName)) ||
                         Directory.Exists(git) || File.Exists(git);
                blockedDirs[dir] = marked;
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
        IReadOnlyList<string> excludedFolders)
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

        // Exclude paths: 그 폴더 자체와 하위만. C:\Data를 제외해도 C:\Database는 검사한다.
        foreach (var excluded in excludedFolders)
        {
            if (fullPath.StartsWith(excluded + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
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
