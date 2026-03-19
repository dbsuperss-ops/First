using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using FileFlow.Models;
namespace FileFlow.Services
{
    public static class RecordService
    {
        private static readonly string Folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow");
        private static readonly string FilePath = Path.Combine(Folder, "records.json");
        private static readonly JsonSerializerOptions JsonOpt = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public static List<ClassifyRecord> LoadRecords()
        {
            try { if (!File.Exists(FilePath)) return new(); return JsonSerializer.Deserialize<List<ClassifyRecord>>(File.ReadAllText(FilePath), JsonOpt) ?? new(); }
            catch (Exception ex) { ErrorService.Report(ex, "RecordService.LoadRecords"); return new(); }
        }

        public static bool SaveRecord(ClassifyRecord record)
        {
            try { Directory.CreateDirectory(Folder); var records = LoadRecords(); records.Insert(0, record); if (records.Count > 200) records = records.Take(200).ToList(); File.WriteAllText(FilePath, JsonSerializer.Serialize(records, JsonOpt)); return true; }
            catch (Exception ex) { ErrorService.Report(ex, "RecordService.SaveRecord"); return false; }
        }

        public static void Clear() { try { File.Delete(FilePath); } catch (Exception ex) { ErrorService.Report(ex, "RecordService.Clear"); } }
    }
}
