using Microsoft.Web.WebView2.Wpf;

namespace AIRoundTable.Services;

/// <summary>
/// WebView2 브라우저 자동화 — JS 주입으로 AI 사이트에 메시지 전송 후 응답 추출
/// </summary>
public class BrowserAiService : IAiService
{
    private readonly WebView2 _webView;
    private readonly string   _inputSel;
    private readonly string   _submitSel;
    private readonly string   _responseSel;

    public BrowserAiService(
        WebView2 webView,
        string   inputSelector,
        string   submitSelector,
        string   responseSelector)
    {
        _webView     = webView;
        _inputSel    = EscapeForJs(inputSelector);
        _submitSel   = EscapeForJs(submitSelector);
        _responseSel = EscapeForJs(responseSelector);
    }

    public async Task<string> AskAsync(string prompt, CancellationToken ct = default)
    {
        if (_webView.CoreWebView2 is null)
            throw new InvalidOperationException("WebView2가 아직 초기화되지 않았습니다. 브라우저 패널에서 해당 사이트에 먼저 로그인하십시오.");

        var escapedPrompt = prompt
            .Replace("\\", "\\\\")
            .Replace("`",  "\\`")
            .Replace("$",  "\\$");

        // 현재 응답 개수 기록
        var countStr = await _webView.CoreWebView2.ExecuteScriptAsync(
            $"document.querySelectorAll('{_responseSel}').length");
        int before = int.TryParse(countStr, out var b) ? b : 0;

        // 입력창에 텍스트 주입 (React/ProseMirror/일반 textarea 모두 지원)
        await _webView.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const el = document.querySelector('{_inputSel}');
    if (!el) return;
    el.focus();
    if (el.tagName === 'TEXTAREA' || el.tagName === 'INPUT') {{
        const desc = Object.getOwnPropertyDescriptor(
            el.tagName === 'TEXTAREA'
                ? window.HTMLTextAreaElement.prototype
                : window.HTMLInputElement.prototype,
            'value');
        if (desc && desc.set) {{
            desc.set.call(el, `{escapedPrompt}`);
        }} else {{
            el.value = `{escapedPrompt}`;
        }}
        el.dispatchEvent(new Event('input',  {{ bubbles: true }}));
        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
    }} else {{
        // contenteditable (ProseMirror 등)
        document.execCommand('selectAll', false, null);
        document.execCommand('insertText', false, `{escapedPrompt}`);
    }}
}})();
");

        await Task.Delay(600, ct);

        // 전송 버튼 클릭 (없으면 Enter 키 이벤트)
        await _webView.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const btn = document.querySelector('{_submitSel}');
    if (btn && !btn.disabled) {{
        btn.click();
    }} else {{
        const el = document.querySelector('{_inputSel}');
        el?.dispatchEvent(new KeyboardEvent('keydown', {{ key: 'Enter', keyCode: 13, bubbles: true }}));
    }}
}})();
");

        // 응답 대기 (최대 90초, 500ms 간격 폴링)
        var deadline = DateTime.UtcNow.AddSeconds(90);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(800, ct);

            var afterStr = await _webView.CoreWebView2.ExecuteScriptAsync(
                $"document.querySelectorAll('{_responseSel}').length");

            if (int.TryParse(afterStr, out var after) && after > before)
            {
                // 스트리밍 완료 대기: 전송 버튼이 다시 활성화될 때까지
                await WaitForSubmitEnabledAsync(ct);

                var raw = await _webView.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const els = document.querySelectorAll('{_responseSel}');
    return els.length > 0 ? els[els.length - 1].innerText : '';
}})();
");
                // JSON 문자열 이스케이프 제거
                return UnescapeJsonString(raw);
            }
        }

        return ct.IsCancellationRequested ? "(취소됨)" : "(응답 시간 초과)";
    }

    /// <summary>전송 버튼이 다시 활성화될 때까지 대기 (스트리밍 완료 신호)</summary>
    private async Task WaitForSubmitEnabledAsync(CancellationToken ct)
    {
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline && !ct.IsCancellationRequested)
        {
            await Task.Delay(500, ct);
            var disabled = await _webView.CoreWebView2.ExecuteScriptAsync(
                $"(document.querySelector('{_submitSel}')?.disabled ?? false).toString()");
            if (disabled == "false" || disabled == "null")
                return;
        }
    }

    private static string EscapeForJs(string selector) =>
        selector.Replace("'", "\\'");

    private static string UnescapeJsonString(string raw)
    {
        if (raw.Length >= 2 && raw[0] == '"' && raw[^1] == '"')
            raw = raw[1..^1];
        return raw
            .Replace("\\n",  "\n")
            .Replace("\\t",  "\t")
            .Replace("\\\"", "\"")
            .Replace("\\\\", "\\");
    }
}
