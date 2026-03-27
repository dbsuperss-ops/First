using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace AIRoundTable.Services;

/// <summary>
/// OpenAI /v1/chat/completions 호환 엔드포인트 (OpenAI, DeepSeek 등)
/// </summary>
public class OpenAiCompatService : IAiService
{
    private readonly HttpClient _http;
    private readonly string     _endpoint;
    private readonly string     _model;

    public OpenAiCompatService(string apiKey, string endpoint, string model)
    {
        _endpoint = endpoint.TrimEnd('/');
        _model    = model;
        _http     = new HttpClient { Timeout = TimeSpan.FromSeconds(120) };
        _http.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", apiKey);
    }

    public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
    {
        var payload = new
        {
            model    = _model,
            messages = new[] { new { role = "user", content = prompt } }
        };

        var content  = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var resp = await _http.PostAsync($"{_endpoint}/chat/completions", content, ct);
        resp.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await resp.Content.ReadAsStringAsync(ct));
        return doc.RootElement
                  .GetProperty("choices")[0]
                  .GetProperty("message")
                  .GetProperty("content")
                  .GetString() ?? string.Empty;
    }
}
