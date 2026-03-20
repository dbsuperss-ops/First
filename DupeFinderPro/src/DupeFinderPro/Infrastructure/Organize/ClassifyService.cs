using System.Text.RegularExpressions;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Infrastructure.Organize;

public sealed class ClassifyService : IClassifyService
{
    private readonly IOrganizeLogRepository _logRepo;
    private readonly IClassifyRecordRepository _recordRepo;

    private static readonly string[] SystemDirs =
        [@"\Windows\", @"\Program Files\", @"\Program Files (x86)\", @"\ProgramData\", @"\AppData\"];

    private static readonly string[] DatePatterns =
    [
        @"(?<!\d)((?:19|20)\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)",
        @"(?<!\d)((?:19|20)\d{2})[-._](0[1-9]|1[0-2])[-._](0[1-9]|[12]\d|3[01])(?!\d)"
    ];

    public ClassifyService(IOrganizeLogRepository logRepo, IClassifyRecordRepository recordRepo)
    {
        _logRepo = logRepo;
        _recordRepo = recordRepo;
    }

    public Task<IReadOnlyList<ClassifyResult>> PreviewAsync(Scenario scenario, CancellationToken ct = default)
        => Task.Run(() => Preview(scenario, ct), ct);

    public Task<int> ExecuteAsync(IReadOnlyList<ClassifyResult> results, Scenario scenario,
        IProgress<int>? progress = null, CancellationToken ct = default)
        => Task.Run(() => Execute(results, scenario, progress, ct), ct);

    public bool MatchesRule(FileInfo file, ClassifyRule rule)
    {
        if (rule.Conditions.Count == 0) return false;
        var evaluations = rule.Conditions.Select(c => Eval(file, c)).ToList();
        return rule.Logic == ConditionLogic.And ? evaluations.All(x => x) : evaluations.Any(x => x);
    }

    public string BuildTargetPath(FileInfo file, ClassifyRule rule, string baseTarget)
    {
        string baseDir = rule.DestinationMode switch
        {
            DestinationMode.Custom => rule.Destination,
            DestinationMode.Absolute => rule.Destination,
            _ => baseTarget
        };

        if (rule.DestinationMode == DestinationMode.Absolute)
            return Path.Combine(baseDir, file.Name);

        string path = rule.TargetPath;
        string nameNoExt = Path.GetFileNameWithoutExtension(file.Name);

        path = path.Replace("{ext}", file.Extension.TrimStart('.').ToUpper());

        if (TryExtractDate(nameNoExt, out int fy, out int fmo, out int fd))
        {
            string ys = fy.ToString(), ms = fmo.ToString("D2"), ds = fd.ToString("D2");
            path = path.Replace("{year}", ys).Replace("{년}", ys)
                       .Replace("{month}", ms).Replace("{day}", ds).Replace("{일}", ds);
            path = path.Replace("{monthname}", fmo + "월").Replace("{월}", fmo + "월");
        }
        else
        {
            var dt = file.LastWriteTime;
            path = path.Replace("{year}", dt.ToString("yyyy")).Replace("{년}", dt.ToString("yyyy"))
                       .Replace("{month}", dt.ToString("MM")).Replace("{day}", dt.ToString("dd")).Replace("{일}", dt.ToString("dd"));
            path = path.Replace("{monthname}", dt.Month + "월").Replace("{월}", dt.Month + "월");
        }

        var keyMatch = Regex.Match(nameNoExt, @"^([A-Za-z가-힣]+)");
        if (keyMatch.Success) path = path.Replace("{keyword}", keyMatch.Groups[1].Value);
        path = path.Replace("{size}", file.Length > 104857600 ? "대용량" : file.Length > 10485760 ? "중간" : "소용량");

        var result = Path.Combine(baseDir, path, file.Name);

        // Guard: reject path traversal — fall back to flat placement under baseDir
        var normalizedResult = Path.GetFullPath(result);
        var normalizedBase = Path.GetFullPath(baseDir);
        if (!normalizedResult.StartsWith(normalizedBase, StringComparison.OrdinalIgnoreCase))
            return Path.Combine(normalizedBase, file.Name);

        return normalizedResult;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────────

    private IReadOnlyList<ClassifyResult> Preview(Scenario scenario, CancellationToken ct)
    {
        var results = new List<ClassifyResult>();
        if (string.IsNullOrEmpty(scenario.SourceFolder) || !Directory.Exists(scenario.SourceFolder))
            return results;

        var enumOpt = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = scenario.IncludeSubfolders
        };

        string[] files;
        try { files = Directory.GetFiles(scenario.SourceFolder, "*", enumOpt); }
        catch { return results; }

        foreach (var file in files)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fi = new FileInfo(file);
                if (scenario.ExcludeSystemFiles && IsSystemFile(fi, file)) continue;

                foreach (var rule in scenario.Rules)
                {
                    if (!MatchesRule(fi, rule)) continue;
                    results.Add(new ClassifyResult(fi.Name, file, BuildTargetPath(fi, rule, scenario.TargetFolder), rule.RuleName, fi.Length));
                    break;
                }
            }
            catch { /* skip inaccessible files */ }
        }

        return results;
    }

    private int Execute(IReadOnlyList<ClassifyResult> results, Scenario scenario,
        IProgress<int>? progress, CancellationToken ct)
    {
        int count = 0;
        var batchId = Guid.NewGuid();
        var logs = new List<OrganizeLogEntry>();
        var movedFrom = new HashSet<string>();
        var fileMoves = new List<FileMove>();

        foreach (var r in results)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var fiAttr = new FileInfo(r.SourcePath);
                if (!fiAttr.Exists) continue;
                if (scenario.ExcludeSystemFiles && IsSystemFile(fiAttr, r.SourcePath)) continue;

                var dir = Path.GetDirectoryName(r.TargetPath);
                if (dir == null) continue;
                Directory.CreateDirectory(dir);

                string finalTarget = ResolveConflict(r.TargetPath, scenario.ConflictMode);
                if (string.IsNullOrEmpty(finalTarget)) continue;

                if (scenario.ConflictMode == ConflictMode.Overwrite && File.Exists(finalTarget))
                    File.Delete(finalTarget);

                File.Move(r.SourcePath, finalTarget);
                count++;
                progress?.Report(count);
                movedFrom.Add(Path.GetDirectoryName(r.SourcePath) ?? "");

                logs.Add(new OrganizeLogEntry(batchId, DateTime.Now, Path.GetFileName(finalTarget), r.SourcePath, finalTarget, "이동"));
                fileMoves.Add(new FileMove(r.SourcePath, finalTarget, r.FileName, r.RuleName));
            }
            catch { /* skip files that fail to move */ }
        }

        if (logs.Count > 0) _logRepo.AddRange(logs);

        if (count > 0)
        {
            long totalBytes = fileMoves.Select(f =>
            {
                try { return new FileInfo(f.NewPath).Length; } catch { return 0L; }
            }).Sum();

            _recordRepo.Add(new ClassifyRecord(Guid.NewGuid(), DateTime.Now, scenario.Name,
                scenario.SourceFolder, scenario.TargetFolder, count, totalBytes, fileMoves));
        }

        if (scenario.CleanupEmptyFolders)
        {
            foreach (var folder in movedFrom.Where(f => !string.IsNullOrEmpty(f)))
                try { DeleteIfEmpty(folder); } catch { }
        }

        return count;
    }

    private static bool IsSystemFile(FileInfo fi, string path)
    {
        if (fi.Attributes.HasFlag(FileAttributes.Hidden) || fi.Attributes.HasFlag(FileAttributes.System))
            return true;
        return SystemDirs.Any(d => path.Contains(d, StringComparison.OrdinalIgnoreCase));
    }

    private static string ResolveConflict(string targetPath, ConflictMode mode)
    {
        if (!File.Exists(targetPath)) return targetPath;
        if (mode == ConflictMode.Overwrite) return targetPath;
        if (mode == ConflictMode.Skip) return "";

        string dir = Path.GetDirectoryName(targetPath) ?? "";
        string name = Path.GetFileNameWithoutExtension(targetPath);
        string ext = Path.GetExtension(targetPath);
        int i = 1;
        string result;
        do { result = Path.Combine(dir, $"{name}_{i++}{ext}"); } while (File.Exists(result));
        return result;
    }

    private static void DeleteIfEmpty(string path, int depth = 0)
    {
        if (depth > 64 || !Directory.Exists(path)) return;
        foreach (var sub in Directory.GetDirectories(path)) DeleteIfEmpty(sub, depth + 1);
        if (!Directory.GetFiles(path).Any() && !Directory.GetDirectories(path).Any())
            Directory.Delete(path);
    }

    private static bool Eval(FileInfo f, FileCondition c) => c.Type switch
    {
        ConditionType.Extension => EvalExt(f, c),
        ConditionType.Keyword   => EvalKey(f, c),
        ConditionType.Size      => EvalSize(f, c),
        ConditionType.Date      => EvalDate(f, c),
        _                       => false
    };

    private static bool EvalExt(FileInfo f, FileCondition c)
    {
        var ext = f.Extension.ToLowerInvariant();
        var exts = c.Value.ToLowerInvariant().Split(',').Select(e => e.Trim()).ToList();
        return c.Operator switch
        {
            ConditionOperator.Equals or ConditionOperator.Contains
                => exts.Any(e => ext == e || ext == "." + e.TrimStart('.')),
            ConditionOperator.NotEquals or ConditionOperator.DoesNotContain
                => !exts.Any(e => ext == e || ext == "." + e.TrimStart('.')),
            _ => false
        };
    }

    private static bool EvalKey(FileInfo f, FileCondition c)
    {
        var name = Path.GetFileNameWithoutExtension(f.Name);
        return c.Operator switch
        {
            ConditionOperator.Contains      => name.Contains(c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.DoesNotContain => !name.Contains(c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.StartsWith    => name.StartsWith(c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.EndsWith      => name.EndsWith(c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Equals        => name.Equals(c.Value, StringComparison.OrdinalIgnoreCase),
            ConditionOperator.Regex         => SafeRegex(name, c.Value),
            _ => false
        };
    }

    private static bool SafeRegex(string input, string pattern)
    {
        try { return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase, TimeSpan.FromMilliseconds(100)); }
        catch { return false; }
    }

    private static bool EvalSize(FileInfo f, FileCondition c)
    {
        if (!double.TryParse(c.Value, out double threshold)) return false;
        double size = c.Unit switch
        {
            SizeUnit.KB => f.Length / 1024.0,
            SizeUnit.MB => f.Length / 1048576.0,
            SizeUnit.GB => f.Length / 1073741824.0,
            _           => f.Length
        };
        return c.Operator switch
        {
            ConditionOperator.GreaterThan => size > threshold,
            ConditionOperator.LessThan    => size < threshold,
            ConditionOperator.Equals      => Math.Abs(size - threshold) < 0.01,
            _ => false
        };
    }

    private static bool EvalDate(FileInfo f, FileCondition c)
    {
        var name = Path.GetFileNameWithoutExtension(f.Name);
        if (!TryExtractDate(name, out int year, out int month, out _)) return false;
        return c.Operator switch
        {
            ConditionOperator.Year  => year.ToString() == c.Value,
            ConditionOperator.Month => month.ToString() == c.Value || month.ToString("D2") == c.Value,
            _ => false
        };
    }

    private static bool TryExtractDate(string name, out int year, out int month, out int day)
    {
        year = month = day = 0;
        foreach (var pattern in DatePatterns)
        {
            var m = Regex.Match(name, pattern);
            if (!m.Success) continue;
            int y = int.Parse(m.Groups[1].Value);
            int mo = int.Parse(m.Groups[2].Value);
            int d = int.Parse(m.Groups[3].Value);
            try { _ = new DateTime(y, mo, d); }
            catch { continue; }
            year = y; month = mo; day = d;
            return true;
        }
        return false;
    }
}
