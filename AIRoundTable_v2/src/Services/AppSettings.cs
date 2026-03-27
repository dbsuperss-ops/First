using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using AIRoundTable.Models;

namespace AIRoundTable.Services;

public class AppSettings
{
    private static readonly string SettingsPath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "AIRoundTable", "settings.json");

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented    = true,
        Converters       = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public List<AiModelConfig> Models { get; set; } = new();

    // ── 로드 ──────────────────────────────────────────────────────────────
    public static AppSettings Load()
    {
        if (!File.Exists(SettingsPath))
            return CreateDefault();

        try
        {
            var json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<AppSettings>(json, JsonOpts) ?? CreateDefault();
        }
        catch
        {
            return CreateDefault();
        }
    }

    // ── 저장 ──────────────────────────────────────────────────────────────
    public void Save()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(SettingsPath)!);
        File.WriteAllText(SettingsPath, JsonSerializer.Serialize(this, JsonOpts));
    }

    // ── 기본값 ────────────────────────────────────────────────────────────
    private static AppSettings CreateDefault() => new()
    {
        Models =
        [
            new()
            {
                Name = "ChatGPT", Color = "#10a37f", Mode = AiMode.Manual,
                ApiType = ApiType.OpenAiCompat,
                ApiEndpoint = "https://api.openai.com/v1", ModelId = "gpt-4o",
                SiteUrl          = "https://chat.openai.com",
                InputSelector    = "#prompt-textarea",
                SubmitSelector   = "[data-testid='send-button']",
                ResponseSelector = ".markdown",
            },
            new()
            {
                Name = "클로드", Color = "#d97706", Mode = AiMode.Manual,
                ApiType = ApiType.Anthropic,
                ModelId = "claude-opus-4-5",
                SiteUrl          = "https://claude.ai/new",
                InputSelector    = ".ProseMirror[contenteditable='true']",
                SubmitSelector   = "[aria-label='Send message']",
                ResponseSelector = ".font-claude-message",
            },
            new()
            {
                Name = "제미나이", Color = "#4285f4", Mode = AiMode.Manual,
                ApiType = ApiType.Gemini,
                ModelId = "gemini-2.0-flash",
                SiteUrl          = "https://gemini.google.com",
                InputSelector    = ".ql-editor",
                SubmitSelector   = "button.send-button",
                ResponseSelector = ".model-response-text",
            },
            new()
            {
                Name = "딥시크", Color = "#4f46e5", Mode = AiMode.Manual,
                ApiType = ApiType.OpenAiCompat,
                ApiEndpoint = "https://api.deepseek.com/v1", ModelId = "deepseek-chat",
                SiteUrl          = "https://chat.deepseek.com",
                InputSelector    = "#chat-input",
                SubmitSelector   = "[aria-label='Send']",
                ResponseSelector = ".ds-markdown",
            },
            new()
            {
                Name = "코파일럿", Color = "#0078d4", Mode = AiMode.Manual,
                SiteUrl          = "https://copilot.microsoft.com",
                InputSelector    = "textarea",
                SubmitSelector   = "[aria-label='Submit']",
                ResponseSelector = ".ac-container",
            },
        ]
    };
}
