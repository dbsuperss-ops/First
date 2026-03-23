using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;

namespace SettlementUI
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            PathTextBox.Text = @"C:\Users\dbsup\Desktop\ClosingSample";
        }

        private void TitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton == MouseButton.Left) this.DragMove();
        }

        private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
        private void Close_Click(object sender, RoutedEventArgs e) => Close();

        private void SelectPath_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new System.Windows.Forms.FolderBrowserDialog();
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                PathTextBox.Text = dialog.SelectedPath;
            }
        }

        private string GetEnginePath()
        {
            // 1) exe 옆 engine\ 폴더 (설치 후 실행)
            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string installed = Path.Combine(exeDir, "engine", "ksc_engine.exe");
            if (File.Exists(installed)) return installed;

            // 2) 개발 환경: 소스 루트 기준
            string devPath = Path.Combine(exeDir, "..", "..", "..", "..",
                "files", "KSC_Refiner_v1.1_full", "ksc_refiner", "dist", "ksc_engine", "ksc_engine.exe");
            devPath = Path.GetFullPath(devPath);
            if (File.Exists(devPath)) return devPath;

            return string.Empty;
        }

        private async void StartRefinement_Click(object sender, RoutedEventArgs e)
        {
            string targetPath = PathTextBox.Text;
            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                System.Windows.MessageBox.Show("유효한 경로를 선택해주세요.");
                return;
            }

            string enginePath = GetEnginePath();
            if (string.IsNullOrEmpty(enginePath))
            {
                System.Windows.MessageBox.Show("엔진을 찾을 수 없습니다.\n설치 디렉터리의 engine\\ksc_engine.exe를 확인해주세요.");
                return;
            }

            LogText.Text = "엔진 기동 중...\n";
            StatusLabel.Text = "처리 중";
            StatusLabel.Foreground = System.Windows.Media.Brushes.Orange;
            ProgressBar.IsIndeterminate = true;

            try
            {
                string year = DateTime.Now.Year.ToString();

                ProcessStartInfo start = new ProcessStartInfo
                {
                    FileName = enginePath,
                    Arguments = $"\"{targetPath}\" \"{year}\"",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    StandardOutputEncoding = System.Text.Encoding.UTF8
                };

                Process? process = Process.Start(start);
                if (process != null)
                {
                    string result = await process.StandardOutput.ReadToEndAsync();
                    string error  = await process.StandardError.ReadToEndAsync();
                    LogText.Text += result;
                    if (!string.IsNullOrEmpty(error))
                        LogText.Text += "\n[오류]\n" + error;
                }

                StatusLabel.Text = "완료";
                StatusLabel.Foreground = (System.Windows.Media.Brush)
                    new System.Windows.Media.BrushConverter().ConvertFromString("#10B981")!;
                ProgressBar.IsIndeterminate = false;
                ProgressBar.Value = 100;
            }
            catch (Exception ex)
            {
                LogText.Text += $"\n[시스템 오류] {ex.Message}";
                StatusLabel.Text = "오류 발생";
                StatusLabel.Foreground = System.Windows.Media.Brushes.Red;
            }
        }
    }
}