namespace AIRoundTable.Models;

public class Message
{
    public string Sender    { get; init; } = string.Empty;
    public string Content   { get; init; } = string.Empty;
    public DateTime Timestamp { get; init; } = DateTime.Now;
}
