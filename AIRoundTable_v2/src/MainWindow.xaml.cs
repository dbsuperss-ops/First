using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIRoundTable.Models;
using AIRoundTable.Services;
using AIRoundTable.Views;
using Microsoft.Web.WebView2.Wpf;

namespace AIRoundTable;

public partial class MainWindow : Window
{
    // ── 슬롯 정보 ──────────────────────────────────────────────────────────
    private class BrowserSlot
    {
        public WebView2         WebView     { get; set; } = null!;
        public AiModelConfig?   Model       { get; set; }
        public TextBlock?       StatusText  { get; set; }
        public bool             IsBusy      { get; set; }
    }

    // ── 데이터 ─────────────────────────────────────────────────────────────
    private readonly ObservableCollection<MessageViewModel> _messages = new();
    private Session _currentSession = new() { Name = "새 토론" };

    // ── 설정 & 브라우저 ────────────────────────────────────────────────────
    private AppSettings _settings;

    private readonly Dictionary<string, WebView2> _webViews = new();

    private readonly List<BrowserSlot> _leftSlots  = new();
    private readonly List<BrowserSlot> _rightSlots = new();
    private int _leftPanelCount  = 1;
    private int _rightPanelCount = 1;

    private string _lastPrompt = string.Empty;

    // ──────────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _messageList.ItemsSource = _messages;

        RebuildUiFromSettings();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                SaveLog();
        };
    }

    // ══════════════════════════════════════════════════════════════════════
    // 설정 기반 UI 재구성
    // ══════════════════════════════════════════════════════════════════════

    private void RebuildUiFromSettings()
    {
        MessageViewModel.RegisterColors(_settings.Models);

        BuildSidePanels(_leftBrowserArea,  _leftSlots,  "Left",  _leftPanelCount);
        BuildSidePanels(_rightBrowserArea, _rightSlots, "Right", _rightPanelCount);

        UpdateCountButtons();

        int enabledCount = _settings.Models.Count(m => m.Enabled);
        _activeModelCount.Text = $"{enabledCount}개 모델 참여 중";
    }

    // ══════════════════════════════════════════════════════════════════════
    // 슬롯 패널 구성
    // ══════════════════════════════════════════════════════════════════════

    private void BuildSidePanels(Grid area, List<BrowserSlot> slots, string side, int count)
    {
        // 기존 WebView2 해제
        foreach (var s in slots)
        {
            var key = _webViews.FirstOrDefault(kv => kv.Value == s.WebView).Key;
            if (key != null) _webViews.Remove(key);
            s.WebView.Dispose();
        }
        slots.Clear();
        area.Children.Clear();
        area.RowDefinitions.Clear();

        var availableModels = _settings.Models
            .Where(m => m.Enabled && !string.IsNullOrWhiteSpace(m.SiteUrl))
            .ToList();

        if (availableModels.Count == 0)
        {
            // 모델 없음 안내
            var placeholder = BuildPlaceholder();
            area.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            Grid.SetRow(placeholder, 0);
            area.Children.Add(placeholder);
            return;
        }

        for (int i = 0; i < count; i++)
        {
            if (i > 0)
            {
                area.RowDefinitions.Add(new RowDefinition { Height = new GridLength(4) });
                var splitter = new GridSplitter
                {
                    Height              = 4,
                    HorizontalAlignment = HorizontalAlignment.Stretch,
                    VerticalAlignment   = VerticalAlignment.Center,
                    Background          = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
                    ShowsPreview        = false,
                };
                Grid.SetRow(splitter, area.RowDefinitions.Count - 1);
                area.Children.Add(splitter);
            }

            area.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            // 기본 모델: 슬롯 순서대로 할당
            var defaultModel = availableModels[i % availableModels.Count];
            var slotPanel    = CreateSlotPanel(slots, availableModels, defaultModel);

            Grid.SetRow(slotPanel, area.RowDefinitions.Count - 1);
            area.Children.Add(slotPanel);
        }
    }

    private Grid CreateSlotPanel(
        List<BrowserSlot>   slots,
        List<AiModelConfig> availableModels,
        AiModelConfig       defaultModel)
    {
        var wv = new WebView2
        {
            HorizontalAlignment = HorizontalAlignment.Stretch,
            VerticalAlignment   = VerticalAlignment.Stretch,
        };

        var slot = new BrowserSlot { WebView = wv, Model = defaultModel };
        slots.Add(slot);

        // WebView 키 등록
        if (defaultModel.Name != null)
            _webViews[defaultModel.Name] = wv;

        // 초기 URL 로드
        LoadUrl(wv, defaultModel.SiteUrl);

        // ── 슬롯 헤더 ────────────────────────────────────────────────────
        var combo = new ComboBox
        {
            ItemsSource       = availableModels,
            DisplayMemberPath = "Name",
            SelectedItem      = defaultModel,
            VerticalAlignment = VerticalAlignment.Center,
            FontSize          = 12,
            Height            = 26,
            Margin            = new Thickness(0, 0, 6, 0),
        };
        combo.SelectionChanged += (_, _) =>
        {
            if (combo.SelectedItem is AiModelConfig m)
                OnSlotModelChanged(slot, m);
        };

        var sendBtn = new Button
        {
            Content = "📤 질문 전송",
            FontSize = 11,
            Padding  = new Thickness(8, 3, 8, 3),
            Margin   = new Thickness(0, 0, 4, 0),
            Cursor   = Cursors.Hand,
        };
        if (TryFindResource("OutlineButtonStyle") is Style outlineStyle)
            sendBtn.Style = outlineStyle;
        sendBtn.Click += async (_, _) => await SendToSlotAsync(slot);

        var getBtn = new Button
        {
            Content = "📥 답변 가져오기",
            FontSize = 11,
            Padding  = new Thickness(8, 3, 8, 3),
            Cursor   = Cursors.Hand,
        };
        if (TryFindResource("AccentButtonStyle") is Style accentStyle)
            getBtn.Style = accentStyle;
        getBtn.Click += async (_, _) => await GetAnswerFromSlotAsync(slot);

        var statusText = new TextBlock
        {
            FontSize          = 10,
            Foreground        = new SolidColorBrush(Color.FromRgb(0xF5, 0x9E, 0x0B)),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 6, 0),
            TextTrimming      = TextTrimming.CharacterEllipsis,
        };
        slot.StatusText = statusText;

        var headerGrid = new Grid();
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        headerGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(combo,      0);
        Grid.SetColumn(statusText, 1);
        Grid.SetColumn(sendBtn,    2);
        Grid.SetColumn(getBtn,     3);
        headerGrid.Children.Add(combo);
        headerGrid.Children.Add(statusText);
        headerGrid.Children.Add(sendBtn);
        headerGrid.Children.Add(getBtn);

        var header = new Border
        {
            Background      = new SolidColorBrush(Color.FromRgb(0xF1, 0xF5, 0xF9)),
            BorderBrush     = new SolidColorBrush(Color.FromRgb(0xE2, 0xE8, 0xF0)),
            BorderThickness = new Thickness(0, 0, 0, 1),
            Padding         = new Thickness(8, 5, 8, 5),
            Child           = headerGrid,
        };

        // ── 슬롯 Grid ────────────────────────────────────────────────────
        var slotGrid = new Grid();
        slotGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        slotGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(header, 0);
        Grid.SetRow(wv,     1);
        slotGrid.Children.Add(header);
        slotGrid.Children.Add(wv);

        return slotGrid;
    }

    private static void LoadUrl(WebView2 wv, string? url)
    {
        if (string.IsNullOrWhiteSpace(url)) return;
        var captured = url;
        wv.Loaded += async (_, _) =>
        {
            await wv.EnsureCoreWebView2Async();
            wv.Source = new Uri(captured);
        };
    }

    private void OnSlotModelChanged(BrowserSlot slot, AiModelConfig newModel)
    {
        // 이전 모델 키 제거
        if (slot.Model?.Name != null)
            _webViews.Remove(slot.Model.Name);

        slot.Model = newModel;

        if (newModel.Name != null)
            _webViews[newModel.Name] = slot.WebView;

        if (!string.IsNullOrWhiteSpace(newModel.SiteUrl))
        {
            if (slot.WebView.CoreWebView2 != null)
                slot.WebView.CoreWebView2.Navigate(newModel.SiteUrl);
            else
                LoadUrl(slot.WebView, newModel.SiteUrl);
        }
    }

    private static UIElement BuildPlaceholder()
    {
        var sp = new StackPanel
        {
            VerticalAlignment   = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        sp.Children.Add(new TextBlock
        {
            Text                = "🌐",
            FontSize            = 40,
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin              = new Thickness(0, 0, 0, 14),
        });
        sp.Children.Add(new TextBlock
        {
            Text                = "설정에서 모델을 추가하세요.",
            FontSize            = 13,
            Foreground          = new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8)),
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        return sp;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 창 수 선택
    // ══════════════════════════════════════════════════════════════════════

    private void PanelCount_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not Button btn || btn.Tag is not string tag) return;

        bool isLeft = tag.StartsWith('L');
        int  count  = int.Parse(tag[1..]);

        if (isLeft)
        {
            _leftPanelCount = count;
            BuildSidePanels(_leftBrowserArea, _leftSlots, "Left", count);
        }
        else
        {
            _rightPanelCount = count;
            BuildSidePanels(_rightBrowserArea, _rightSlots, "Right", count);
        }

        UpdateCountButtons();
    }

    private void UpdateCountButtons()
    {
        ApplyCountActiveState(_leftCount1Btn,  _leftPanelCount  == 1);
        ApplyCountActiveState(_leftCount2Btn,  _leftPanelCount  == 2);
        ApplyCountActiveState(_leftCount3Btn,  _leftPanelCount  == 3);
        ApplyCountActiveState(_rightCount1Btn, _rightPanelCount == 1);
        ApplyCountActiveState(_rightCount2Btn, _rightPanelCount == 2);
        ApplyCountActiveState(_rightCount3Btn, _rightPanelCount == 3);
    }

    private static void ApplyCountActiveState(Button btn, bool active)
    {
        btn.Background = active
            ? new SolidColorBrush(Colors.White)
            : Brushes.Transparent;
        btn.Foreground = active
            ? new SolidColorBrush(Color.FromRgb(0x1E, 0x29, 0x3B))
            : new SolidColorBrush(Color.FromRgb(0x94, 0xA3, 0xB8));
    }

    // ══════════════════════════════════════════════════════════════════════
    // 브라우저 액션: 질문 전송
    // ══════════════════════════════════════════════════════════════════════

    private async Task SendToSlotAsync(BrowserSlot slot)
    {
        if (string.IsNullOrWhiteSpace(_lastPrompt))
        {
            MessageBox.Show(
                "먼저 하단 입력창에 질문을 입력하고 '전송'을 누르세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (slot.Model is null)
        {
            MessageBox.Show("모델을 선택하세요.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        if (slot.WebView.CoreWebView2 is null)
        {
            MessageBox.Show("브라우저가 아직 로딩 중입니다. 잠시 후 다시 시도하세요.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        await InjectAndSubmitPromptAsync(slot.WebView, slot.Model, _lastPrompt);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 브라우저 액션: 답변 가져오기
    // ══════════════════════════════════════════════════════════════════════

    private async Task GetAnswerFromSlotAsync(BrowserSlot slot)
    {
        if (slot.Model is null) return;

        if (slot.WebView.CoreWebView2 is null)
        {
            MessageBox.Show("브라우저가 아직 로딩 중입니다.",
                "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var responseText = await ExtractLatestResponseAsync(slot.WebView, slot.Model);

        if (string.IsNullOrWhiteSpace(responseText))
        {
            MessageBox.Show(
                $"응답을 찾을 수 없습니다.\n\n" +
                $"응답 셀렉터: {slot.Model.ResponseSelector ?? "(미설정)"}\n\n" +
                $"설정 → 해당 모델 → 응답 셀렉터",
                "응답 없음", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var msg = new Message { Sender = slot.Model.Name, Content = responseText };
        _currentSession.Messages.Add(msg);
        _messages.Add(new MessageViewModel(msg));
        UpdateMessageCount();
        ScrollToBottom();
    }

    // ══════════════════════════════════════════════════════════════════════
    // JS 주입 / 응답 추출
    // ══════════════════════════════════════════════════════════════════════

    private async Task InjectAndSubmitPromptAsync(WebView2 wv, AiModelConfig model, string prompt)
    {
        var inputSel  = (model.InputSelector  ?? "textarea").Replace("'", "\\'");
        var submitSel = (model.SubmitSelector ?? "button[type=submit]").Replace("'", "\\'");
        var escaped   = prompt
            .Replace("\\", "\\\\")
            .Replace("`",  "\\`")
            .Replace("$",  "\\$");

        await wv.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const el = document.querySelector('{inputSel}');
    if (!el) return;
    el.focus();
    if (el.tagName === 'TEXTAREA' || el.tagName === 'INPUT') {{
        const proto = el.tagName === 'TEXTAREA'
            ? window.HTMLTextAreaElement.prototype
            : window.HTMLInputElement.prototype;
        const desc = Object.getOwnPropertyDescriptor(proto, 'value');
        if (desc?.set) {{
            desc.set.call(el, `{escaped}`);
        }} else {{
            el.value = `{escaped}`;
        }}
        el.dispatchEvent(new Event('input',  {{ bubbles: true }}));
        el.dispatchEvent(new Event('change', {{ bubbles: true }}));
    }} else {{
        el.focus();
        document.execCommand('selectAll', false, null);
        document.execCommand('insertText', false, `{escaped}`);
    }}
}})();
");

        await Task.Delay(600);

        var ctrlKey = model.UseCtrlEnter ? "true" : "false";
        await wv.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const btn = document.querySelector('{submitSel}');
    if (btn && !btn.disabled) {{
        btn.click();
    }} else {{
        const el = document.querySelector('{inputSel}');
        el?.dispatchEvent(new KeyboardEvent('keydown', {{
            key: 'Enter', keyCode: 13, ctrlKey: {ctrlKey}, bubbles: true
        }}));
    }}
}})();
");
    }

    private async Task<string> ExtractLatestResponseAsync(WebView2 wv, AiModelConfig model)
    {
        var responseSel = (model.ResponseSelector ?? ".response").Replace("'", "\\'");
        var raw = await wv.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const els = document.querySelectorAll('{responseSel}');
    return els.length > 0 ? els[els.length - 1].innerText : '';
}})();
");
        return UnescapeJsonString(raw);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 자동 주입 + 응답 수집
    // ══════════════════════════════════════════════════════════════════════

    /// <param name="isCrossDispatch">true이면 다른 AI의 응답을 받은 것 — 재귀 교차 배포 방지</param>
    private async Task AutoInjectAndExtractAsync(BrowserSlot slot, string prompt, bool isCrossDispatch = false)
    {
        if (slot.Model is null || slot.WebView.CoreWebView2 is null) return;

        var responseSel = (slot.Model.ResponseSelector ?? ".response").Replace("'", "\\'");

        slot.IsBusy = true;
        try
        {
            SetSlotStatus(slot, "📤 전송 중...");

            // 현재 응답 개수 기록
            var countStr = await slot.WebView.CoreWebView2.ExecuteScriptAsync(
                $"document.querySelectorAll('{responseSel}').length");
            int before = int.TryParse(countStr, out var b) ? b : 0;

            // 질문 주입
            await InjectAndSubmitPromptAsync(slot.WebView, slot.Model, prompt);

            SetSlotStatus(slot, "⏳ 응답 대기 중...");

            // 새 응답 등장까지 폴링 (최대 90초)
            var deadline = DateTime.UtcNow.AddSeconds(90);
            while (DateTime.UtcNow < deadline)
            {
                await Task.Delay(800);
                if (slot.WebView.CoreWebView2 is null) return;

                var afterStr = await slot.WebView.CoreWebView2.ExecuteScriptAsync(
                    $"document.querySelectorAll('{responseSel}').length");

                if (!int.TryParse(afterStr, out var after) || after <= before) continue;

                SetSlotStatus(slot, "✍️ 스트리밍 중...");
                await WaitForSubmitEnabledAsync(slot);

                // 응답 추출
                var raw = await slot.WebView.CoreWebView2.ExecuteScriptAsync($@"
(function() {{
    const els = document.querySelectorAll('{responseSel}');
    return els.length > 0 ? els[els.length - 1].innerText : '';
}})();
");
                var text = UnescapeJsonString(raw);

                if (!string.IsNullOrWhiteSpace(text))
                {
                    var senderName = slot.Model.Name;
                    Dispatcher.Invoke(() =>
                    {
                        var msg = new Message { Sender = senderName, Content = text };
                        _currentSession.Messages.Add(msg);
                        _messages.Add(new MessageViewModel(msg));
                        UpdateMessageCount();
                        ScrollToBottom();
                    });

                    // 다른 AI에게 이 답변 전달 (사용자 원질문에 대한 응답만, 무한루프 방지)
                    if (!isCrossDispatch)
                        _ = CrossDispatchResponseAsync(slot, text);
                }

                SetSlotStatus(slot, "✅ 완료");
                await Task.Delay(2000);
                SetSlotStatus(slot, "");
                return;
            }

            SetSlotStatus(slot, "⚠️ 응답 시간 초과");
            await Task.Delay(3000);
            SetSlotStatus(slot, "");
        }
        catch (OperationCanceledException)
        {
            SetSlotStatus(slot, "");
        }
        catch (Exception ex)
        {
            var msg = ex.Message.Length > 40 ? ex.Message[..40] + "…" : ex.Message;
            SetSlotStatus(slot, $"❌ {msg}");
            await Task.Delay(3000);
            SetSlotStatus(slot, "");
        }
        finally
        {
            slot.IsBusy = false;
        }
    }

    /// <summary>
    /// 응답한 슬롯을 제외한 다른 모든 슬롯에 해당 응답을 주입하고 전송합니다.
    /// 각 슬롯이 현재 작업 중이면 완료될 때까지 대기 후 주입합니다.
    /// </summary>
    private async Task CrossDispatchResponseAsync(BrowserSlot respondingSlot, string responseText)
    {
        var otherSlots = _leftSlots.Concat(_rightSlots)
                                   .Where(s => s != respondingSlot)
                                   .ToList();

        foreach (var slot in otherSlots)
        {
            // 해당 슬롯이 작업 완료될 때까지 대기 (최대 2분)
            var waitDeadline = DateTime.UtcNow.AddSeconds(120);
            while (slot.IsBusy && DateTime.UtcNow < waitDeadline)
                await Task.Delay(500);

            // 교차 배포: 이전 응답을 받아 계속 토론 진행
            _ = AutoInjectAndExtractAsync(slot, responseText, isCrossDispatch: false);
        }
    }

    private async Task WaitForSubmitEnabledAsync(BrowserSlot slot)
    {
        if (slot.Model is null || slot.WebView.CoreWebView2 is null) return;
        var submitSel = (slot.Model.SubmitSelector ?? "button[type=submit]").Replace("'", "\\'");
        var deadline  = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            await Task.Delay(500);
            if (slot.WebView.CoreWebView2 is null) return;
            var disabled = await slot.WebView.CoreWebView2.ExecuteScriptAsync(
                $"(document.querySelector('{submitSel}')?.disabled ?? false).toString()");
            if (disabled == "false" || disabled == "null") return;
        }
    }

    private void SetSlotStatus(BrowserSlot slot, string text)
    {
        Dispatcher.Invoke(() =>
        {
            if (slot.StatusText != null)
                slot.StatusText.Text = text;
        });
    }

    // ══════════════════════════════════════════════════════════════════════
    // 설정 창
    // ══════════════════════════════════════════════════════════════════════

    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
            RebuildUiFromSettings();
    }

    private void History_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("히스토리 기능은 준비 중입니다.", "알림",
               MessageBoxButton.OK, MessageBoxImage.Information);

    private void Statistics_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("통계 기능은 준비 중입니다.", "알림",
               MessageBoxButton.OK, MessageBoxImage.Information);

    // ══════════════════════════════════════════════════════════════════════
    // 새 토론 시작
    // ══════════════════════════════════════════════════════════════════════

    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        string? title = ShowInputDialog("새 토론 시작", "토론 주제를 입력하시오:", "새 토론");
        if (string.IsNullOrWhiteSpace(title)) return;

        _currentSession   = new Session { Name = title.Trim() };
        _debateTitle.Text = title.Trim();
        _lastPrompt       = string.Empty;

        _messages.Clear();
        UpdateMessageCount();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 입력 이벤트
    // ══════════════════════════════════════════════════════════════════════

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
        => _placeholder.Visibility = string.IsNullOrEmpty(_inputTextBox.Text)
               ? Visibility.Visible : Visibility.Collapsed;

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            _ = AddMessageAsync();
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e) => _ = AddMessageAsync();

    // ══════════════════════════════════════════════════════════════════════
    // 메시지 추가 & API 자동 디스패치
    // ══════════════════════════════════════════════════════════════════════

    private async Task AddMessageAsync()
    {
        string text = _inputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        _lastPrompt = text;

        var msg = new Message { Sender = "나", Content = text };
        _currentSession.Messages.Add(msg);
        _messages.Add(new MessageViewModel(msg));

        _inputTextBox.Clear();
        _inputTextBox.Focus();
        UpdateMessageCount();
        ScrollToBottom();

        // 브라우저 슬롯: 자동 주입 + 자동 응답 수집 (슬롯별 독립 실행)
        foreach (var slot in _leftSlots.Concat(_rightSlots))
            _ = AutoInjectAndExtractAsync(slot, text);

        // API 모드 모델 자동 응답
        await DispatchToApiModelsAsync(text);
    }

    private async Task DispatchToApiModelsAsync(string prompt)
    {
        var apiModels = _settings.Models
            .Where(m => m.Enabled && m.Mode == AiMode.Api)
            .ToList();

        if (apiModels.Count == 0) return;

        SetThinking(true, $"⏳  {apiModels.Count}개 API 모델 응답 대기 중...");
        _sendBtn.IsEnabled = false;

        await Task.WhenAll(apiModels.Select(m => AskApiModelAsync(m, prompt)));

        SetThinking(false);
        _sendBtn.IsEnabled = true;
    }

    private async Task AskApiModelAsync(AiModelConfig model, string prompt)
    {
        var service = AiServiceFactory.Create(model, _webViews);
        if (service is null) return;

        string responseText;
        try   { responseText = await service.AskAsync(prompt); }
        catch (Exception ex) { responseText = $"[오류] {ex.Message}"; }

        Dispatcher.Invoke(() =>
        {
            var reply = new Message { Sender = model.Name, Content = responseText };
            _currentSession.Messages.Add(reply);
            _messages.Add(new MessageViewModel(reply));
            UpdateMessageCount();
            ScrollToBottom();
        });
    }

    private void SetThinking(bool show, string? text = null)
    {
        _thinkingBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (text is not null) _thinkingText.Text = text;
    }

    // ══════════════════════════════════════════════════════════════════════
    // 메시지 컨텍스트 메뉴
    // ══════════════════════════════════════════════════════════════════════

    private void CopyMessage_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetContextMenuMessage(sender);
        if (vm is null) return;
        Clipboard.SetText(vm.Content);
    }

    private void DeleteMessage_Click(object sender, RoutedEventArgs e)
    {
        var vm = GetContextMenuMessage(sender);
        if (vm is null) return;
        _currentSession.Messages.Remove(vm.Source);
        _messages.Remove(vm);
        UpdateMessageCount();
    }

    // ══════════════════════════════════════════════════════════════════════
    // 저장
    // ══════════════════════════════════════════════════════════════════════

    private void SaveLog_Click(object sender, RoutedEventArgs e) => SaveLog();

    private void SaveLog()
    {
        if (_currentSession.Messages.Count == 0)
        {
            MessageBox.Show("저장할 대화 내용이 없소.", "알림",
                MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dlg = new Microsoft.Win32.SaveFileDialog
        {
            Filter   = "텍스트 파일 (*.txt)|*.txt",
            FileName = $"{_currentSession.Name}_{DateTime.Now:yyyyMMdd_HHmm}",
        };
        if (dlg.ShowDialog() != true) return;

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {_currentSession.Name}");
        sb.AppendLine($"# 저장일시: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        foreach (var m in _currentSession.Messages)
        {
            sb.AppendLine($"[{m.Sender}] {m.Timestamp:yyyy-MM-dd HH:mm}");
            sb.AppendLine(m.Content);
            sb.AppendLine();
        }
        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
        MessageBox.Show($"저장 완료:\n{dlg.FileName}", "저장",
            MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ══════════════════════════════════════════════════════════════════════
    // 유틸리티
    // ══════════════════════════════════════════════════════════════════════

    private void UpdateMessageCount()
        => _messageCountText.Text = $"{_messages.Count}개 메시지";

    private void ScrollToBottom()
        => Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
               new Action(() => _messageScrollViewer.ScrollToBottom()));

    private static SolidColorBrush HexBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static MessageViewModel? GetContextMenuMessage(object sender)
    {
        var mi = (MenuItem)sender;
        var cm = (ContextMenu)mi.Parent;
        return (cm.PlacementTarget as FrameworkElement)?.DataContext as MessageViewModel;
    }

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

    private string? ShowInputDialog(string title, string prompt, string defaultValue = "")
    {
        var dlg = new Window
        {
            Title                 = title,
            Width                 = 380,
            Height                = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner                 = this,
            ResizeMode            = ResizeMode.NoResize,
            FontFamily            = new FontFamily("맑은 고딕"),
            Background            = new SolidColorBrush(Colors.White),
        };
        var panel  = new StackPanel { Margin = new Thickness(20) };
        var lbl    = new TextBlock  { Text = prompt, Margin = new Thickness(0, 0, 0, 8),
                                      Foreground = HexBrush("#374151") };
        var input  = new TextBox    { Text = defaultValue,
                                      Padding = new Thickness(8, 6, 8, 6),
                                      BorderBrush = HexBrush("#E2E8F0"),
                                      BorderThickness = new Thickness(1) };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
                                      HorizontalAlignment = HorizontalAlignment.Right,
                                      Margin = new Thickness(0, 12, 0, 0) };
        var ok     = new Button { Content = "확인", Width = 76, Height = 32,
                                   Background = HexBrush("#3B82F6"),
                                   Foreground = new SolidColorBrush(Colors.White),
                                   BorderThickness = new Thickness(0),
                                   Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "취소", Width = 76, Height = 32 };

        string? result = null;
        ok.Click      += (_, _) => { result = input.Text; dlg.Close(); };
        cancel.Click  += (_, _) => dlg.Close();
        input.KeyDown += (_, e) => { if (e.Key == Key.Enter) { result = input.Text; dlg.Close(); } };

        btnRow.Children.Add(ok);
        btnRow.Children.Add(cancel);
        panel.Children.Add(lbl);
        panel.Children.Add(input);
        panel.Children.Add(btnRow);
        dlg.Content = panel;
        dlg.Loaded += (_, _) => { input.Focus(); input.SelectAll(); };
        dlg.ShowDialog();
        return result;
    }
}
