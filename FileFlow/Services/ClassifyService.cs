using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using FileFlow.Models;

namespace FileFlow.Services
{
    public static class ClassifyService
    {
        public static List<ClassifyResult> Preview(Scenario scenario)
        {
            var results = new List<ClassifyResult>();
            if (string.IsNullOrEmpty(scenario.SourceFolder) || !Directory.Exists(scenario.SourceFolder)) return results;
            var enumOpt = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = scenario.IncludeSubfolders
            };
            string[] files;
            try { files = Directory.GetFiles(scenario.SourceFolder, "*", enumOpt); }
            catch (Exception ex) { ErrorService.Report(ex, "ClassifyService.Preview"); return results; }

            foreach (var file in files)
            {
                try
                {
                    if (scenario.ExcludeSystemFiles)
                    {
                        var fiAttr = new FileInfo(file);
                        if (fiAttr.Attributes.HasFlag(FileAttributes.Hidden) || fiAttr.Attributes.HasFlag(FileAttributes.System)) continue;
                        
                        string[] sysDirs = { @"\Windows\", @"\Program Files\", @"\Program Files (x86)\", @"\ProgramData\", @"\AppData\" };
                        if (sysDirs.Any(d => file.Contains(d, StringComparison.OrdinalIgnoreCase))) continue;
                    }
                    var fi = new FileInfo(file);
                    foreach (var rule in scenario.Rules)
                    {
                        if (MatchesRule(fi, rule))
                        {
                            results.Add(new ClassifyResult { FileName = fi.Name, SourcePath = file, TargetPath = BuildTargetPath(fi, rule, scenario.TargetFolder), RuleName = rule.RuleName, FileSize = fi.Length });
                            break;
                        }
                    }
                }
                catch (Exception ex) { ErrorService.Report(ex, $"ClassifyService.Preview: {file}"); }
            }
            return results;
        }

        public static Task<List<ClassifyResult>> PreviewAsync(Scenario s) => Task.Run(() => Preview(s));

        public static int Execute(List<ClassifyResult> results, Scenario scenario, IProgress<int>? progress = null)
        {
            int count = 0; var batchId = Guid.NewGuid(); var logs = new List<LogEntry>(); var movedFrom = new HashSet<string>();
            var record = new ClassifyRecord { ExecutedAt = DateTime.Now, ScenarioName = scenario.Name, SourceFolder = scenario.SourceFolder, TargetFolder = scenario.TargetFolder, Files = new() };

            foreach (var r in results)
            {
                try
                {
                    if (scenario.ExcludeSystemFiles)
                    {
                        var fiAttr = new FileInfo(r.SourcePath);
                        if (fiAttr.Exists && (fiAttr.Attributes.HasFlag(FileAttributes.Hidden) || fiAttr.Attributes.HasFlag(FileAttributes.System))) continue;

                        string[] sysDirs = { @"\Windows\", @"\Program Files\", @"\Program Files (x86)\", @"\ProgramData\", @"\AppData\" };
                        if (sysDirs.Any(d => r.SourcePath.Contains(d, StringComparison.OrdinalIgnoreCase))) continue;
                    }

                    var dir = Path.GetDirectoryName(r.TargetPath);
                    if (dir == null) continue; Directory.CreateDirectory(dir);

                    string ft = ResolveConflict(r.TargetPath, scenario.ConflictMode);
                    if (ft == "") continue;
                    if (scenario.ConflictMode == ConflictMode.Overwrite && File.Exists(ft)) File.Delete(ft);
                    File.Move(r.SourcePath, ft); count++; movedFrom.Add(Path.GetDirectoryName(r.SourcePath) ?? "");
                    progress?.Report(count);
                    logs.Add(new LogEntry { BatchId = batchId, Timestamp = DateTime.Now, FileName = Path.GetFileName(ft), SourcePath = r.SourcePath, TargetPath = ft, Action = "이동" });
                    record.Files.Add(new FileMove { OriginalPath = r.SourcePath, NewPath = ft, FileName = r.FileName, RuleName = r.RuleName });
                }
                catch (Exception ex) { ErrorService.Report(ex, $"ClassifyService.Execute: {r.SourcePath}"); }
            }

            if (logs.Count > 0) LogService.AddLogs(logs);
            record.FileCount = count;
            record.TotalBytes = record.Files.Select(f => { try { return new FileInfo(f.NewPath).Length; } catch { return 0L; } }).Sum();
            if (count > 0) RecordService.SaveRecord(record);

            if (scenario.CleanupEmptyFolders) foreach (var f in movedFrom.Where(f => !string.IsNullOrEmpty(f))) try { DeleteIfEmpty(f); } catch { }

            return count;
        }

        public static Task<int> ExecuteAsync(List<ClassifyResult> r, Scenario s, IProgress<int>? p = null) => Task.Run(() => Execute(r, s, p));

        private static string ResolveConflict(string t, ConflictMode m)
        {
            if (!File.Exists(t)) return t; if (m == ConflictMode.Overwrite) return t; if (m == ConflictMode.Skip) return "";
            string d = Path.GetDirectoryName(t) ?? "", n = Path.GetFileNameWithoutExtension(t), e = Path.GetExtension(t); int i = 1; string r;
            do { r = Path.Combine(d, $"{n}_{i++}{e}"); } while (File.Exists(r)); return r;
        }

        private static void DeleteIfEmpty(string p)
        {
            if (!Directory.Exists(p)) return; foreach (var s in Directory.GetDirectories(p)) DeleteIfEmpty(s);
            if (!Directory.GetFiles(p).Any() && !Directory.GetDirectories(p).Any()) Directory.Delete(p);
        }

        public static bool MatchesRule(FileInfo file, ClassifyRule rule)
        {
            if (rule.Conditions.Count == 0) return false;
            var r = rule.Conditions.Select(c => Eval(file, c)).ToList();
            return rule.ConditionOperator == "AND" ? r.All(x => x) : r.Any(x => x);
        }

        private static bool Eval(FileInfo f, FileCondition c) => c.Type switch { "Extension" => EvalExt(f, c), "Keyword" => EvalKey(f, c), "Size" => EvalSize(f, c), "Date" => EvalDate(f, c), _ => false };

        private static bool EvalExt(FileInfo f, FileCondition c)
        {
            var ext = f.Extension.ToLower(); var exts = c.Value.ToLower().Split(',').Select(e => e.Trim()).ToList();
            if (c.Operator == "DoesNotContain" || c.Operator == "NotEquals") return !exts.Any(e => ext == e || ext == "." + e.TrimStart('.'));
            return c.Operator == "Equals" ? exts.Any(e => ext == e || ext == "." + e.TrimStart('.')) : c.Operator == "Contains" ? exts.Any(e => ext.Contains(e.TrimStart('.'))) : false;
        }

        private static bool EvalKey(FileInfo f, FileCondition c)
        {
            var n = Path.GetFileNameWithoutExtension(f.Name);
            return c.Operator switch { "DoesNotContain" => !n.Contains(c.Value, StringComparison.OrdinalIgnoreCase), "Contains" => n.Contains(c.Value, StringComparison.OrdinalIgnoreCase), "StartsWith" => n.StartsWith(c.Value, StringComparison.OrdinalIgnoreCase), "EndsWith" => n.EndsWith(c.Value, StringComparison.OrdinalIgnoreCase), "Regex" => SafeRegex(n, c.Value), "Equals" => n.Equals(c.Value, StringComparison.OrdinalIgnoreCase), _ => false };
        }

        private static bool SafeRegex(string input, string pattern) { try { return Regex.IsMatch(input, pattern, RegexOptions.IgnoreCase); } catch { return false; } }

        private static bool EvalSize(FileInfo f, FileCondition c)
        {
            if (!double.TryParse(c.Value, out double v)) return false;
            double s = c.Unit switch { "KB" => f.Length / 1024.0, "MB" => f.Length / 1048576.0, "GB" => f.Length / 1073741824.0, _ => f.Length };
            return c.Operator switch { "GreaterThan" => s > v, "LessThan" => s < v, "Equals" => Math.Abs(s - v) < 0.01, _ => false };
        }

        private static readonly string[] DatePat = {
            @"(?<!\d)((?:19|20)\d{2})(0[1-9]|1[0-2])(0[1-9]|[12]\d|3[01])(?!\d)",
            @"(?<!\d)((?:19|20)\d{2})[-._](0[1-9]|1[0-2])[-._](0[1-9]|[12]\d|3[01])(?!\d)"
        };

        private static bool TryExtractDate(string name, out int year, out int month, out int day)
        {
            year = month = day = 0;
            foreach (var p in DatePat)
            {
                var m = Regex.Match(name, p);
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

        private static bool EvalDate(FileInfo f, FileCondition c)
        {
            var n = Path.GetFileNameWithoutExtension(f.Name);
            if (!TryExtractDate(n, out int y, out int mo, out _)) return false;
            if (c.Operator == "Year") return y.ToString() == c.Value;
            if (c.Operator == "Month") return mo.ToString() == c.Value || mo.ToString("D2") == c.Value;
            return false;
        }

        public static string BuildTargetPath(FileInfo file, ClassifyRule rule, string baseTgt)
        {
            string bd = rule.DestinationMode switch { "Custom" => rule.Destination, "Absolute" => rule.Destination, _ => baseTgt };
            if (rule.DestinationMode == "Absolute") return Path.Combine(bd, file.Name);

            string path = rule.TargetPath, fn = Path.GetFileNameWithoutExtension(file.Name);
            path = path.Replace("{ext}", file.Extension.TrimStart('.').ToUpper());
            if (TryExtractDate(fn, out int fy, out int fmo, out int fd))
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

            var km = Regex.Match(fn, @"^([A-Za-z가-힣]+)");
            if (km.Success) path = path.Replace("{keyword}", km.Groups[1].Value);
            path = path.Replace("{size}", file.Length > 104857600 ? "대용량" : file.Length > 10485760 ? "중간" : "소용량");
            return Path.Combine(bd, path, file.Name);
        }
    }
}
