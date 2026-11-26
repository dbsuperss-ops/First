using System.Text.Encodings.Web;
using System.Text.Json;
using DupeFinderPro.Domain.Interfaces;

namespace DupeFinderPro.Infrastructure.Storage;

/// <summary>%AppData%\DupeFinderPro\cleanup-log.jsonl에 한 줄씩 덧붙인다.</summary>
public sealed class JsonlCleanupLog : ICleanupLog
{
    private static readonly JsonSerializerOptions JsonOpt = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
    private readonly string _filePath;
    private readonly object _lock = new();

    public JsonlCleanupLog() : this(Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "DupeFinderPro", "cleanup-log.jsonl"))
    {
    }

    public JsonlCleanupLog(string filePath) => _filePath = filePath;

    public void Record(CleanupLogEntry entry)
    {
        var line = JsonSerializer.Serialize(entry, JsonOpt) + Environment.NewLine;
        lock (_lock)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_filePath)!);
            File.AppendAllText(_filePath, line);
        }
    }
}
