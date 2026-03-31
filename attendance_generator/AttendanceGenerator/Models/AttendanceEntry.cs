namespace AttendanceGenerator.Models;

public class AttendanceEntry
{
    public DateTime Date { get; set; }
    public string Status { get; set; } = "";   // "", "○", "△", "×"
}
