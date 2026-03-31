namespace AttendanceGenerator.Models;

public class Student
{
    public string Name { get; set; } = "";
    public string Grade { get; set; } = "";               // 학년
    public string Class { get; set; } = "";               // 반
    public string Number { get; set; } = "";              // 번호
    public string ParticipationDay { get; set; } = "";    // 참여요일
    public string Phone { get; set; } = "";
    public string Note { get; set; } = "";
}
