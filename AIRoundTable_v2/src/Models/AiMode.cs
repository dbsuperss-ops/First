namespace AIRoundTable.Models;

public enum AiMode
{
    Manual,   // 수동 입력 (복사/붙여넣기)
    Api,      // 공식 REST API 키 사용
    Browser,  // WebView2 브라우저 자동화 (로그인 후 자동)
}
