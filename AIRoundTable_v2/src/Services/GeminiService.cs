using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace AIRoundTable.Services;

/// <summary>
/// Google Gemini API — generateContent
/// </summary>
public class GeminiService : IAiService
{
    private readonly HttpClient _http;
    private readonly string     _apiKey;
    private readonly string     _model;

    public GeminiService(string apiKey, string model)
    {
        _apiKey = apiKey;
        _model  = model;
        _http   = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
    }

    public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new
        {
            contents = new[]
            {
                new { parts = new[] { new { text = prompt } } }
            }
        };

        var url     = $"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}";
        var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync(url, content, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
                  .GetProperty("candidates")[0]
                  .GetProperty("content")
                  .GetProperty("parts")[0]
                  .GetProperty("text")
                  .GetString() ?? string.Empty;
    }
}
