namespace AttendanceGenerator.ViewModels;

public class AttendanceCellViewModel(DateTime date) : ViewModelBase
{
    public DateTime Date { get; } = date.Date;

    private string _status = "";
    public string Status
    {
        get => _status;
        set => SetProperty(ref _status, value);
    }
}
