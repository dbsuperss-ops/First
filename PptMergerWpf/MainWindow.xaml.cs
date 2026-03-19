using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using D            = DocumentFormat.OpenXml.Drawing;
using WinForms     = System.Windows.Forms;
using WinColor     = System.Drawing.Color;
using MediaColor   = System.Windows.Media.Color;
using MediaFont    = System.Windows.Media.FontFamily;
using WpfMsgBox   = System.Windows.MessageBox;
using WpfSaveDlg  = Microsoft.Win32.SaveFileDialog;
using WpfColorCvt = System.Windows.Media.ColorConverter;

namespace PPT_Merger;

public partial class MainWindow : Window
{
    private MediaColor _selectedColor = (MediaColor)System.Windows.Media.ColorConverter.ConvertFromString("#1F1F1F")!;

    public MainWindow()
    {
        InitializeComponent();
        PopulateFonts();
        cmbAlign.SelectedIndex = 0;
        UpdatePreview();

        txtOutput.Text = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.Desktop),
            "통합_주간업무보고.pptx");
    }

    // ─── 폰트 목록 ───────────────────────────────────────────────────────────
    private void PopulateFonts()
    {
        string[] fonts = ["맑은 고딕", "굴림", "돋움", "바탕", "궁서",
                          "현대하모니 M", "현대하모니 B", "나눔고딕", "나눔명조"];
        foreach (var f in fonts) cmbFont.Items.Add(f);
        cmbFont.SelectedIndex = 0;
    }

    // ─── 폴더 선택 ───────────────────────────────────────────────────────────
    private void OnBrowseFolder(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.FolderBrowserDialog { Description = "PPTX 파일이 있는 폴더 선택" };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

        txtFolder.Text = dlg.SelectedPath;
        lstFiles.Items.Clear();

        var files = Directory.GetFiles(dlg.SelectedPath, "*.pptx", SearchOption.TopDirectoryOnly)
                             .OrderBy(f => f).ToArray();

        foreach (var f in files)
            lstFiles.Items.Add(new FileItem { Name = Path.GetFileName(f), FullPath = f, IsChecked = true });

        AppendLog(files.Length == 0
            ? "선택한 폴더에 PPTX 파일이 없습니다."
            : $"{files.Length}개 파일을 찾았습니다.");
    }

    // ─── 출력 파일 선택 ──────────────────────────────────────────────────────
    private void OnBrowseOutput(object sender, RoutedEventArgs e)
    {
        var dlg = new WpfSaveDlg
        {
            Filter = "PowerPoint 파일|*.pptx",
            FileName = "통합_주간업무보고.pptx",
            Title = "저장 위치 선택"
        };
        if (dlg.ShowDialog() == true)
            txtOutput.Text = dlg.FileName;
    }

    // ─── 전체 선택 / 해제 ────────────────────────────────────────────────────
    private void OnSelectAll(object sender, RoutedEventArgs e) => SetAllChecked(true);
    private void OnSelectNone(object sender, RoutedEventArgs e) => SetAllChecked(false);
    private void OnFileChecked(object sender, RoutedEventArgs e) { }

    private void SetAllChecked(bool check)
    {
        foreach (FileItem item in lstFiles.Items) item.IsChecked = check;
        lstFiles.Items.Refresh();
    }

    // ─── 색상 선택 ───────────────────────────────────────────────────────────
    private void OnPickColor(object sender, RoutedEventArgs e)
    {
        using var dlg = new WinForms.ColorDialog
        {
            Color = WinColor.FromArgb(_selectedColor.R, _selectedColor.G, _selectedColor.B),
            FullOpen = true
        };
        if (dlg.ShowDialog() != WinForms.DialogResult.OK) return;

        var c = dlg.Color;
        _selectedColor = MediaColor.FromRgb(c.R, c.G, c.B);
        btnColor.Content = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        btnColor.Background = new SolidColorBrush(_selectedColor);
        btnColor.Foreground = IsLightColor(_selectedColor)
            ? new SolidColorBrush(Colors.Black)
            : new SolidColorBrush(Colors.White);
        UpdatePreview();
    }

    private static bool IsLightColor(MediaColor c) =>
        (c.R * 299 + c.G * 587 + c.B * 114) / 1000 >= 128;

    // ─── 미리보기 ────────────────────────────────────────────────────────────
    private void OnFormatChanged(object sender, RoutedEventArgs e) => UpdatePreview();
    private void OnFormatChanged(object sender, SelectionChangedEventArgs e) => UpdatePreview();

    private void UpdatePreview()
    {
        if (txtPreview == null) return;
        txtPreview.FontFamily = new MediaFont(cmbFont.SelectedItem?.ToString() ?? "맑은 고딕");
        txtPreview.FontSize   = sliderFontSize.Value;
        txtPreview.FontWeight = chkBold.IsChecked == true ? FontWeights.Bold : FontWeights.Normal;
        txtPreview.Foreground = new SolidColorBrush(_selectedColor);
        txtPreview.TextAlignment = cmbAlign.SelectedIndex switch
        {
            1 => TextAlignment.Center,
            2 => TextAlignment.Right,
            3 => TextAlignment.Justify,
            _ => TextAlignment.Left,
        };
        txtPreview.LineHeight = txtPreview.FontSize * sliderLineSpacing.Value;
    }

    // ─── 통합 실행 ───────────────────────────────────────────────────────────
    private async void OnRun(object sender, RoutedEventArgs e)
    {
        var checkedFiles = lstFiles.Items
            .Cast<FileItem>()
            .Where(f => f.IsChecked)
            .Select(f => f.FullPath)
            .ToList();

        if (checkedFiles.Count == 0)
        {
            WpfMsgBox.Show("파일을 1개 이상 선택해주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }
        if (string.IsNullOrWhiteSpace(txtOutput.Text))
        {
            WpfMsgBox.Show("저장 위치를 지정해주세요.", "안내", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        btnRun.IsEnabled = false;
        txtLog.Clear();

        var keyword    = txtKeyword.Text.Trim();
        var outputPath = txtOutput.Text;
        var fmt = new FormatConfig
        {
            FontName    = cmbFont.SelectedItem?.ToString() ?? "맑은 고딕",
            FontSizePt  = sliderFontSize.Value,
            Bold        = chkBold.IsChecked == true,
            ColorHex    = $"#{_selectedColor.R:X2}{_selectedColor.G:X2}{_selectedColor.B:X2}",
            Align       = cmbAlign.SelectedIndex switch { 1 => "center", 2 => "right", 3 => "justify", _ => "left" },
            LineSpacing = sliderLineSpacing.Value,
        };

        try
        {
            await Task.Run(() => RunMerge(checkedFiles, keyword, outputPath, fmt));
            WpfMsgBox.Show("통합 완료!", "완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            AppendLog($"[오류] {ex.Message}");
            WpfMsgBox.Show(ex.Message, "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            btnRun.IsEnabled = true;
        }
    }

    private void RunMerge(IList<string> sourcePaths, string keyword, string outputPath, FormatConfig fmt)
    {
        // 첫 파일을 기반 템플릿으로 복사
        File.Copy(sourcePaths[0], outputPath, overwrite: true);

        using var destDoc = PresentationDocument.Open(outputPath, isEditable: true);
        var destPrs = destDoc.PresentationPart!;
        ClearAllSlides(destPrs);

        int totalCopied = 0;

        foreach (var srcPath in sourcePaths)
        {
            Log($"▶ {Path.GetFileName(srcPath)}");
            try
            {
                using var srcDoc = PresentationDocument.Open(srcPath, isEditable: false);
                var srcPrs = srcDoc.PresentationPart!;
                var slideIds = srcPrs.Presentation.SlideIdList!.Elements<SlideId>().ToList();

                for (int i = 0; i < slideIds.Count; i++)
                {
                    var srcSlidePart = (SlidePart)srcPrs.GetPartById(slideIds[i].RelationshipId!);

                    // 키워드 필터
                    if (!string.IsNullOrEmpty(keyword) && !SlideContainsKeyword(srcSlidePart, keyword))
                    {
                        Log($"  슬라이드 {i + 1}: 키워드 없음, 건너뜀");
                        continue;
                    }

                    CopySlide(srcPrs, i, destPrs);
                    totalCopied++;
                    Log($"  슬라이드 {i + 1} 복사 완료");
                }
            }
            catch (Exception ex)
            {
                Log($"  [오류] {ex.Message}");
            }
        }

        if (totalCopied == 0)
        {
            Log("⚠ 조건에 맞는 슬라이드가 없습니다.");
            return;
        }

        Log("서식 적용 중...");
        ApplyFormatToAll(destPrs, fmt);
        destPrs.Presentation.Save();
        Log($"저장 완료 → {outputPath} ({totalCopied}장)");
    }

    // ─── 키워드 검색 ─────────────────────────────────────────────────────────
    private static bool SlideContainsKeyword(SlidePart slidePart, string keyword)
    {
        var allText = slidePart.Slide.Descendants<D.Text>()
                               .Select(t => t.Text)
                               .Aggregate("", (acc, t) => acc + t);
        return allText.Contains(keyword, StringComparison.OrdinalIgnoreCase);
    }

    // ─── Open XML 슬라이드 작업 ──────────────────────────────────────────────
    private static void ClearAllSlides(PresentationPart prsPart)
    {
        var slideIds = prsPart.Presentation.SlideIdList!.Elements<SlideId>().ToList();
        foreach (var sid in slideIds)
            prsPart.DeletePart((SlidePart)prsPart.GetPartById(sid.RelationshipId!));
        prsPart.Presentation.SlideIdList!.RemoveAllChildren<SlideId>();
    }

    private static void CopySlide(PresentationPart srcPrs, int slideIndex, PresentationPart destPrs)
    {
        var srcSlideId   = srcPrs.Presentation.SlideIdList!.Elements<SlideId>().ElementAt(slideIndex);
        var srcSlidePart = (SlidePart)srcPrs.GetPartById(srcSlideId.RelationshipId!);
        var destSlidePart = destPrs.AddNewPart<SlidePart>();

        using (var s = srcSlidePart.GetStream())
            destSlidePart.FeedData(s);

        foreach (var partRef in srcSlidePart.Parts)
        {
            if (partRef.OpenXmlPart is ImagePart srcImg)
            {
                var destImg = destSlidePart.AddNewPart<ImagePart>(srcImg.ContentType, partRef.RelationshipId);
                using var imgStream = srcImg.GetStream();
                destImg.FeedData(imgStream);
            }
        }

        var destLayout = destPrs.SlideMasterParts.First().SlideLayoutParts.First();
        var srcLayoutRelId = srcSlidePart.GetIdOfPart(srcSlidePart.SlideLayoutPart!);
        destSlidePart.AddPart(destLayout, srcLayoutRelId);

        var slideList = destPrs.Presentation.SlideIdList!;
        uint maxId = slideList.Elements<SlideId>().Any()
            ? slideList.Elements<SlideId>().Max(s => s.Id!.Value) : 255u;

        slideList.Append(new SlideId
        {
            Id = maxId + 1,
            RelationshipId = destPrs.GetIdOfPart(destSlidePart)
        });
    }

    // ─── 서식 적용 ───────────────────────────────────────────────────────────
    private static void ApplyFormatToAll(PresentationPart prsPart, FormatConfig fmt)
    {
        foreach (var sid in prsPart.Presentation.SlideIdList!.Elements<SlideId>().ToList())
        {
            var slidePart = (SlidePart)prsPart.GetPartById(sid.RelationshipId!);
            ApplyFormatToSlide(slidePart.Slide, fmt);
        }
    }

    private static void ApplyFormatToSlide(Slide slide, FormatConfig fmt)
    {
        foreach (var txBody in slide.Descendants<D.TextBody>())
            ApplyFormat(txBody, fmt);
        foreach (var cell in slide.Descendants<D.TableCell>())
            if (cell.TextBody != null) ApplyFormat(cell.TextBody, fmt);
    }

    private static void ApplyFormat(OpenXmlElement txBody, FormatConfig fmt)
    {
        var align = fmt.Align switch
        {
            "center"  => D.TextAlignmentTypeValues.Center,
            "right"   => D.TextAlignmentTypeValues.Right,
            "justify" => D.TextAlignmentTypeValues.Justified,
            _         => D.TextAlignmentTypeValues.Left,
        };

        foreach (var para in txBody.Elements<D.Paragraph>())
        {
            var pPr = para.ParagraphProperties ?? para.PrependChild(new D.ParagraphProperties());
            pPr.Alignment = align;
            pPr.LineSpacing = new D.LineSpacing(
                new D.SpacingPercent { Val = (int)(fmt.LineSpacing * 100000) });

            foreach (var run in para.Elements<D.Run>())
            {
                var rPr = run.RunProperties ?? run.PrependChild(new D.RunProperties());
                rPr.FontSize = (int)(fmt.FontSizePt * 100);
                rPr.Bold = fmt.Bold ? true : (bool?)null;
                rPr.RemoveAllChildren<D.LatinFont>();
                rPr.Append(new D.LatinFont { Typeface = fmt.FontName });
                rPr.RemoveAllChildren<D.SolidFill>();
                rPr.RemoveAllChildren<D.GradientFill>();
                rPr.RemoveAllChildren<D.NoFill>();
                rPr.Append(new D.SolidFill(
                    new D.RgbColorModelHex { Val = fmt.ColorHex.TrimStart('#') }));
            }
        }
    }

    // ─── 로그 ────────────────────────────────────────────────────────────────
    private void AppendLog(string msg) =>
        Dispatcher.Invoke(() => { txtLog.AppendText(msg + "\n"); txtLog.ScrollToEnd(); });

    private void Log(string msg) => AppendLog(msg);
}

public class FileItem
{
    public string Name     { get; set; } = "";
    public string FullPath { get; set; } = "";
    public bool   IsChecked { get; set; } = true;
}

public class FormatConfig
{
    public string FontName    { get; set; } = "맑은 고딕";
    public double FontSizePt  { get; set; } = 11.0;
    public bool   Bold        { get; set; } = false;
    public string ColorHex    { get; set; } = "#1F1F1F";
    public string Align       { get; set; } = "left";
    public double LineSpacing { get; set; } = 1.2;
}
