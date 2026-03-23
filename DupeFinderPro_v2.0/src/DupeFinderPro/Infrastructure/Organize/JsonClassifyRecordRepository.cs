using System.Text.Json;
using System.Text.Json.Serialization;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Infrastructure.Organize;

public sealed class JsonClassifyRecordRepository : IClassifyRecordRepository
{
    private static readonly string DataFolder = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupeFinderPro");
    private static readonly string FilePath = Path.Combine(DataFolder, "classify-records.json");
    private const int MaxRecords = 200;
    private static readonly JsonSerializerOptions JsonOpt = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    public IReadOnlyList<ClassifyRecord> GetAll()
    {
        try
        {
            if (!File.Exists(FilePath)) return [];
            return JsonSerializer.Deserialize<List<ClassifyRecord>>(File.ReadAllText(FilePath), JsonOpt) ?? [];
        }
        catch { return []; }
    }

    public bool Add(ClassifyRecord record)
    {
        try
        {
            var records = GetAll().ToList();
            records.Insert(0, record);
            if (records.Count > MaxRecords)
                records = records.Take(MaxRecords).ToList();
            Directory.CreateDirectory(DataFolder);
            File.WriteAllText(FilePath, JsonSerializer.Serialize(records, JsonOpt));
            return true;
        }
        catch { return false; }
    }

    public void Clear()
    {
        try { File.Delete(FilePath); }
        catch { }
    }
}
