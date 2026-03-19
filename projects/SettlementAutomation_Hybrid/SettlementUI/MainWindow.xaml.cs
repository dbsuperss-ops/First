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

        private async void StartRefinement_Click(object sender, RoutedEventArgs e)
        {
            string targetPath = PathTextBox.Text;
            if (string.IsNullOrEmpty(targetPath) || !Directory.Exists(targetPath))
            {
                System.Windows.MessageBox.Show("유효한 경로를 선택해주세요.");
                return;
            }

            LogText.Text = "엔진 기동 중...\n";
            StatusLabel.Text = "처리 중";
            StatusLabel.Foreground = System.Windows.Media.Brushes.Orange;
            ProgressBar.IsIndeterminate = true;

            try
            {
                // Note: Ensure engine.py is in the same folder as the exe during production
                // For now we use the absolute path for testing
                string scriptPath = @"c:\Users\dbsup\.antigravity\projects\SettlementAutomation_Hybrid\engine.py";
                
                ProcessStartInfo start = new ProcessStartInfo();
                start.FileName = "python.exe";
                start.Arguments = $"\"{scriptPath}\" \"{targetPath}\"";
                start.UseShellExecute = false;
                start.RedirectStandardOutput = true;
                start.RedirectStandardError = true;
                start.CreateNoWindow = true;
                start.StandardOutputEncoding = System.Text.Encoding.UTF8;

                Process? process = Process.Start(start);
                if (process != null)
                {
                    using (StreamReader reader = process.StandardOutput)
                    {
                        string result = await reader.ReadToEndAsync();
                        LogText.Text += result;
                    }
                    using (StreamReader reader = process.StandardError)
                    {
                        string error = await reader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(error)) LogText.Text += "\nError: " + error;
                    }
                }

                StatusLabel.Text = "완료";
                StatusLabel.Foreground = (System.Windows.Media.Brush)new System.Windows.Media.BrushConverter().ConvertFromString("#10B981")!;
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