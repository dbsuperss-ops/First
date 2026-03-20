using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using MessageBox = System.Windows.MessageBox;
using Brush = System.Windows.Media.Brush;
using Brushes = System.Windows.Media.Brushes;

namespace SettlementUI
{
    public partial class MainWindow : Window
    {
        private string? _lastOutputDir;
        private bool _isRunning;

        // 엔진 경로: 1) 배포 번들 engine.exe, 2) ksc_refiner/engine.py, 3) 개발 폴백
        private static readonly string EngineExe =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ksc_refiner", "engine.exe");
        private static readonly string EnginePrimary =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ksc_refiner", "engine.py");
        private static readonly string EngineFallback =
            @"c:\Users\dbsup\.antigravity\KSC_Refiner_v1.1_full\ksc_refiner\engine.py";

        public MainWindow()
        {
            InitializeComponent();
            PathTextBox.Text = @"C:\Users\dbsup\Desktop\ClosingSample";
            LoadRates("2026");
        }

        // ──────────────── 환율 ────────────────

        private static string UserConfigDir =>
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "KSC Refiner");

        private string GetRatesPath()
        {
            // 1) User-writable %APPDATA%\KSC Refiner\ (engine copies here on first run)
            string userRates = Path.Combine(UserConfigDir, "rates.json");
            if (File.Exists(userRates)) return userRates;
            // 2) Install dir (read-only fallback before first engine run)
            string bundledConfig = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "ksc_refiner", "config", "rates.json");
            if (File.Exists(bundledConfig)) return bundledConfig;
            // 3) Dev fallback
            return Path.Combine(Path.GetDirectoryName(EngineFallback)!, "config", "rates.json");
        }

        private void LoadRates(string year)
        {
            try
            {
                string path = GetRatesPath();
                if (!File.Exists(path)) return;
                var doc = JsonDocument.Parse(File.ReadAllText(path));
                if (!doc.RootElement.TryGetProperty(year, out var yr)) return;
                RateUSD.Text = yr.TryGetProperty("USD", out var v) ? v.ToString() : "";
                RateEUR.Text = yr.TryGetProperty("EUR", out var e) ? e.ToString() : "";
                RateRMB.Text = yr.TryGetProperty("RMB", out var r) ? r.ToString() : "";
                RateTRY.Text = yr.TryGetProperty("TRY", out var t) ? t.ToString() : "";
                RateRSD.Text = yr.TryGetProperty("RSD", out var s) ? s.ToString() : "";
            }
            catch { /* silent — don't block UI on rates load failure */ }
        }

        private void SaveRates(string year)
        {
            try
            {
                // Always write to %APPDATA%\KSC Refiner\ (user-writable)
                Directory.CreateDirectory(UserConfigDir);
                string path = Path.Combine(UserConfigDir, "rates.json");
                // Seed from install/dev config if user file doesn't exist yet
                if (!File.Exists(path))
                {
                    string src = GetRatesPath();
                    if (File.Exists(src)) File.Copy(src, path);
                }
                Dictionary<string, Dictionary<string, double>> all;
                if (File.Exists(path))
                {
                    all = JsonSerializer.Deserialize<Dictionary<string, Dictionary<string, double>>>(
                              File.ReadAllText(path))
                          ?? new Dictionary<string, Dictionary<string, double>>();
                }
                else
                {
                    all = new Dictionary<string, Dictionary<string, double>>();
                }

                if (!all.ContainsKey(year))
                    all[year] = new Dictionary<string, double>();

                void Set(string key, string text)
                {
                    if (double.TryParse(text, out double val))
                        all[year][key] = val;
                }

                Set("USD", RateUSD.Text.Trim());
                Set("EUR", RateEUR.Text.Trim());
                Set("RMB", RateRMB.Text.Trim());
                Set("TRY", RateTRY.Text.Trim());
                Set("RSD", RateRSD.Text.Trim());
                all[year]["KRW"] = 1.0;
                all[year]["PLN"] = all[year].TryGetValue("PLN", out double p) ? p : 0;

                var opts = new JsonSerializerOptions { WriteIndented = true };
                File.WriteAllText(path, JsonSerializer.Serialize(all, opts));
            }
            catch { /* silent — don't block engine start on write failure */ }
        }

        private void YearCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            string year = (YearCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "2026";
            LoadRates(year);
        }

        private void SaveRatesBtn_Click(object sender, RoutedEventArgs e)
        {
            string year = (YearCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "2026";
            SaveRates(year);
            MessageBox.Show($"{year}년 환율이 저장되었습니다.", "저장 완료", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // ──────────────── 타이틀바 ────────────────

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) =>
            WindowState = WindowState.Minimized;

        private void MaxRestore_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        // ──────────────── 경로 선택 ────────────────

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog
            {
                Description = "결산 파일이 있는 폴더를 선택하세요",
                UseDescriptionForTitle = true
            };
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                PathTextBox.Text = dialog.SelectedPath;
        }

        // ──────────────── 엔진 실행 ────────────────

        private async void StartRefinement_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning) return;

            string targetPath = PathTextBox.Text.Trim();
            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                MessageBox.Show("유효한 경로를 선택해주세요.", "경로 오류",
                                MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            bool useBundled = File.Exists(EngineExe);
            string enginePath = useBundled ? EngineExe
                              : File.Exists(EnginePrimary) ? EnginePrimary
                              : EngineFallback;

            if (!File.Exists(enginePath))
            {
                MessageBox.Show($"엔진을 찾을 수 없습니다.\n{enginePath}",
                                "엔진 없음", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }

            string year = (YearCombo.SelectedItem as System.Windows.Controls.ComboBoxItem)?.Content?.ToString() ?? "2026";

            SaveRates(year);

            _isRunning = true;
            _lastOutputDir = null;
            OpenOutputBtn.IsEnabled = false;
            RowCountText.Text = "";
            LogText.Text = "";
            AppendLogLine($"▶  엔진 기동  |  경로: {targetPath}  |  기준연도: {year}");
            AppendLogLine(useBundled ? $"   engine: {enginePath}  [bundled]" : $"   script: {enginePath}");
            AppendLogLine(new string('─', 60));

            StatusLabel.Text = "처리 중";
            StatusLabel.Foreground = Brushes.Orange;
            ProgressBar.IsIndeterminate = true;

            try
            {
                ProcessStartInfo psi;
                if (useBundled)
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = EngineExe,
                        Arguments = $"\"{targetPath}\" \"{year}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                    };
                }
                else
                {
                    psi = new ProcessStartInfo
                    {
                        FileName = "python.exe",
                        Arguments = $"\"{enginePath}\" \"{targetPath}\" \"{year}\"",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        StandardOutputEncoding = System.Text.Encoding.UTF8,
                        StandardErrorEncoding = System.Text.Encoding.UTF8,
                    };
                }

                using var process = new Process { StartInfo = psi, EnableRaisingEvents = true };

                process.OutputDataReceived += (_, ev) =>
                {
                    if (ev.Data is null) return;
                    Dispatcher.Invoke(() =>
                    {
                        AppendLogLine(ev.Data);
                        TryDetectOutputDir(ev.Data);
                        TryDetectRowCount(ev.Data);
                    });
                };

                process.ErrorDataReceived += (_, ev) =>
                {
                    if (string.IsNullOrWhiteSpace(ev.Data)) return;
                    Dispatcher.Invoke(() => AppendLogLine("⚠  " + ev.Data));
                };

                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                await System.Threading.Tasks.Task.Run(() => process.WaitForExit());

                bool success = process.ExitCode == 0;

                Dispatcher.Invoke(() =>
                {
                    AppendLogLine(new string('─', 60));
                    if (success)
                    {
                        AppendLogLine("✅  완료");
                        StatusLabel.Text = "완료";
                        StatusLabel.Foreground = (Brush)new BrushConverter().ConvertFromString("#10B981")!;
                        ProgressBar.Value = 100;
                        if (_lastOutputDir != null) OpenOutputBtn.IsEnabled = true;
                    }
                    else
                    {
                        AppendLogLine($"❌  비정상 종료  (exit code {process.ExitCode})");
                        StatusLabel.Text = "오류 발생";
                        StatusLabel.Foreground = Brushes.Red;
                    }
                    ProgressBar.IsIndeterminate = false;
                });
            }
            catch (Exception ex)
            {
                AppendLogLine($"❌  [시스템 오류] {ex.Message}");
                StatusLabel.Text = "오류 발생";
                StatusLabel.Foreground = Brushes.Red;
                ProgressBar.IsIndeterminate = false;
            }
            finally
            {
                _isRunning = false;
            }
        }

        // ──────────────── 로그 헬퍼 ────────────────

        private void AppendLogLine(string line)
        {
            LogText.AppendText(line + "\n");
            LogScrollViewer.ScrollToEnd();
        }

        private void TryDetectOutputDir(string line)
        {
            // 엔진이 "저장:" 또는 "Saved to" 등으로 경로를 출력하면 캡처
            var m = Regex.Match(line, @"(?:저장|saved to)[:\s]+(.+\.xlsx)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                string p = m.Groups[1].Value.Trim();
                _lastOutputDir = Path.GetDirectoryName(p);
            }
            // 폴더 자체를 출력하는 경우
            var m2 = Regex.Match(line, @"출력 경로[:\s]+(.+)");
            if (m2.Success)
                _lastOutputDir = m2.Groups[1].Value.Trim();
        }

        private void TryDetectRowCount(string line)
        {
            var m = Regex.Match(line, @"(\d[\d,]+)\s*(?:행|rows?|건)", RegexOptions.IgnoreCase);
            if (m.Success)
                RowCountText.Text = $"총 {m.Groups[1].Value} 행";
        }

        // ──────────────── 버튼 핸들러 ────────────────

        private void ClearLog_Click(object sender, RoutedEventArgs e)
        {
            LogText.Text = "";
            RowCountText.Text = "";
        }

        private void OpenOutput_Click(object sender, RoutedEventArgs e)
        {
            if (_lastOutputDir != null && Directory.Exists(_lastOutputDir))
                Process.Start("explorer.exe", _lastOutputDir);
            else if (!string.IsNullOrEmpty(PathTextBox.Text) && Directory.Exists(PathTextBox.Text))
                Process.Start("explorer.exe", PathTextBox.Text);
        }
    }
}
