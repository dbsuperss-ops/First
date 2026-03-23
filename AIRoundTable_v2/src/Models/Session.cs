using System.Collections.ObjectModel;

namespace AIRoundTable.Models;

public class Session
{
    public Guid   Id          { get; } = Guid.NewGuid();
    public string Name        { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime CreatedAt { get; init; } = DateTime.Now;
    public string CreatedAtText => CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public ObservableCollection<Message> Messages { get; } = new();
}
