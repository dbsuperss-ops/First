using PptxMerger.Models;
using PptxMerger.Services;

namespace PptxMerger;

public class MainForm : Form
{
    // ─── 컨트롤 ──────────────────────────────────────────────────────────────
    private readonly TextBox        _folderBox      = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly CheckedListBox _fileList       = new() { Dock = DockStyle.Fill, CheckOnClick = true };
    private readonly ComboBox       _fontCombo      = new() { Width = 180 };
    private readonly NumericUpDown  _fontSizeNum    = new() { DecimalPlaces = 1, Minimum = 6, Maximum = 96, Value = 11, Width = 70 };
    private readonly CheckBox       _boldCheck      = new() { Text = "굵게", AutoSize = true };
    private readonly Button         _colorBtn       = new() { Width = 90, Height = 24, Text = "#1F1F1F" };
    private readonly ComboBox       _alignCombo     = new() { Width = 90, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown  _lineSpacingNum = new() { DecimalPlaces = 2, Minimum = 0.5m, Maximum = 5, Increment = 0.1m, Value = 1.2m, Width = 70 };
    private readonly NumericUpDown  _charSpacingNum = new() { DecimalPlaces = 1, Minimum = -20, Maximum = 200, Value = 0, Width = 70 };
    private readonly TextBox        _outputBox      = new() { ReadOnly = true, Dock = DockStyle.Fill };
    private readonly Button         _runBtn         = new() { Text = "통합 실행", Height = 38, Dock = DockStyle.Fill, Font = new Font("맑은 고딕", 11, FontStyle.Bold) };
    private readonly TextBox        _logBox         = new() { Multiline = true, ReadOnly = true, ScrollBars = ScrollBars.Vertical, Dock = DockStyle.Fill, Font = new Font("Consolas", 9) };

    private Color _selectedColor = ColorTranslator.FromHtml("#1F1F1F");

    public MainForm()
    {
        Text = "PPTX 통합 도구";
        Size = new Size(700, 780);
        MinimumSize = new Size(600, 700);
        Font = new Font("맑은 고딕", 9.5f);
        Padding = new Padding(12);

        BuildUI();
        WireEvents();
        PopulateFonts();

        _alignCombo.Items.AddRange(["왼쪽", "가운데", "오른쪽", "양쪽"]);
        _alignCombo.SelectedIndex = 0;
    }

    // ─── UI 구성 ─────────────────────────────────────────────────────────────
    private void BuildUI()
    {
        var main = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 5, ColumnCount = 1 };
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 70));   // 소스 폴더
        main.RowStyles.Add(new RowStyle(SizeType.Percent,  42));   // 파일 목록
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 130));  // 서식
        main.RowStyles.Add(new RowStyle(SizeType.Absolute, 60));   // 출력
        main.RowStyles.Add(new RowStyle(SizeType.Percent,  58));   // 버튼 + 로그

        main.Controls.Add(BuildFolderGroup(),  0, 0);
        main.Controls.Add(BuildFileListGroup(), 0, 1);
        main.Controls.Add(BuildFormatGroup(),  0, 2);
        main.Controls.Add(BuildOutputGroup(),  0, 3);
        main.Controls.Add(BuildBottomPanel(), 0, 4);

        Controls.Add(main);
    }

    private GroupBox BuildFolderGroup()
    {
        var gb = MakeGroup("소스 폴더");
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        var browseBtn = MakeButton("찾아보기");
        browseBtn.Click += OnBrowseFolder;
        row.Controls.Add(_folderBox, 0, 0);
        row.Controls.Add(browseBtn, 1, 0);
        gb.Controls.Add(row);
        return gb;
    }

    private GroupBox BuildFileListGroup()
    {
        var gb = MakeGroup("PPTX 파일 목록");
        var layout = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 34));
        layout.Controls.Add(_fileList, 0, 0);

        var btnRow = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.LeftToRight };
        var selectAll = MakeButton("전체 선택", 80);
        var selectNone = MakeButton("전체 해제", 80);
        selectAll.Click  += (_, _) => SetAllChecked(true);
        selectNone.Click += (_, _) => SetAllChecked(false);
        btnRow.Controls.AddRange([selectAll, selectNone]);
        layout.Controls.Add(btnRow, 0, 1);

        gb.Controls.Add(layout);
        return gb;
    }

    private GroupBox BuildFormatGroup()
    {
        var gb = MakeGroup("서식 설정");
        var layout = new FlowLayoutPanel { Dock = DockStyle.Fill, WrapContents = true, Padding = new Padding(2) };

        layout.Controls.AddRange([
            Label("폰트:"), _fontCombo,
            Spacer(8),
            Label("크기:"), _fontSizeNum, Label("pt"),
            Spacer(8),
            _boldCheck,
        ]);

        _colorBtn.BackColor = _selectedColor;
        _colorBtn.ForeColor = ContrastColor(_selectedColor);
        _colorBtn.Click += OnPickColor;
        layout.Controls.AddRange([
            Spacer(16),
            Label("색상:"), _colorBtn,
            Spacer(8),
            Label("정렬:"), _alignCombo,
        ]);

        layout.Controls.AddRange([
            Spacer(16),
            Label("줄간격:"), _lineSpacingNum,
            Spacer(8),
            Label("자간:"), _charSpacingNum, Label("pt"),
        ]);

        gb.Controls.Add(layout);
        return gb;
    }

    private GroupBox BuildOutputGroup()
    {
        var gb = MakeGroup("출력 파일");
        var row = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2 };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 80));
        var browseBtn = MakeButton("저장 위치");
        browseBtn.Click += OnBrowseOutput;
        row.Controls.Add(_outputBox, 0, 0);
        row.Controls.Add(browseBtn, 1, 0);
        gb.Controls.Add(row);
        return gb;
    }

    private Panel BuildBottomPanel()
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2, ColumnCount = 1 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 48));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        _runBtn.Click += OnRun;
        panel.Controls.Add(_runBtn, 0, 0);
        var logGroup = MakeGroup("처리 로그");
        logGroup.Controls.Add(_logBox);
        panel.Controls.Add(logGroup, 0, 1);
        return panel;
    }

    // ─── 이벤트 ──────────────────────────────────────────────────────────────
    private void WireEvents() { /* 개별 컨트롤에서 직접 연결 */ }

    private void OnBrowseFolder(object? s, EventArgs e)
    {
        using var dlg = new FolderBrowserDialog { Description = "PPTX 파일이 있는 폴더 선택" };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _folderBox.Text = dlg.SelectedPath;
        _fileList.Items.Clear();

        var files = Directory.GetFiles(dlg.SelectedPath, "*.pptx", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f)
                             .ToArray();
        foreach (var f in files)
            _fileList.Items.Add(Path.GetFileName(f), true);

        if (files.Length == 0)
            AppendLog("선택한 폴더에 PPTX 파일이 없습니다.");
        else
            AppendLog($"{files.Length}개 파일을 찾았습니다.");
    }

    private void OnBrowseOutput(object? s, EventArgs e)
    {
        using var dlg = new SaveFileDialog
        {
            Filter = "PowerPoint 파일|*.pptx",
            FileName = "통합_주간업무.pptx",
            Title = "저장 위치 선택"
        };
        if (dlg.ShowDialog() == DialogResult.OK)
            _outputBox.Text = dlg.FileName;
    }

    private void OnPickColor(object? s, EventArgs e)
    {
        using var dlg = new ColorDialog { Color = _selectedColor, FullOpen = true };
        if (dlg.ShowDialog() != DialogResult.OK) return;

        _selectedColor = dlg.Color;
        _colorBtn.BackColor = _selectedColor;
        _colorBtn.ForeColor = ContrastColor(_selectedColor);
        _colorBtn.Text = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}";
    }

    private async void OnRun(object? s, EventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_folderBox.Text) || _fileList.CheckedItems.Count == 0)
        {
            MessageBox.Show("폴더와 파일을 먼저 선택해주세요.", "안내",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(_outputBox.Text))
        {
            MessageBox.Show("저장 위치를 지정해주세요.", "안내",
                MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _runBtn.Enabled = false;
        _logBox.Clear();

        var selectedFiles = _fileList.CheckedItems
            .Cast<string>()
            .Select(name => Path.Combine(_folderBox.Text, name))
            .ToList();

        var fmt = new FormatConfig
        {
            FontName    = _fontCombo.Text,
            FontSizePt  = (double)_fontSizeNum.Value,
            Bold        = _boldCheck.Checked,
            ColorHex    = _colorBtn.Text,
            Align       = _alignCombo.SelectedIndex switch { 1 => "center", 2 => "right", 3 => "justify", _ => "left" },
            LineSpacing = (double)_lineSpacingNum.Value,
            CharSpacing = (double)_charSpacingNum.Value,
        };

        try
        {
            var svc = new PptxMergeService();
            svc.LogMessage += msg => BeginInvoke(() => AppendLog(msg));

            await Task.Run(() => svc.Merge(selectedFiles, _outputBox.Text, fmt));

            MessageBox.Show("통합 완료!", "완료", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"[오류] {ex.Message}");
            MessageBox.Show(ex.Message, "오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
        finally
        {
            _runBtn.Enabled = true;
        }
    }

    // ─── 헬퍼 ────────────────────────────────────────────────────────────────
    private void AppendLog(string msg)
    {
        _logBox.AppendText(msg + Environment.NewLine);
        _logBox.ScrollToCaret();
    }

    private void SetAllChecked(bool check)
    {
        for (int i = 0; i < _fileList.Items.Count; i++)
            _fileList.SetItemChecked(i, check);
    }

    private void PopulateFonts()
    {
        string[] koreanFonts = ["맑은 고딕", "굴림", "돋움", "바탕", "궁서",
                                 "현대하모니 M", "현대하모니 B", "나눔고딕", "나눔명조"];
        _fontCombo.Items.AddRange(koreanFonts);
        _fontCombo.SelectedIndex = 0;
        _fontCombo.DropDownStyle = ComboBoxStyle.DropDownList;
    }

    private static Color ContrastColor(Color c) =>
        (c.R * 299 + c.G * 587 + c.B * 114) / 1000 >= 128 ? Color.Black : Color.White;

    private static GroupBox MakeGroup(string title) =>
        new() { Text = title, Dock = DockStyle.Fill, Padding = new Padding(6) };

    private static Button MakeButton(string text, int width = 80) =>
        new() { Text = text, Width = width, Height = 26 };

    private static Label Label(string text) =>
        new() { Text = text, AutoSize = true, TextAlign = ContentAlignment.MiddleLeft };

    private static Panel Spacer(int width) =>
        new() { Width = width, Height = 1 };
}
