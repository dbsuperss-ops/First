namespace AIRoundTable.Services;

public interface IAiService
{
    Task<string> AskAsync(string prompt, CancellationToken ct = default);
}
