using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIRoundTable.Services;

/// <summary>
/// Anthropic Claude API — POST /v1/messages
/// </summary>
public class AnthropicService : IAiService
{
    private readonly HttpClient _http;
    private readonly string     _model;

    public AnthropicService(string apiKey, string model)
    {
        _model = model;
        _http  = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _http.DefaultRequestHeaders.Add("x-api-key", apiKey);
        _http.DefaultRequestHeaders.Add("anthropic-version", "2023-06-01");
    }

    public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new
        {
            model      = _model,
            max_tokens = 2048,
            messages   = new[] { new { role = "user", content = prompt } }
        };

        var content  = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync("https://api.anthropic.com/v1/messages", content, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
                  .GetProperty("content")[0]
                  .GetProperty("text")
                  .GetString() ?? string.Empty;
    }
}
