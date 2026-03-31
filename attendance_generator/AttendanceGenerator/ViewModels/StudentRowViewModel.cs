using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AttendanceGenerator.ViewModels;

public class StudentRowViewModel : ViewModelBase
{
    private int _order;
    private string _name = "";
    private string _grade = "";
    private string _class = "";
    private string _number = "";
    private string _participationDay = "";
    private string _phone = "";
    private string _note = "";

    public int Order
    {
        get => _order;
        set => SetProperty(ref _order, value);
    }
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public string Grade
    {
        get => _grade;
        set => SetProperty(ref _grade, value);
    }
    public string Class
    {
        get => _class;
        set => SetProperty(ref _class, value);
    }
    public string Number
    {
        get => _number;
        set => SetProperty(ref _number, value);
    }
    public string ParticipationDay
    {
        get => _participationDay;
        set => SetProperty(ref _participationDay, value);
    }
    public string Phone
    {
        get => _phone;
        set => SetProperty(ref _phone, value);
    }
    public string Note
    {
        get => _note;
        set => SetProperty(ref _note, value);
    }

    public ObservableCollection<AttendanceCellViewModel> Attendances { get; } = [];

    public int TotalPresent => Attendances.Count(a => a.Status == "○");
    public int TotalLate    => Attendances.Count(a => a.Status == "△");
    public int TotalAbsent  => Attendances.Count(a => a.Status == "×");

    /// <summary>날짜 목록에 맞게 출결 셀을 재구성한다. 기존 상태는 보존된다.</summary>
    public void RebuildAttendances(IEnumerable<DateTime> dates)
    {
        var existing = Attendances.ToDictionary(a => a.Date, a => a.Status);

        foreach (var cell in Attendances)
            cell.PropertyChanged -= OnCellChanged;

        Attendances.Clear();

        foreach (var d in dates)
        {
            var cell = new AttendanceCellViewModel(d);
            if (existing.TryGetValue(d.Date, out var status))
                cell.Status = status;
            cell.PropertyChanged += OnCellChanged;
            Attendances.Add(cell);
        }

        RefreshTotals();
    }

    private void OnCellChanged(object? sender, PropertyChangedEventArgs e)
        => RefreshTotals();

    private void RefreshTotals()
    {
        OnPropertyChanged(nameof(TotalPresent));
        OnPropertyChanged(nameof(TotalLate));
        OnPropertyChanged(nameof(TotalAbsent));
    }
}
