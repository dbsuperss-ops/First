namespace AIRoundTable.Models;

public class AiModelConfig
{
    public string  Name    { get; set; } = string.Empty;
    public string  Color   { get; set; } = "#6B7280";
    public AiMode  Mode    { get; set; } = AiMode.Manual;
    public bool    Enabled { get; set; } = true;

    // ── API 모드 ──────────────────────────────────────────────────────────
    public ApiType ApiType      { get; set; } = ApiType.OpenAiCompat;
    public string? ApiKey       { get; set; }
    public string? ApiEndpoint  { get; set; }   // OpenAI 호환 시 엔드포인트
    public string? ModelId      { get; set; }

    // ── 브라우저 모드 ─────────────────────────────────────────────────────
    public string? SiteUrl           { get; set; }
    public string? InputSelector     { get; set; }  // 입력창 CSS 셀렉터
    public string? SubmitSelector    { get; set; }  // 전송 버튼 CSS 셀렉터
    public string? ResponseSelector  { get; set; }  // 응답 요소 CSS 셀렉터
}
