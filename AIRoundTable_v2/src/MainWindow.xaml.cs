using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using AIRoundTable.Models;

namespace AIRoundTable;

public partial class MainWindow : Window
{
    // ── 데이터 ─────────────────────────────────────────────────────────────
    private readonly ObservableCollection<Session>          _sessions = new();
    private readonly ObservableCollection<MessageViewModel> _messages = new();
    private Session? _currentSession;
    private string   _activeSender = "나";

    // ── 발언자 버튼 상태 테이블 ─────────────────────────────────────────────
    private record SenderStyle(
        Border    Btn, TextBlock Lbl,
        string    ActiveBg,   string ActiveFg,
        string    InactiveBg, string InactiveFg);

    private IReadOnlyList<SenderStyle> _senderStyles = null!;

    // ──────────────────────────────────────────────────────────────────────

    public MainWindow()
    {
        InitializeComponent();
        _senderStyles = BuildSenderStyles();

        _sessionListBox.ItemsSource = _sessions;
        _messageList.ItemsSource    = _messages;

        UpdateSenderButtonStates();
        SetActiveTab(showConversation: true);

        LoadSampleData();

        if (_sessions.Count > 0)
            _sessionListBox.SelectedIndex = 0;

        // Ctrl+S 전역 단축키
        KeyDown += (_, e) =>
        {
            if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
                SaveLog();
        };
    }

    // ── 샘플 데이터 ────────────────────────────────────────────────────────
    private void LoadSampleData()
    {
        var s1 = new Session
        {
            Name        = "전략 아이디어 원탁",
            Description = "AI 모델 간 아이디어 브레인스토밍",
        };
        AddSampleMessage(s1, "나",     "오늘 논의할 주제는 비용 절감형 AI 활용입니다.",                  -3);
        AddSampleMessage(s1, "제미나이", "API 비용 없이 복사/붙여넣기 워크플로가 합리적입니다.",           -2);
        AddSampleMessage(s1, "코파일럿", "UI 통합으로 맥락 유지가 중요합니다.",                           -1);
        AddSampleMessage(s1, "클로드",   "타임스탬프와 화자 태깅이 핵심입니다.",                           0);

        var s2 = new Session { Name = "기술 검토 회의",   Description = "기술 스택 및 아키텍처 검토" };
        var s3 = new Session { Name = "요약 정리",        Description = "회의 내용 요약 및 액션 아이템" };

        _sessions.Add(s1);
        _sessions.Add(s2);
        _sessions.Add(s3);
    }

    private static void AddSampleMessage(Session s, string sender, string text, int minuteOffset)
    {
        var baseTime = new DateTime(2026, 3, 23, 10, 1, 0);
        s.Messages.Add(new Message
        {
            Sender    = sender,
            Content   = text,
            Timestamp = baseTime.AddMinutes(minuteOffset + 1),
        });
    }

    // ── 탭 전환 ────────────────────────────────────────────────────────────
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

    // ── 세션 선택 ──────────────────────────────────────────────────────────
    private void SessionListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_sessionListBox.SelectedItem is not Session session) return;

        _currentSession = session;
        _sessionTitle.Text = session.Name;
        _sessionDesc.Text  = session.Description;

        _messages.Clear();
        foreach (var msg in session.Messages)
            _messages.Add(new MessageViewModel(msg));

        ScrollToBottom();
    }

    // ── 새 원탁회의 ────────────────────────────────────────────────────────
    private void NewSession_Click(object sender, RoutedEventArgs e)
    {
        string? name = ShowInputDialog("새 원탁회의", "회의 이름을 입력하시오:", "새 원탁회의");
        if (string.IsNullOrWhiteSpace(name)) return;

        var session = new Session
        {
            Name        = name.Trim(),
            Description = "AI 모델 간 브레인스토밍",
        };
        _sessions.Insert(0, session);
        _sessionListBox.SelectedItem = session;
    }

    // ── 세션 컨텍스트 메뉴 ─────────────────────────────────────────────────
    private void RenameSession_Click(object sender, RoutedEventArgs e)
    {
        var session = GetContextMenuSession(sender);
        if (session is null) return;

        string? name = ShowInputDialog("이름 편집", "새 이름을 입력하시오:", session.Name);
        if (string.IsNullOrWhiteSpace(name)) return;

        session.Name = name.Trim();
        if (_currentSession == session)
            _sessionTitle.Text = session.Name;
    }

    private void DeleteSession_Click(object sender, RoutedEventArgs e)
    {
        var session = GetContextMenuSession(sender);
        if (session is null) return;

        var result = MessageBox.Show(
            $"'{session.Name}' 세션을 삭제하시겠소?",
            "세션 삭제",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);
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

    // ── 메시지 컨텍스트 메뉴 ───────────────────────────────────────────────
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

    // ── 발언자 버튼 클릭 ───────────────────────────────────────────────────
    private void SenderBtn_Click(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border { Tag: string senderName })
        {
            _activeSender = senderName;
            UpdateSenderButtonStates();
        }
    }

    private void UpdateSenderButtonStates()
    {
        foreach (var s in _senderStyles)
        {
            bool active = s.Btn.Tag is string t && t == _activeSender;

            s.Btn.Background = HexBrush(active ? s.ActiveBg : s.InactiveBg);
            s.Lbl.Foreground = HexBrush(active ? s.ActiveFg : s.InactiveFg);
        }
    }

    // ── 입력창 이벤트 ──────────────────────────────────────────────────────
    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        _placeholder.Visibility = string.IsNullOrEmpty(_inputTextBox.Text)
            ? Visibility.Visible : Visibility.Collapsed;
    }

    private void InputTextBox_KeyDown(object sender, KeyEventArgs e)
    {
        // Enter 단독: 전송 / Shift+Enter: 줄바꿈
        if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift)
        {
            e.Handled = true;
            AddMessage();
        }
    }

    private void Send_Click(object sender, RoutedEventArgs e) => AddMessage();

    // ── 메시지 추가 ────────────────────────────────────────────────────────
    private void AddMessage()
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
    }

    // ── 저장 ──────────────────────────────────────────────────────────────
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

        foreach (var msg in _currentSession.Messages)
        {
            sb.AppendLine($"[{msg.Sender}] {msg.Timestamp:yyyy-MM-dd HH:mm}");
            sb.AppendLine(msg.Content);
            sb.AppendLine();
        }
        System.IO.File.WriteAllText(dlg.FileName, sb.ToString(), System.Text.Encoding.UTF8);
        MessageBox.Show($"저장 완료:\n{dlg.FileName}", "저장", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    // ── 유틸리티 ──────────────────────────────────────────────────────────
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

    private IReadOnlyList<SenderStyle> BuildSenderStyles() => new[]
    {
        new SenderStyle(_senderMeBtn,      _senderMeLbl,
                        "#3B82F6", "White",   "#0D1929", "#3B82F6"),
        new SenderStyle(_senderGeminiBtn,  _senderGeminiLbl,
                        "#10B981", "White",   "#081F15", "#10B981"),
        new SenderStyle(_senderCopilotBtn, _senderCopilotLbl,
                        "#0078D4", "White",   "#081526", "#0078D4"),
        new SenderStyle(_senderClaudeBtn,  _senderClaudeLbl,
                        "#D97706", "White",   "#1A1005", "#D97706"),
    };

    // ── 컨텍스트 메뉴 헬퍼 ────────────────────────────────────────────────
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

    // ── 간단한 입력 다이얼로그 ─────────────────────────────────────────────
    private string? ShowInputDialog(string title, string prompt, string defaultValue = "")
    {
        var dlg = new Window
        {
            Title  = title, Width = 380, Height = 170,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner  = this,
            ResizeMode   = ResizeMode.NoResize,
            FontFamily   = new FontFamily("Segoe UI"),
            Background   = HexBrush("#0F1626"),
        };
        var panel = new StackPanel { Margin = new Thickness(20) };
        var lbl   = new TextBlock  { Text = prompt, Margin = new Thickness(0, 0, 0, 8), Foreground = HexBrush("#94A3B8") };
        var input = new TextBox    { Text = defaultValue, Padding = new Thickness(8, 6, 8, 6),
                                     Background = HexBrush("#141B2D"), Foreground = HexBrush("#E2E8F0"),
                                     BorderBrush = HexBrush("#1E2A40"), BorderThickness = new Thickness(1),
                                     CaretBrush = HexBrush("#E2E8F0") };
        var btnRow = new StackPanel { Orientation = Orientation.Horizontal,
                                      HorizontalAlignment = HorizontalAlignment.Right,
                                      Margin = new Thickness(0, 12, 0, 0) };
        var ok     = new Button { Content = "확인", Width = 76, Height = 32,
                                   Background = HexBrush("#3B82F6"), Foreground = new SolidColorBrush(Colors.White),
                                   BorderThickness = new Thickness(0), Margin = new Thickness(0, 0, 8, 0) };
        var cancel = new Button { Content = "취소", Width = 76, Height = 32,
                                   Background = HexBrush("#1E2A40"), Foreground = HexBrush("#94A3B8"),
                                   BorderThickness = new Thickness(0) };

        string? result = null;
        ok.Click     += (_, _) => { result = input.Text; dlg.Close(); };
        cancel.Click += (_, _) => dlg.Close();
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
