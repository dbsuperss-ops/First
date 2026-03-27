namespace AIRoundTable.Models;

public enum ApiType
{
    OpenAiCompat,  // OpenAI, DeepSeek 등 OpenAI 호환 엔드포인트
    Anthropic,     // Claude API
    Gemini,        // Google Gemini API
}
