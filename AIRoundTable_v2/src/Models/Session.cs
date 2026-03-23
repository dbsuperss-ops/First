using System.Collections.ObjectModel;
using System.ComponentModel;

namespace AIRoundTable.Models;

public class Session : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;

    private string _name        = string.Empty;
    private string _description = string.Empty;

    public Guid   Id        { get; } = Guid.NewGuid();
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public string Name
    {
        get => _name;
        set { _name = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Name))); }
    }

    public string Description
    {
        get => _description;
        set { _description = value; PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Description))); }
    }

    public ObservableCollection<Message> Messages { get; } = new();
}
