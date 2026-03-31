namespace AttendanceGenerator.Models;

/// <summary>저장/불러오기용 최상위 직렬화 모델</summary>
public class AttendanceData
{
    public ProgramMeta Meta { get; set; } = new();
    public List<DateTime> ClassDates { get; set; } = [];
    public List<StudentRecord> Students { get; set; } = [];
}

public class StudentRecord
{
    public Student Student { get; set; } = new();
    public List<AttendanceEntry> Attendances { get; set; } = [];
}
