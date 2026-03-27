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
    // ── 데이터 ─────────────────────────────────────────────────────────────
    private readonly ObservableCollection<MessageViewModel> _messages = new();
    private Session _currentSession = new() { Name = "새 토론" };

    // ── 설정 & AI ──────────────────────────────────────────────────────────
    private AppSettings _settings;
    private readonly Dictionary<string, WebView2>  _webViews            = new();
    private readonly Dictionary<string, TextBlock> _modelResponseBlocks = new();

    // ──────────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();
        _messageList.ItemsSource = _messages;

        RebuildUiFromSettings();
        LoadSampleData();

        KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                SaveLog();
        };
    }

    // ── 설정 기반 UI 재구성 ─────────────────────────────────────────────────
    private void RebuildUiFromSettings()
    {
        MessageViewModel.RegisterColors(_settings.Models);
        _modelResponseBlocks.Clear();

        _leftAiPanels.Children.Clear();
        _rightAiPanels.Children.Clear();

        var enabledModels = _settings.Models.Where(m => m.Enabled).ToList();
        int half = (enabledModels.Count + 1) / 2;

        for (int i = 0; i < enabledModels.Count; i++)
        {
            var model = enabledModels[i];
            var (panel, responseBlock) = BuildAiPanelCard(model);
            _modelResponseBlocks[model.Name] = responseBlock;

            if (i < half)
                _leftAiPanels.Children.Add(panel);
            else
                _rightAiPanels.Children.Add(panel);
        }

        // WebView2 재구성
        foreach (var wv in _webViews.Values) wv.Dispose();
        _webViews.Clear();
        _browserTabControl.Items.Clear();

        var browserModels = _settings.Models
            .Where(m => m.Enabled && m.Mode == AiMode.Browser).ToList();

        foreach (var model in browserModels)
        {
            var wv  = new WebView2 { HorizontalAlignment = HorizontalAlignment.Stretch };
            var tab = new TabItem  { Header = model.Name, Content = wv };
            _browserTabControl.Items.Add(tab);
            _webViews[model.Name] = wv;

            if (!string.IsNullOrWhiteSpace(model.SiteUrl))
            {
                var url = model.SiteUrl;
                wv.Loaded += async (_, _) =>
                {
                    await wv.EnsureCoreWebView2Async();
                    wv.Source = new Uri(url);
                };
            }
        }

        _browserExpander.Visibility = browserModels.Count > 0
            ? Visibility.Visible : Visibility.Collapsed;

        _activeModelCount.Text = $"{enabledModels.Count}개 모델 참여 중";
    }

    private (Border Panel, TextBlock ResponseBlock) BuildAiPanelCard(AiModelConfig model)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(model.Color); }
        catch { c = (Color)ColorConverter.ConvertFromString("#6B7280"); }

        var responseBlock = new TextBlock
        {
            Text         = "응답 대기 중...",
            FontSize     = 11,
            Foreground   = HexBrush("#94A3B8"),
            TextWrapping = TextWrapping.Wrap,
            MaxHeight    = 110,
        };

        var dot = new Border
        {
            Width             = 8,
            Height            = 8,
            CornerRadius      = new CornerRadius(4),
            Background        = new SolidColorBrush(c),
            VerticalAlignment = VerticalAlignment.Center,
            Margin            = new Thickness(0, 0, 7, 0),
        };

        var nameText = new TextBlock
        {
            Text              = model.Name,
            FontSize          = 12,
            FontWeight        = FontWeights.SemiBold,
            Foreground        = HexBrush("#0F172A"),
            VerticalAlignment = VerticalAlignment.Center,
        };

        // 엔진/모델 식별자 (우측 상단 회색 텍스트)
        var engineLabel = new TextBlock
        {
            Text              = !string.IsNullOrWhiteSpace(model.ModelId) ? model.ModelId : model.Mode.ToString(),
            FontSize          = 11,
            Foreground        = HexBrush("#94A3B8"),
            VerticalAlignment = VerticalAlignment.Center,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        var leftStack = new StackPanel { Orientation = Orientation.Horizontal };
        leftStack.Children.Add(dot);
        leftStack.Children.Add(nameText);

        var header = new Grid();
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        header.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Grid.SetColumn(leftStack, 0);
        Grid.SetColumn(engineLabel, 1);
        header.Children.Add(leftStack);
        header.Children.Add(engineLabel);

        var divider = new Border
        {
            Height     = 1,
            Background = HexBrush("#F1F5F9"),
            Margin     = new Thickness(0, 8, 0, 8),
        };

        var body = new StackPanel { Margin = new Thickness(12, 10, 12, 12) };
        body.Children.Add(header);
        body.Children.Add(divider);
        body.Children.Add(responseBlock);

        var panel = new Border
        {
            Background      = new SolidColorBrush(Colors.White),
            BorderBrush     = HexBrush("#E2E8F0"),
            BorderThickness = new Thickness(1),
            CornerRadius    = new CornerRadius(8),
            Margin          = new Thickness(0, 0, 0, 10),
            Child           = body,
        };

        return (panel, responseBlock);
    }

    // ── 설정 창 ───────────────────────────────────────────────────────────
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
            RebuildUiFromSettings();
    }

    private void History_Click(object sender, RoutedEventArgs e)
        => MessageBox.Show("히스토리 기능은 준비 중입니다.", "알림",
               MessageBoxButton.OK, MessageBoxImage.Information);

    // ── 새 토론 시작 ──────────────────────────────────────────────────────
    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        string? title = ShowInputDialog("새 토론 시작", "토론 주제를 입력하시오:", "새 토론");
        if (string.IsNullOrWhiteSpace(title)) return;

        _currentSession = new Session { Name = title.Trim() };
        _debateTitle.Text = title.Trim();
        _messages.Clear();
        UpdateMessageCount();

        foreach (var block in _modelResponseBlocks.Values)
        {
            block.Text       = "응답 대기 중...";
            block.Foreground = HexBrush("#94A3B8");
        }
    }

    // ── 입력 이벤트 ───────────────────────────────────────────────────────
    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _placeholder.Visibility = string.IsNullOrEmpty(_inputTextBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            _ = AddMessageAsync();
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e) => _ = AddMessageAsync();

    // ── 메시지 추가 & AI 디스패치 ─────────────────────────────────────────
    private async Task AddMessageAsync()
    {
        string text = _inputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var msg = new Message { Sender = "나", Content = text };
        _currentSession.Messages.Add(msg);
        _messages.Add(new MessageViewModel(msg));
        _inputTextBox.Clear();
        _inputTextBox.Focus();
        UpdateMessageCount();
        ScrollToBottom();

        await DispatchToAiModelsAsync(text);
    }

    private async Task DispatchToAiModelsAsync(string prompt)
    {
        var autoModels = _settings.Models
            .Where(m => m.Enabled && m.Mode != AiMode.Manual)
            .ToList();

        if (autoModels.Count == 0) return;

        SetThinking(true, $"⏳  {autoModels.Count}개 AI 모델 응답 대기 중...");
        _sendBtn.IsEnabled = false;

        await Task.WhenAll(autoModels.Select(m => AskModelAsync(m, prompt)));

        SetThinking(false);
        _sendBtn.IsEnabled = true;
    }

    private async Task AskModelAsync(AiModelConfig model, string prompt)
    {
        Dispatcher.Invoke(() =>
        {
            if (_modelResponseBlocks.TryGetValue(model.Name, out var b))
            {
                b.Text       = "응답 중...";
                b.Foreground = HexBrush("#3B82F6");
            }
        });

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

            if (_modelResponseBlocks.TryGetValue(model.Name, out var b))
            {
                b.Text       = responseText.Length > 200 ? responseText[..200] + "…" : responseText;
                b.Foreground = HexBrush("#374151");
            }
        });
    }

    private void SetThinking(bool show, string? text = null)
    {
        _thinkingBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (text is not null) _thinkingText.Text = text;
    }

    // ── 메시지 컨텍스트 메뉴 ─────────────────────────────────────────────
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

    // ── 저장 ─────────────────────────────────────────────────────────────
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

    // ── 샘플 데이터 ───────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        _debateTitle.Text    = "비용 절감형 AI 활용 전략";
        _currentSession.Name = _debateTitle.Text;

        static Message Msg(string sender, string text, int offsetMin)
        {
            var t = new DateTime(2026, 3, 27, 10, 0, 0).AddMinutes(offsetMin);
            return new Message { Sender = sender, Content = text, Timestamp = t };
        }

        var samples = new[]
        {
            Msg("나",      "오늘 논의할 주제는 비용 절감형 AI 활용입니다.",            0),
            Msg("ChatGPT", "API 비용 없이 복사/붙여넣기 워크플로가 합리적입니다.",     1),
            Msg("코파일럿", "UI 통합으로 맥락 유지가 중요합니다.",                      2),
            Msg("클로드",   "타임스탬프와 화자 태깅이 핵심입니다.",                      3),
        };

        foreach (var m in samples)
        {
            _currentSession.Messages.Add(m);
            _messages.Add(new MessageViewModel(m));

            if (_modelResponseBlocks.TryGetValue(m.Sender, out var block))
            {
                block.Text       = m.Content;
                block.Foreground = HexBrush("#374151");
            }
        }

        UpdateMessageCount();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            new Action(() => _messageScrollViewer.ScrollToBottom()));
    }

    // ── 유틸리티 ─────────────────────────────────────────────────────────
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
