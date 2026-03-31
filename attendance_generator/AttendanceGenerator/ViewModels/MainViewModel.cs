using System.Collections.ObjectModel;
using AttendanceGenerator.Models;
using AttendanceGenerator.Services;
using Microsoft.Win32;

namespace AttendanceGenerator.ViewModels;

public class MainViewModel : ViewModelBase
{
    private readonly IDataService _dataService;

    // ── Program Meta ────────────────────────────────────────────────
    private int _year = DateTime.Today.Year;
    private string _category = "방과후";
    private string _programName = "";
    private string _weekday = "월";
    private DateTime _startDate = new(DateTime.Today.Year, 3, 1);
    private DateTime _endDate = new(DateTime.Today.Year, 7, 31);
    private string _teacher = "";
    private string _approver1 = "";
    private string _approver2 = "";
    private string _approver3 = "";
    private string _statusMessage = "준비됨";

    public int Year { get => _year; set => SetProperty(ref _year, value); }
    public string Category { get => _category; set => SetProperty(ref _category, value); }
    public string ProgramName { get => _programName; set => SetProperty(ref _programName, value); }
    public string Weekday { get => _weekday; set => SetProperty(ref _weekday, value); }
    public DateTime StartDate { get => _startDate; set => SetProperty(ref _startDate, value); }
    public DateTime EndDate { get => _endDate; set => SetProperty(ref _endDate, value); }
    public string Teacher { get => _teacher; set => SetProperty(ref _teacher, value); }
    public string Approver1 { get => _approver1; set => SetProperty(ref _approver1, value); }
    public string Approver2 { get => _approver2; set => SetProperty(ref _approver2, value); }
    public string Approver3 { get => _approver3; set => SetProperty(ref _approver3, value); }
    public string StatusMessage { get => _statusMessage; set => SetProperty(ref _statusMessage, value); }

    // ── Static Options ───────────────────────────────────────────────
    public IReadOnlyList<string> Categories { get; } = ["맞춤형", "방과후"];
    public IReadOnlyList<string> Weekdays { get; } = ["월", "화", "수", "목", "금"];
    public IReadOnlyList<string> AttendanceOptions { get; } = ["", "○", "△", "×"];

    // ── Data Collections ─────────────────────────────────────────────
    public ObservableCollection<DateTime> ClassDates { get; } = [];
    public ObservableCollection<StudentRowViewModel> Students { get; } = [];

    // ── Commands ─────────────────────────────────────────────────────
    public RelayCommand GenerateDatesCommand { get; }
    public RelayCommand AddStudentCommand { get; }
    public RelayCommand RemoveLastStudentCommand { get; }
    public RelayCommand SaveCommand { get; }
    public RelayCommand LoadCommand { get; }

    /// <summary>View가 구독하여 동적 날짜 컬럼을 재생성한다.</summary>
    public event Action? ClassDatesChanged;

    public MainViewModel(IDataService dataService)
    {
        _dataService = dataService;

        GenerateDatesCommand    = new RelayCommand(GenerateDates);
        AddStudentCommand       = new RelayCommand(AddStudent);
        RemoveLastStudentCommand = new RelayCommand(RemoveLast, () => Students.Count > 0);
        SaveCommand             = new RelayCommand(async () => await SaveAsync());
        LoadCommand             = new RelayCommand(async () => await LoadAsync());
    }

    // ── Business Logic ───────────────────────────────────────────────

    private void GenerateDates()
    {
        if (StartDate.Date > EndDate.Date)
        {
            StatusMessage = "오류: 시작일이 종료일보다 늦습니다.";
            return;
        }

        var targetDay = Weekday switch
        {
            "월" => DayOfWeek.Monday,
            "화" => DayOfWeek.Tuesday,
            "수" => DayOfWeek.Wednesday,
            "목" => DayOfWeek.Thursday,
            "금" => DayOfWeek.Friday,
            _   => DayOfWeek.Monday
        };

        var dates = new List<DateTime>();
        for (var d = StartDate.Date; d <= EndDate.Date; d = d.AddDays(1))
            if (d.DayOfWeek == targetDay)
                dates.Add(d);

        ClassDates.Clear();
        foreach (var dt in dates) ClassDates.Add(dt);

        foreach (var student in Students)
            student.RebuildAttendances(dates);

        ClassDatesChanged?.Invoke();
        StatusMessage = $"날짜 생성 완료 — {Weekday}요일 총 {dates.Count}회";
    }

    private void AddStudent()
    {
        var row = new StudentRowViewModel { Order = Students.Count + 1 };
        row.RebuildAttendances(ClassDates);
        Students.Add(row);
        RemoveLastStudentCommand.RaiseCanExecuteChanged();
    }

    private void RemoveLast()
    {
        if (Students.Count == 0) return;
        Students.RemoveAt(Students.Count - 1);
        RemoveLastStudentCommand.RaiseCanExecuteChanged();
        StatusMessage = $"마지막 행 삭제됨 (현재 {Students.Count}명)";
    }

    private async Task SaveAsync()
    {
        var dlg = new SaveFileDialog
        {
            Filter = "출석부 파일 (*.att)|*.att|모든 파일 (*.*)|*.*",
            DefaultExt = ".att",
            FileName = $"{Category}_{ProgramName}_{Weekday}_출석부"
        };
        if (dlg.ShowDialog() != true) return;

        var data = BuildAttendanceData();
        await _dataService.SaveAsync(data, dlg.FileName);
        StatusMessage = $"저장 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    private async Task LoadAsync()
    {
        var dlg = new OpenFileDialog
        {
            Filter = "출석부 파일 (*.att)|*.att|모든 파일 (*.*)|*.*"
        };
        if (dlg.ShowDialog() != true) return;

        var data = await _dataService.LoadAsync(dlg.FileName);
        ApplyAttendanceData(data);
        StatusMessage = $"불러오기 완료: {System.IO.Path.GetFileName(dlg.FileName)}";
    }

    private AttendanceData BuildAttendanceData() => new()
    {
        Meta = new ProgramMeta
        {
            Year = Year, Category = Category, ProgramName = ProgramName,
            Weekday = Weekday, StartDate = StartDate, EndDate = EndDate,
            Teacher = Teacher, Approver1 = Approver1, Approver2 = Approver2, Approver3 = Approver3
        },
        ClassDates = [.. ClassDates],
        Students = Students.Select(s => new StudentRecord
        {
            Student = new Student
            {
                Name = s.Name, Grade = s.Grade, Class = s.Class,
                Number = s.Number, ParticipationDay = s.ParticipationDay,
                Phone = s.Phone, Note = s.Note
            },
            Attendances = s.Attendances
                .Select(a => new AttendanceEntry { Date = a.Date, Status = a.Status })
                .ToList()
        }).ToList()
    };

    private void ApplyAttendanceData(AttendanceData data)
    {
        Year = data.Meta.Year;
        Category = data.Meta.Category;
        ProgramName = data.Meta.ProgramName;
        Weekday = data.Meta.Weekday;
        StartDate = data.Meta.StartDate;
        EndDate = data.Meta.EndDate;
        Teacher = data.Meta.Teacher;
        Approver1 = data.Meta.Approver1;
        Approver2 = data.Meta.Approver2;
        Approver3 = data.Meta.Approver3;

        ClassDates.Clear();
        foreach (var d in data.ClassDates) ClassDates.Add(d);

        Students.Clear();
        int order = 1;
        foreach (var sr in data.Students)
        {
            var row = new StudentRowViewModel
            {
                Order = order++,
                Name = sr.Student.Name,
                Grade = sr.Student.Grade,
                Class = sr.Student.Class,
                Number = sr.Student.Number,
                ParticipationDay = sr.Student.ParticipationDay,
                Phone = sr.Student.Phone,
                Note = sr.Student.Note
            };
            row.RebuildAttendances(data.ClassDates);
            foreach (var att in sr.Attendances)
            {
                var cell = row.Attendances.FirstOrDefault(a => a.Date == att.Date.Date);
                if (cell is not null) cell.Status = att.Status;
            }
            Students.Add(row);
        }

        RemoveLastStudentCommand.RaiseCanExecuteChanged();
        ClassDatesChanged?.Invoke();
    }
}
