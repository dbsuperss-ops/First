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
    private readonly ObservableCollection<Session>          _sessions = new();
    private readonly ObservableCollection<MessageViewModel> _messages = new();
    private Session? _currentSession;
    private string   _activeSender = "나";

    // ── 설정 & AI ──────────────────────────────────────────────────────────
    private AppSettings _settings;
    private readonly Dictionary<string, WebView2> _webViews = new();

    // ── 동적 발언자 버튼 ─────────────────────────────────────────────────────
    private readonly Dictionary<string, (Border Btn, TextBlock Lbl, string Color)> _dynamicBtns = new();

    // ──────────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        _settings = AppSettings.Load();

        _sessionListBox.ItemsSource = _sessions;
        _messageList.ItemsSource    = _messages;

        RebuildUiFromSettings();
        SetActiveTab(showConversation: true);
        LoadSampleData();

        if (_sessions.Count > 0)
            _sessionListBox.SelectedIndex = 0;

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

        // 동적 발언자 버튼
        _dynamicSenderButtons.Children.Clear();
        _dynamicBtns.Clear();
        foreach (var model in _settings.Models.Where(m => m.Enabled))
        {
            var (btn, lbl) = CreateDynamicSenderButton(model);
            _dynamicSenderButtons.Children.Add(btn);
            _dynamicBtns[model.Name] = (btn, lbl, model.Color);
        }

        // 참가자 카드
        _participantCards.Children.Clear();
        foreach (var model in _settings.Models.Where(m => m.Enabled))
            _participantCards.Children.Add(BuildParticipantCard(model));

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

        if (_activeSender != "나" && !_dynamicBtns.ContainsKey(_activeSender))
            _activeSender = "나";

        UpdateAllSenderButtonStates();
    }

    private (Border Btn, TextBlock Lbl) CreateDynamicSenderButton(AiModelConfig model)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(model.Color); }
        catch { c = (Color)ColorConverter.ConvertFromString("#6B7280"); }

        var lbl = new TextBlock
        {
            Text = model.Name.Length > 0 ? model.Name[0].ToString() : "?",
            Foreground          = HexBrush("#6B7280"),
            FontWeight          = FontWeights.Bold,
            FontSize            = 12,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment   = VerticalAlignment.Center,
        };

        var btn = new Border
        {
            Width        = 36, Height = 36,
            CornerRadius = new CornerRadius(18),
            Background   = new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B)),
            Cursor       = Cursors.Hand,
            Margin       = new Thickness(0, 0, 8, 0),
            Tag          = model.Name,
            Child        = lbl,
        };
        btn.MouseDown += SenderBtn_Click;

        return (btn, lbl);
    }

    private Border BuildParticipantCard(AiModelConfig model)
    {
        Color c;
        try { c = (Color)ColorConverter.ConvertFromString(model.Color); }
        catch { c = (Color)ColorConverter.ConvertFromString("#6B7280"); }

        var avatar = new Border
        {
            Width = 48, Height = 48, CornerRadius = new CornerRadius(24),
            Background          = new SolidColorBrush(c),
            HorizontalAlignment = HorizontalAlignment.Center,
            Margin = new Thickness(0, 0, 0, 8),
            Child  = new TextBlock
            {
                Text       = model.Name.Length > 0 ? model.Name[0].ToString() : "?",
                Foreground = Brushes.White,
                FontWeight = FontWeights.Bold, FontSize = 18,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment   = VerticalAlignment.Center,
            },
        };

        var modeBadge = new Border
        {
            CornerRadius        = new CornerRadius(10),
            Padding             = new Thickness(8, 2, 8, 2),
            Margin              = new Thickness(0, 4, 0, 0),
            HorizontalAlignment = HorizontalAlignment.Center,
            Background = model.Mode switch
            {
                AiMode.Api     => new SolidColorBrush(Color.FromRgb(0xD1, 0xFA, 0xE5)),
                AiMode.Browser => new SolidColorBrush(Color.FromRgb(0xDB, 0xEA, 0xFE)),
                _              => new SolidColorBrush(Color.FromRgb(0xF3, 0xF4, 0xF6)),
            },
            Child = new TextBlock
            {
                Text     = model.Mode.ToString(),
                FontSize = 11,
                Foreground = model.Mode switch
                {
                    AiMode.Api     => new SolidColorBrush(Color.FromRgb(0x06, 0x5F, 0x46)),
                    AiMode.Browser => new SolidColorBrush(Color.FromRgb(0x1D, 0x4E, 0xD8)),
                    _              => new SolidColorBrush(Color.FromRgb(0x6B, 0x72, 0x80)),
                },
            },
        };

        var content = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center };
        content.Children.Add(avatar);
        content.Children.Add(new TextBlock
        {
            Text                = model.Name,
            FontWeight          = FontWeights.SemiBold, FontSize = 13,
            HorizontalAlignment = HorizontalAlignment.Center,
        });
        content.Children.Add(modeBadge);

        return new Border
        {
            Width        = 140,
            Margin       = new Thickness(0, 0, 16, 16),
            Background   = new SolidColorBrush(Color.FromArgb(30, c.R, c.G, c.B)),
            CornerRadius = new CornerRadius(12),
            Padding      = new Thickness(16, 20, 16, 20),
            Child        = content,
        };
    }

    // ── 설정 창 ───────────────────────────────────────────────────────────
    private void Settings_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new SettingsWindow(_settings) { Owner = this };
        if (dlg.ShowDialog() == true)
            RebuildUiFromSettings();
    }

    // ── 탭 전환 ───────────────────────────────────────────────────────────
    private void TabConversation_Click(object sender, RoutedEventArgs e) => SetActiveTab(true);
    private void TabParticipants_Click(object sender, RoutedEventArgs e) => SetActiveTab(false);

    private void SetActiveTab(bool showConversation)
    {
        _messageScrollViewer.Visibility = showConversation ? Visibility.Visible   : Visibility.Collapsed;
        _participantsView.Visibility    = showConversation ? Visibility.Collapsed : Visibility.Visible;
        _inputArea.Visibility           = showConversation ? Visibility.Visible   : Visibility.Collapsed;

        _tabConversation.Style = (Style)FindResource(showConversation ? "ActiveTabButtonStyle" : "OutlineButtonStyle");
        _tabParticipants.Style = (Style)FindResource(showConversation ? "OutlineButtonStyle"   : "ActiveTabButtonStyle");
    }

    // ── 세션 ─────────────────────────────────────────────────────────────
    private void SessionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_sessionListBox.SelectedItem is not Session session) return;
        _currentSession    = session;
        _sessionTitle.Text = session.Name;
        _sessionDesc.Text  = session.Description;
        _messages.Clear();
        foreach (var msg in session.Messages)
            _messages.Add(new MessageViewModel(msg));
        ScrollToBottom();
    }

    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        string? name = ShowInputDialog("새 원탁회의", "회의 이름을 입력하시오:", "새 원탁회의");
        if (string.IsNullOrWhiteSpace(name)) return;
        var session = new Session { Name = name.Trim(), Description = "AI 모델 간 브레인스토밍" };
        _sessions.Insert(0, session);
        _sessionListBox.SelectedItem = session;
    }

    private void RenameSession_Click(object sender, RoutedEventArgs e)
    {
        var session = GetContextMenuSession(sender);
        if (session is null) return;
        string? name = ShowInputDialog("이름 편집", "새 이름을 입력하시오:", session.Name);
        if (string.IsNullOrWhiteSpace(name)) return;
        session.Name = name.Trim();
        if (_currentSession == session) _sessionTitle.Text = session.Name;
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        var session = GetContextMenuSession(sender);
        if (session is null) return;
        var result = MessageBox.Show($"'{session.Name}' 세션을 삭제하시겠소?",
            "세션 삭제", MessageBoxButton.YesNo, MessageBoxImage.Warning);
        if (result != MessageBoxResult.Yes) return;
        if (_currentSession == session)
        {
            _currentSession    = null;
            _sessionTitle.Text = "세션을 선택하시오";
            _sessionDesc.Text  = "";
            _messages.Clear();
        }
        _sessions.Remove(session);
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
        if (vm is null || _currentSession is null) return;
        _currentSession.Messages.Remove(vm.Source);
        _messages.Remove(vm);
    }

    // ── 발언자 버튼 ───────────────────────────────────────────────────────
    private void SenderBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: string name })
        {
            _activeSender = name;
            UpdateAllSenderButtonStates();
        }
    }

    private void UpdateAllSenderButtonStates()
    {
        bool meActive = _activeSender == "나";
        _senderMeBtn.Background = HexBrush(meActive ? "#7C3AED" : "#F3F4F6");
        _senderMeLbl.Foreground = HexBrush(meActive ? "White"   : "#6B7280");

        foreach (var (name, (btn, lbl, colorHex)) in _dynamicBtns)
        {
            bool active = _activeSender == name;
            if (active)
            {
                btn.Background = HexBrush(colorHex);
                lbl.Foreground = Brushes.White;
            }
            else
            {
                Color c;
                try { c = (Color)ColorConverter.ConvertFromString(colorHex); }
                catch { c = (Color)ColorConverter.ConvertFromString("#6B7280"); }
                btn.Background = new SolidColorBrush(Color.FromArgb(40, c.R, c.G, c.B));
                lbl.Foreground = HexBrush("#6B7280");
            }
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
        if (_currentSession is null)
        {
            MessageBox.Show("먼저 세션을 선택하시오.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        string text = _inputTextBox.Text.Trim();
        if (string.IsNullOrEmpty(text)) return;

        var msg = new Message { Sender = _activeSender, Content = text };
        _currentSession.Messages.Add(msg);
        _messages.Add(new MessageViewModel(msg));
        _inputTextBox.Clear();
        _inputTextBox.Focus();
        ScrollToBottom();

        if (_activeSender == "나")
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
        var service = AiServiceFactory.Create(model, _webViews);
        if (service is null) return;

        string responseText;
        try
        {
            responseText = await service.AskAsync(prompt);
        }
        catch (Exception ex)
        {
            responseText = $"[오류] {ex.Message}";
        }

        Dispatcher.Invoke(() =>
        {
            if (_currentSession is null) return;
            var reply = new Message { Sender = model.Name, Content = responseText };
            _currentSession.Messages.Add(reply);
            _messages.Add(new MessageViewModel(reply));
            ScrollToBottom();
        });
    }

    private void SetThinking(bool show, string? text = null)
    {
        _thinkingBar.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        if (text is not null) _thinkingText.Text = text;
    }

    // ── 저장 ─────────────────────────────────────────────────────────────
    private void SaveLog()
    {
        if (_currentSession is null || _currentSession.Messages.Count == 0)
        {
            MessageBox.Show("저장할 대화 내용이 없소.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
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
        sb.AppendLine($"# {_currentSession.Description}");
        sb.AppendLine($"# 저장일시: {DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine();
        foreach (var m in _currentSession.Messages)
        {
            sb.AppendLine($"[{m.Sender}] {m.Timestamp:yyyy-MM-dd HH:mm}");
            sb.AppendLine(m.Content);
            sb.AppendLine();
        }
        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
        MessageBox.Show($"저장 완료:\n{dlg.FileName}", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── 샘플 데이터 ───────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        var s1 = new Session
        {
            Name        = "전략 아이디어 원탁",
            Description = "AI 모델 간 아이디어 브레인스토밍",
        };

        static void Add(Session s, string sender, string text, int offset)
        {
            var t = new DateTime(2026, 3, 23, 10, 1, 0).AddMinutes(offset + 1);
            s.Messages.Add(new Message { Sender = sender, Content = text, Timestamp = t });
        }

        Add(s1, "나",      "오늘 논의할 주제는 비용 절감형 AI 활용입니다.",           -3);
        Add(s1, "ChatGPT", "API 비용 없이 복사/붙여넣기 워크플로가 합리적입니다.",     -2);
        Add(s1, "코파일럿", "UI 통합으로 맥락 유지가 중요합니다.",                      -1);
        Add(s1, "클로드",   "타임스탬프와 화자 태깅이 핵심입니다.",                      0);

        _sessions.Add(s1);
        _sessions.Add(new Session { Name = "기술 검토 회의",  Description = "기술 스택 및 아키텍처 검토" });
        _sessions.Add(new Session { Name = "요약 정리",       Description = "회의 내용 요약 및 액션 아이템" });
    }

    // ── 유틸리티 ─────────────────────────────────────────────────────────
    private void ScrollToBottom()
    {
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded,
            new Action(() => _messageScrollViewer.ScrollToBottom()));
    }

    private static SolidColorBrush HexBrush(string hex)
    {
        var b = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        b.Freeze();
        return b;
    }

    private static Session? GetContextMenuSession(object sender)
    {
        var mi = (MenuItem)sender;
        var cm = (ContextMenu)mi.Parent;
        return (cm.PlacementTarget as FrameworkElement)?.DataContext as Session;
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
            Title  = title, Width = 380, Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner       = this,
            ResizeMode  = ResizeMode.NoResize,
            FontFamily  = new FontFamily("맑은 고딕"),
            Background  = new SolidColorBrush(Colors.White),
        };
        var panel  = new StackPanel { Margin = new Thickness(20) };
        var lbl    = new TextBlock  { Text = prompt, Margin = new Thickness(0, 0, 0, 8), Foreground = HexBrush("#374151") };
        var input  = new TextBox    { Text = defaultValue, Padding = new Thickness(8, 6, 8, 6),
                                      BorderBrush = HexBrush("#E5E7EB"), BorderThickness = new Thickness(1) };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
                                      HorizontalAlignment = HorizontalAlignment.Right,
                                      Margin = new Thickness(0, 12, 0, 0) };
        var ok     = new Button { Content = "확인", Width = 76, Height = 32,
                                   Background = HexBrush("#7C3AED"), Foreground = new SolidColorBrush(Colors.White),
                                   BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "취소",  Width = 76, Height = 32 };

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
