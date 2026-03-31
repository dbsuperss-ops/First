using AttendanceGenerator.Models;

namespace AttendanceGenerator.Services;

public interface IDataService
{
    Task SaveAsync(AttendanceData data, string path);
    Task<AttendanceData> LoadAsync(string path);
}
