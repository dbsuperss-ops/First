using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace AIRoundTable
{
    // ── 개선 #2: 발언자 이름 매직 스트링 → 상수화 ──────────────────────────
    internal static class Sender
    {
        public const string Me      = "나";
        public const string Gemini  = "제미나이";
        public const string Copilot = "코파일럿";
        public const string Claude  = "클로드";

        public static readonly string[] All = { Me, Gemini, Copilot, Claude };
    }

    public partial class MainForm : Form
    {
        // ── 발언자별 배경 색상 ──────────────────────────────────────────────
        private static readonly Color COLOR_ME      = Color.FromArgb(236, 236, 236);
        private static readonly Color COLOR_GEMINI  = Color.FromArgb(210, 227, 252);
        private static readonly Color COLOR_COPILOT = Color.FromArgb(209, 245, 217);
        private static readonly Color COLOR_CLAUDE  = Color.FromArgb(255, 229, 205);
        private static readonly Color COLOR_TEXT    = Color.FromArgb(30, 30, 30);

        // ── 개선 #8: 색상 조회를 Dictionary 로 통합 ────────────────────────
        private static readonly Dictionary<string, Color> SenderColors = new()
        {
            [Sender.Me]      = COLOR_ME,
            [Sender.Gemini]  = COLOR_GEMINI,
            [Sender.Copilot] = COLOR_COPILOT,
            [Sender.Claude]  = COLOR_CLAUDE,
        };

        // ── 개선 #3: 인스턴스 폰트 (Dispose 에서 해제) ─────────────────────
        private readonly Font _fontSender = new Font("맑은 고딕", 9f,  FontStyle.Bold);
        private readonly Font _fontMsg    = new Font("맑은 고딕", 10f, FontStyle.Regular);
        private readonly Font _fontTime   = new Font("맑은 고딕", 8f,  FontStyle.Regular);

        // ── 컨트롤 ─────────────────────────────────────────────────────────
        private RichTextBox _chatDisplay  = null!;
        private ComboBox    _aiSelector   = null!;
        private RichTextBox _inputBox     = null!;
        private Button      _sendBtn      = null!;
        private Button      _clearBtn     = null!;
        private Button      _saveBtn      = null!;
        private Label       _hintLabel    = null!;
        private Label       _statusLabel  = null!; // 개선 #9: 상태바

        // ── 내보내기용 평문 누적 ────────────────────────────────────────────
        private readonly System.Text.StringBuilder _plainLog = new();
        private int _messageCount = 0; // 개선 #9: 메시지 수 추적

        public MainForm()
        {
            InitializeComponent();
            BuildUI();
            // 개선 #6: Ctrl+S 저장 단축키
            this.KeyPreview = true;
            this.KeyDown   += MainForm_KeyDown;
        }

        // ── UI 구성 ────────────────────────────────────────────────────────
        private void BuildUI()
        {
            this.Text          = "AI 원탁회의";
            this.Size          = new Size(680, 760);
            this.MinimumSize   = new Size(520, 520);
            this.BackColor     = Color.FromArgb(245, 245, 245);
            this.Font          = _fontMsg;
            this.StartPosition = FormStartPosition.CenterScreen;

            // ── 상단 타이틀 바 ────────────────────────────────────────────
            Panel titleBar = new Panel
            {
                Dock      = DockStyle.Top,
                Height    = 44,
                BackColor = Color.FromArgb(40, 40, 40)
            };
            Label titleLabel = new Label
            {
                Text      = "🤝  AI 원탁회의",
                ForeColor = Color.White,
                Font      = new Font("맑은 고딕", 12f, FontStyle.Bold),
                Dock      = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding   = new Padding(12, 0, 0, 0)
            };
            _saveBtn         = MakeHeaderBtn("💾 저장");
            _saveBtn.Click  += SaveLog;
            _clearBtn        = MakeHeaderBtn("🗑 초기화");
            _clearBtn.Click += ClearChat;
            titleBar.Controls.Add(titleLabel);
            titleBar.Controls.Add(_clearBtn);
            titleBar.Controls.Add(_saveBtn);

            // ── 개선 #9: 하단 상태바 ─────────────────────────────────────
            Panel statusBar = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 24,
                BackColor = Color.FromArgb(220, 220, 220)
            };
            _statusLabel = new Label
            {
                Text      = "메시지: 0개",
                Dock      = DockStyle.Right,
                Width     = 120,
                TextAlign = ContentAlignment.MiddleRight,
                ForeColor = Color.FromArgb(80, 80, 80),
                Font      = new Font("맑은 고딕", 8f),
                Padding   = new Padding(0, 0, 8, 0)
            };
            statusBar.Controls.Add(_statusLabel);

            // ── 대화 표시 영역 ────────────────────────────────────────────
            _chatDisplay = new RichTextBox
            {
                Dock        = DockStyle.Fill,
                ReadOnly    = true,
                BackColor   = Color.White,
                BorderStyle = BorderStyle.None,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                Padding     = new Padding(8),
                Font        = _fontMsg
            };

            // ── 입력 패널 ─────────────────────────────────────────────────
            Panel inputPanel = new Panel
            {
                Dock      = DockStyle.Bottom,
                Height    = 140,
                BackColor = Color.FromArgb(230, 230, 230),
                Padding   = new Padding(8)
            };

            // 콤보박스
            _aiSelector = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Font          = new Font("맑은 고딕", 10f, FontStyle.Bold),
                Width         = 110,
                Location      = new Point(8, 8),
                Anchor        = AnchorStyles.Top | AnchorStyles.Left  // 개선 #4
            };
            _aiSelector.Items.AddRange(Sender.All);  // 개선 #2
            _aiSelector.SelectedIndex            = 0;
            _aiSelector.SelectedIndexChanged    += (s, e) => ColorizeSelector();
            ColorizeSelector();

            // 입력 텍스트박스 — 개선 #4: Anchor 로 리사이즈 대응
            _inputBox = new RichTextBox
            {
                Location    = new Point(8, 42),
                Size        = new Size(inputPanel.Width - 130, 76),
                Font        = _fontMsg,
                BorderStyle = BorderStyle.FixedSingle,
                ScrollBars  = RichTextBoxScrollBars.Vertical,
                Anchor      = AnchorStyles.Top | AnchorStyles.Left |
                              AnchorStyles.Right | AnchorStyles.Bottom  // 개선 #4
            };
            _inputBox.KeyDown += InputBox_KeyDown;

            // 추가 버튼 — 개선 #4: Anchor 우측 고정
            _sendBtn = new Button
            {
                Text      = "추가\n(Ctrl+↵)",
                Location  = new Point(inputPanel.Width - 118, 42),
                Size      = new Size(110, 76),
                BackColor = Color.FromArgb(50, 120, 220),
                ForeColor = Color.White,
                FlatStyle = FlatStyle.Flat,
                Font      = new Font("맑은 고딕", 9f, FontStyle.Bold),
                Cursor    = Cursors.Hand,
                Anchor    = AnchorStyles.Top | AnchorStyles.Right | AnchorStyles.Bottom  // 개선 #4
            };
            _sendBtn.FlatAppearance.BorderSize = 0;
            _sendBtn.Click += (s, e) => AddMessage();

            // 힌트 레이블 — 개선 #5: Bottom Anchor 로 잘림 방지
            _hintLabel = new Label
            {
                Text      = "각 AI 탭 응답을 붙여넣고 [추가] (Ctrl+Enter) | 저장: Ctrl+S",
                Location  = new Point(8, inputPanel.Height - 22),
                AutoSize  = false,
                Width     = inputPanel.Width - 16,
                Height    = 18,
                ForeColor = Color.Gray,
                Font      = new Font("맑은 고딕", 8f),
                Anchor    = AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right  // 개선 #5
            };

            inputPanel.Controls.Add(_aiSelector);
            inputPanel.Controls.Add(_inputBox);
            inputPanel.Controls.Add(_sendBtn);
            inputPanel.Controls.Add(_hintLabel);

            this.Controls.Add(_chatDisplay);
            this.Controls.Add(inputPanel);
            this.Controls.Add(statusBar);
            this.Controls.Add(titleBar);

            this.Shown += (s, e) => _inputBox.Focus();
        }

        // ── 개선 #6: Ctrl+S 저장 단축키 ───────────────────────────────────
        private void MainForm_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.S)
            {
                e.SuppressKeyPress = true;
                SaveLog(sender, e);
            }
        }

        // ── 헤더 버튼 팩토리 ───────────────────────────────────────────────
        private Button MakeHeaderBtn(string text)
        {
            var btn = new Button
            {
                Text      = text,
                Dock      = DockStyle.Right,
                Width     = 84,
                FlatStyle = FlatStyle.Flat,
                ForeColor = Color.White,
                BackColor = Color.FromArgb(40, 40, 40),
                Font      = new Font("맑은 고딕", 8.5f),
                Cursor    = Cursors.Hand
            };
            btn.FlatAppearance.BorderSize  = 0;
            btn.FlatAppearance.BorderColor = Color.FromArgb(40, 40, 40);
            return btn;
        }

        // ── 개선 #8: Dictionary 기반 콤보박스 색상 반영 ────────────────────
        private void ColorizeSelector()
        {
            string? selected = _aiSelector.SelectedItem?.ToString();
            if (selected != null && SenderColors.TryGetValue(selected, out Color c))
                _aiSelector.BackColor = c;
        }

        // ── Ctrl+Enter 단축키 ──────────────────────────────────────────────
        private void InputBox_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.Control && e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true;
                AddMessage();
            }
        }

        // ── 메시지 추가 핵심 로직 ──────────────────────────────────────────
        private void AddMessage()
        {
            // 개선 #1: null-safe SelectedItem 접근
            string sender  = _aiSelector.SelectedItem?.ToString() ?? Sender.Me;
            string message = _inputBox.Text.Trim();
            if (string.IsNullOrEmpty(message)) return;

            // 개선 #8: Dictionary 기반 색상 조회
            Color  bgColor  = SenderColors.GetValueOrDefault(sender, COLOR_ME);
            string timeStr  = DateTime.Now.ToString("HH:mm");

            AppendColoredLine($" {sender} ", _fontSender, Color.White, DarkenColor(bgColor), false);
            AppendColoredLine($"  {timeStr}", _fontTime, Color.Gray, Color.White, false);
            AppendNewLine();

            int    start      = _chatDisplay.TextLength;
            string paddedMsg  = "  " + message.Replace("\n", "\n  ") + "  ";
            _chatDisplay.AppendText(paddedMsg);
            int end = _chatDisplay.TextLength;
            _chatDisplay.Select(start, end - start);
            _chatDisplay.SelectionBackColor = bgColor;
            _chatDisplay.SelectionFont      = _fontMsg;
            _chatDisplay.SelectionColor     = COLOR_TEXT;
            _chatDisplay.Select(end, 0);

            AppendNewLine();
            AppendColoredLine("  ─────────────────────────────────", _fontTime, Color.LightGray, Color.White, true);
            _chatDisplay.ScrollToCaret();

            _plainLog.AppendLine($"[{sender}] {timeStr}");
            _plainLog.AppendLine(message);
            _plainLog.AppendLine();

            // 개선 #9: 메시지 카운터 갱신
            _messageCount++;
            _statusLabel.Text = $"메시지: {_messageCount}개";

            _inputBox.Clear();
            _inputBox.Focus();
        }

        // ── RichTextBox 헬퍼 ───────────────────────────────────────────────
        private void AppendColoredLine(string text, Font font, Color fg, Color bg, bool newLineBefore)
        {
            if (newLineBefore) _chatDisplay.AppendText("\n");
            int s = _chatDisplay.TextLength;
            _chatDisplay.AppendText(text + "\n");
            int e = _chatDisplay.TextLength;
            _chatDisplay.Select(s, e - s);
            _chatDisplay.SelectionFont      = font;
            _chatDisplay.SelectionColor     = fg;
            _chatDisplay.SelectionBackColor = bg;
            _chatDisplay.Select(e, 0);
        }

        private void AppendNewLine()
        {
            int s = _chatDisplay.TextLength;
            _chatDisplay.AppendText("\n");
            _chatDisplay.Select(s, 1);
            _chatDisplay.SelectionBackColor = Color.White;
            _chatDisplay.Select(_chatDisplay.TextLength, 0);
        }

        private static Color DarkenColor(Color c) =>
            Color.FromArgb(
                Math.Max(0, c.R - 60),
                Math.Max(0, c.G - 60),
                Math.Max(0, c.B - 60));

        // ── 초기화 ─────────────────────────────────────────────────────────
        private void ClearChat(object? sender, EventArgs e)
        {
            if (MessageBox.Show("대화 내용을 모두 지우겠소?", "초기화 확인",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _chatDisplay.Clear();
                _plainLog.Clear();
                _messageCount       = 0;          // 개선 #9
                _statusLabel.Text   = "메시지: 0개";
            }
        }

        // ── 개선 #7: TXT / RTF 선택 저장 ──────────────────────────────────
        private void SaveLog(object? sender, EventArgs e)
        {
            if (_plainLog.Length == 0)
            {
                MessageBox.Show("저장할 대화 내용이 없소.", "알림");
                return;
            }
            using SaveFileDialog dlg = new SaveFileDialog
            {
                Filter   = "텍스트 파일 (*.txt)|*.txt|서식 있는 텍스트 (*.rtf)|*.rtf",
                FileName = $"원탁회의_{DateTime.Now:yyyyMMdd_HHmm}"
            };
            if (dlg.ShowDialog() != DialogResult.OK) return;

            if (dlg.FilterIndex == 2) // RTF — 색상 보존
            {
                File.WriteAllText(dlg.FileName, _chatDisplay.Rtf ?? string.Empty,
                                  System.Text.Encoding.UTF8);
            }
            else                      // 평문 TXT
            {
                File.WriteAllText(dlg.FileName, _plainLog.ToString(),
                                  System.Text.Encoding.UTF8);
            }
            MessageBox.Show($"저장 완료:\n{dlg.FileName}", "저장");
        }
    }
}
