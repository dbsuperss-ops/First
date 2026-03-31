using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Microsoft.Win32;

namespace AttendanceGenerator;

public partial class MainWindow : Window
{
    private string _outputDir = string.Empty;

    public MainWindow()
    {
        InitializeComponent();
        var now = DateTime.Today;
        DpStart.SelectedDate = new DateTime(now.Year, now.Month, 1);
        DpEnd.SelectedDate   = new DateTime(now.Year, now.Month,
            DateTime.DaysInMonth(now.Year, now.Month));
    }

    private void BtnSelectFile_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFileDialog { Filter = "Excel 파일 (*.xlsx)|*.xlsx", Title = "명단 파일 선택" };
        if (dlg.ShowDialog() != true) return;
        TxtRosterPath.Text = dlg.FileName;
        var dir = Path.GetDirectoryName(dlg.FileName) ?? string.Empty;
        _outputDir = Path.Combine(dir, "출석부_생성결과");
        TxtOutputDir.Text = _outputDir;
    }

    private void BtnSelectOutput_Click(object sender, RoutedEventArgs e)
    {
        var dlg = new OpenFolderDialog { Title = "저장 폴더 선택" };
        if (dlg.ShowDialog() != true) return;
        _outputDir = dlg.FolderName;
        TxtOutputDir.Text = _outputDir;
    }

    private async void BtnGenerate_Click(object sender, RoutedEventArgs e)
    {
        if (!ValidateInputs()) return;

        BtnGenerate.IsEnabled    = false;
        BtnOpenFolder.IsEnabled  = false;
        LbResults.Items.Clear();
        PbProgress.Visibility    = Visibility.Visible;
        PbProgress.Value         = 0;
        TxtStatus.Text           = "준비 중...";

        var rosterPath = TxtRosterPath.Text;
        var startDate  = DpStart.SelectedDate!.Value.ToString("yyyy-MM-dd");
        var endDate    = DpEnd.SelectedDate!.Value.ToString("yyyy-MM-dd");
        var outputDir  = _outputDir;
        Directory.CreateDirectory(outputDir);

        var enginePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "engine.py");
        if (!File.Exists(enginePath))
        {
            MessageBox.Show($"engine.py를 찾을 수 없소.\n경로: {enginePath}", "오류",
                MessageBoxButton.OK, MessageBoxImage.Error);
            BtnGenerate.IsEnabled = true;
            return;
        }

        var pythonExe = FindPython();
        if (pythonExe is null)
        {
            MessageBox.Show("Python 3.x가 설치되어 있지 않소.\nhttps://python.org 에서 설치 후 PATH에 등록해주시오.",
                "오류", MessageBoxButton.OK, MessageBoxImage.Error);
            BtnGenerate.IsEnabled = true;
            return;
        }

        var args = $"\"{enginePath}\" \"{rosterPath}\" {startDate} {endDate} \"{outputDir}\"";
        int totalCombos = 0, totalFiles = 0;

        await Task.Run(() =>
        {
            var psi = new ProcessStartInfo
            {
                FileName = pythonExe, Arguments = args,
                RedirectStandardOutput = true, RedirectStandardError = true,
                UseShellExecute = false, CreateNoWindow = true,
                StandardOutputEncoding = System.Text.Encoding.UTF8,
            };

            using var proc = Process.Start(psi)!;
            while (!proc.StandardOutput.EndOfStream)
            {
                var line = proc.StandardOutput.ReadLine();
                if (string.IsNullOrWhiteSpace(line)) continue;

                try
                {
                    var result = JsonSerializer.Deserialize<EngineResult>(line,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    if (result is null) continue;

                    totalCombos++;
                    int fileCount = result.files?.Length ?? 0;
                    totalFiles += fileCount;

                    Dispatcher.Invoke(() =>
                    {
                        var icon = result.status == "ok" ? "✅" :
                                   result.status == "skip" ? "⏭️" : "❌";
                        var label = fileCount > 1
                            ? $"{icon} {result.combo}  ({fileCount}개 파일)"
                            : $"{icon} {result.combo}";

                        var item = new ListBoxItem
                        {
                            Content    = label,
                            Foreground = result.status == "ok"
                                ? new SolidColorBrush(Color.FromRgb(0, 140, 0))
                                : result.status == "skip"
                                    ? new SolidColorBrush(Colors.Gray)
                                    : new SolidColorBrush(Colors.Red)
                        };
                        LbResults.Items.Add(item);
                        LbResults.ScrollIntoView(item);
                        TxtStatus.Text   = $"처리 중... {totalCombos}개 조합 완료";
                        PbProgress.Value = Math.Min(totalCombos * 7, 95);
                    });
                }
                catch { }
            }
            proc.WaitForExit();
        });

        PbProgress.Value        = 100;
        TxtStatus.Text          = $"완료! 조합 {totalCombos}개 → 파일 {totalFiles}개 생성";
        BtnGenerate.IsEnabled   = true;
        BtnOpenFolder.IsEnabled = true;
    }

    private void BtnOpenFolder_Click(object sender, RoutedEventArgs e)
    {
        if (Directory.Exists(_outputDir))
            Process.Start("explorer.exe", _outputDir);
    }

    private bool ValidateInputs()
    {
        if (string.IsNullOrWhiteSpace(TxtRosterPath.Text))
        { MessageBox.Show("명단 파일을 선택해주시오.", "확인", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        if (!File.Exists(TxtRosterPath.Text))
        { MessageBox.Show("명단 파일을 찾을 수 없소.", "확인", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        if (DpStart.SelectedDate is null || DpEnd.SelectedDate is null)
        { MessageBox.Show("시작일과 종료일을 입력해주시오.", "확인", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        if (DpStart.SelectedDate > DpEnd.SelectedDate)
        { MessageBox.Show("시작일이 종료일보다 늦을 수 없소.", "확인", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        if (string.IsNullOrWhiteSpace(_outputDir))
        { MessageBox.Show("저장 폴더를 선택해주시오.", "확인", MessageBoxButton.OK, MessageBoxImage.Warning); return false; }
        return true;
    }

    private static string? FindPython()
    {
        foreach (var candidate in new[] { "python3", "python" })
        {
            try
            {
                var psi = new ProcessStartInfo
                {
                    FileName = candidate, Arguments = "--version",
                    RedirectStandardOutput = true, RedirectStandardError = true,
                    UseShellExecute = false, CreateNoWindow = true,
                };
                using var p = Process.Start(psi);
                p?.WaitForExit(3000);
                if (p?.ExitCode == 0) return candidate;
            }
            catch { }
        }
        return null;
    }

    private record EngineResult(string combo, string[] files, string status);
}
