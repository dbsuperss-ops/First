using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileFlow.Models;
namespace FileFlow.Services
{
    public static class LogService
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow");
        private static readonly string FilePath = Path.Combine(Folder, "logs.json");
        private const int MaxLogCount = 2000;
        private static readonly JsonSerializerOptions JsonOpt = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public static List<LogEntry> Load()
        {
            try { if (!File.Exists(FilePath)) return new(); return JsonSerializer.Deserialize<List<LogEntry>>(File.ReadAllText(FilePath), JsonOpt) ?? new(); }
            catch (Exception ex) { ErrorService.Report(ex, "LogService.Load"); return new(); }
        }

        public static bool AddLogs(List<LogEntry> newLogs)
        {
            try { var logs = Load(); logs.AddRange(newLogs); if (logs.Count > MaxLogCount) logs = logs.Skip(logs.Count - MaxLogCount).ToList(); Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(logs, JsonOpt)); return true; }
            catch (Exception ex) { ErrorService.Report(ex, "LogService.AddLogs"); return false; }
        }

        public static void Clear() { try { File.Delete(FilePath); } catch (Exception ex) { ErrorService.Report(ex, "LogService.Clear"); } }

        public static (int success, int fail) UndoLastBatch(Guid batchId)
        {
            var logs = Load();
            var batch = logs.Where(l => l.BatchId == batchId && l.Action == "이동").ToList();
            int success = 0, fail = 0;
            foreach (var entry in batch)
            {
                try
                {
                    if (File.Exists(entry.TargetPath))
                    {
                        var d = Path.GetDirectoryName(entry.SourcePath);
                        if (d != null) Directory.CreateDirectory(d);
                        File.Move(entry.TargetPath, GetNonConflict(entry.SourcePath));
                        success++;
                    }
                    else fail++;
                }
                catch { fail++; }
            }
            // 되돌린 배치 로그를 제거하여 이중 Undo 방지
            if (success > 0)
            {
                var remaining = logs.Where(l => l.BatchId != batchId).ToList();
                try { Directory.CreateDirectory(Folder); File.WriteAllText(FilePath, JsonSerializer.Serialize(remaining, JsonOpt)); } catch { }
            }
            return (success, fail);
        }

        public static Guid GetLastBatchId() => Load().Where(l => l.BatchId != Guid.Empty && l.Action == "이동").OrderByDescending(l => l.Timestamp).Select(l => l.BatchId).FirstOrDefault();

        private static string GetNonConflict(string p)
        {
            if (!File.Exists(p)) return p; string d = Path.GetDirectoryName(p) ?? ""; string n = Path.GetFileNameWithoutExtension(p); string e = Path.GetExtension(p); int c = 1; string r;
            do { r = Path.Combine(d, $"{n}_{c++}{e}"); } while (File.Exists(r)); return r;
        }
    }
}
