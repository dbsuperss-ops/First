using System.Text.Json;
using System.Text.Json.Serialization;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Infrastructure.Organize;

public sealed class JsonOrganizeLogRepository : IOrganizeLogRepository
{
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupeFinderPro");
    private static readonly string FilePath = Path.Combine(DataFolder, "organize-logs.json");
    private const int MaxLogCount = 2000;
    
    // 스레드 안전성을 위한 전용 락 객체
    private static readonly object _syncLock = new();

    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<OrganizeLogEntry> GetAll()
    {
        lock (_syncLock)
        {
            try
            {
                if (!File.Exists(FilePath)) return [];
                return JsonSerializer.Deserialize<List<OrganizeLogEntry>>(File.ReadAllText(FilePath), JsonOpt) ?? [];
            }
            catch { return []; }
        }
    }

    public bool AddRange(IReadOnlyList<OrganizeLogEntry> entries)
    {
        lock (_syncLock)
        {
            try
            {
                List<OrganizeLogEntry> logs = [];
                if (File.Exists(FilePath))
                {
                    logs = JsonSerializer.Deserialize<List<OrganizeLogEntry>>(File.ReadAllText(FilePath), JsonOpt) ?? [];
                }

                logs.AddRange(entries);
                if (logs.Count > MaxLogCount)
                    logs = logs.Skip(logs.Count - MaxLogCount).ToList();
                    
                Directory.CreateDirectory(DataFolder);
                File.WriteAllText(FilePath, JsonSerializer.Serialize(logs, JsonOpt));
                return true;
            }
            catch { return false; }
        }
    }

    public (int success, int fail) UndoBatch(Guid batchId)
    {
        lock (_syncLock)
        {
            try
            {
                List<OrganizeLogEntry> logs = [];
                if (File.Exists(FilePath))
                {
                    logs = JsonSerializer.Deserialize<List<OrganizeLogEntry>>(File.ReadAllText(FilePath), JsonOpt) ?? [];
                }

                var batch = logs.Where(l => l.BatchId == batchId && l.Action == "이동").ToList();
                int success = 0, fail = 0;

                foreach (var entry in batch)
                {
                    try
                    {
                        if (File.Exists(entry.TargetPath))
                        {
                            var dir = Path.GetDirectoryName(entry.SourcePath);
                            if (dir != null) Directory.CreateDirectory(dir);
                            File.Move(entry.TargetPath, GetNonConflict(entry.SourcePath));
                            success++;
                        }
                        else fail++;
                    }
                    catch { fail++; }
                }

                if (success > 0)
                {
                    var remaining = logs.Where(l => l.BatchId != batchId).ToList();
                    Directory.CreateDirectory(DataFolder);
                    File.WriteAllText(FilePath, JsonSerializer.Serialize(remaining, JsonOpt));
                }

                return (success, fail);
            }
            catch { return (0, 0); }
        }
    }

    public Guid GetLastBatchId()
    {
        lock (_syncLock)
        {
            try
            {
                List<OrganizeLogEntry> logs = [];
                if (File.Exists(FilePath))
                {
                    logs = JsonSerializer.Deserialize<List<OrganizeLogEntry>>(File.ReadAllText(FilePath), JsonOpt) ?? [];
                }
                
                return logs
                    .Where(l => l.BatchId != Guid.Empty && l.Action == "이동")
                    .OrderByDescending(l => l.Timestamp)
                    .Select(l => l.BatchId)
                    .FirstOrDefault();
            }
            catch { return Guid.Empty; }
        }
    }

    public void Clear()
    {
        lock (_syncLock)
        {
            try { File.Delete(FilePath); }
            catch { }
        }
    }

    private static string GetNonConflict(string path)
    {
        if (!File.Exists(path)) return path;
        var dir = Path.GetDirectoryName(path) ?? "";
        var name = Path.GetFileNameWithoutExtension(path);
        var ext = Path.GetExtension(path);
        int i = 1;
        string result;
        do { result = Path.Combine(dir, $"{name}_{i++}{ext}"); }
        while (File.Exists(result));
        return result;
    }
}
