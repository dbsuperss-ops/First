namespace AttendanceGenerator.Models;

public class ProgramMeta
{
    public int Year { get; set; } = DateTime.Today.Year;
    public string Category { get; set; } = "방과후";      // 맞춤형 / 방과후
    public string ProgramName { get; set; } = "";
    public string Weekday { get; set; } = "월";           // 월/화/수/목/금
    public DateTime StartDate { get; set; } = DateTime.Today;
    public DateTime EndDate { get; set; } = DateTime.Today;
    public string Teacher { get; set; } = "";
    public string Approver1 { get; set; } = "";           // 담당
    public string Approver2 { get; set; } = "";           // 팀장
    public string Approver3 { get; set; } = "";           // 교장
}
