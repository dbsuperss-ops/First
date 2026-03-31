using System.IO;
using System.Text;
using System.Text.Json;
using AttendanceGenerator.Models;

namespace AttendanceGenerator.Services;

public class FileDataService : IDataService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task SaveAsync(AttendanceData data, string path)
    {
        var json = JsonSerializer.Serialize(data, Options);
        await File.WriteAllTextAsync(path, json, Encoding.UTF8);
    }

    public async Task<AttendanceData> LoadAsync(string path)
    {
        var json = await File.ReadAllTextAsync(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<AttendanceData>(json, Options) ?? new AttendanceData();
    }
}
