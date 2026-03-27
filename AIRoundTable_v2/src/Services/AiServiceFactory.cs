using AIRoundTable.Models;
using Microsoft.Web.WebView2.Wpf;

namespace AIRoundTable.Services;

public static class AiServiceFactory
{
    /// <summary>
    /// AiModelConfig 에 맞는 IAiService 인스턴스를 생성합니다.
    /// Browser 모드는 webViews 딕셔너리에서 해당 WebView2 를 찾아 사용합니다.
    /// </summary>
    public static IAiService? Create(
        AiModelConfig config,
        IReadOnlyDictionary<string, WebView2> webViews)
    {
        return config.Mode switch
        {
            AiMode.Api => CreateApiService(config),

            AiMode.Browser when webViews.TryGetValue(config.Name, out var wv) =>
                new BrowserAiService(
                    wv,
                    config.InputSelector    ?? "textarea",
                    config.SubmitSelector   ?? "button[type=submit]",
                    config.ResponseSelector ?? ".response"),

            _ => null, // Manual 또는 WebView2 미준비
        };
    }

    private static IAiService? CreateApiService(AiModelConfig config)
    {
        if (string.IsNullOrWhiteSpace(config.ApiKey))
            return null;

        return config.ApiType switch
        {
            ApiType.OpenAiCompat => new OpenAiCompatService(
                config.ApiKey,
                config.ApiEndpoint ?? "https://api.openai.com/v1",
                config.ModelId     ?? "gpt-4o"),

            ApiType.Anthropic => new AnthropicService(
                config.ApiKey,
                config.ModelId ?? "claude-opus-4-5"),

            ApiType.Gemini => new GeminiService(
                config.ApiKey,
                config.ModelId ?? "gemini-2.0-flash"),

            _ => null,
        };
    }
}
