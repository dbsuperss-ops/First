using System;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Models;
using FileFlow.Services;
using Forms = System.Windows.Forms;
namespace FileFlow.Pages
{
    public partial class SettingsPage : Page
    {
        private static readonly string SF = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "FileFlow", "settings.json");
        private static readonly JsonSerializerOptions JO = new() { WriteIndented = true, PropertyNameCaseInsensitive = true };

        public SettingsPage() { InitializeComponent();
            Loaded += (_, _) => { LoadS(); TxtErrorLogPath.Text = ErrorService.GetErrorLogPath(); TxtVersion.Text = "FileFlow v1.1.0"; TxtDataStorage.Text = "로컬 파일 (JSON)"; };
        }

        private void LoadS()
        {
            try { if (!File.Exists(SF)) { var sc = ScenarioService.Load();
                if (sc.Count > 0) { TxtDefaultSource.Text = sc[0].SourceFolder; TxtDefaultTarget.Text = sc[0].TargetFolder; } return;
                }
                var s = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(SF), JO);
                if (s != null) { TxtDefaultSource.Text = s.DefaultSourceFolder; TxtDefaultTarget.Text = s.DefaultTargetFolder; }
            } catch (Exception ex) { ErrorService.Report(ex, "Settings.Load"); }
        }

        private void BtnBrowseSource_Click(object s, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtDefaultSource.Text = d.SelectedPath; }
        private void BtnBrowseTarget_Click(object s, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtDefaultTarget.Text = d.SelectedPath; }

        private void BtnClearLogs_Click(object s, RoutedEventArgs e) { if (MessageBox.Show("로그+통계 삭제?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            LogService.Clear(); RecordService.Clear(); MessageBox.Show("삭제 완료!"); }
        private void BtnResetScenarios_Click(object s, RoutedEventArgs e) { if (MessageBox.Show("시나리오 초기화?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            ScenarioService.Save(ScenarioService.GetDefault()); MessageBox.Show("초기화 완료!"); }

        private void BtnResetTestFiles_Click(object s, RoutedEventArgs e)
        {
            var sc = ScenarioService.Load();
            string sf = sc.Count > 0 ? sc[0].SourceFolder : TxtDefaultSource.Text;
            if (string.IsNullOrEmpty(sf)) { MessageBox.Show("폴더 미설정"); return; }
            if (MessageBox.Show("테스트 파일 생성?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            MessageBox.Show($"{TestDataService.GenerateTestFiles(sf)}개 생성!");
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            try { var d = Path.GetDirectoryName(SF);
                if (d != null) Directory.CreateDirectory(d);
                File.WriteAllText(SF, JsonSerializer.Serialize(new AppSettings { DefaultSourceFolder = TxtDefaultSource.Text, DefaultTargetFolder = TxtDefaultTarget.Text }, JO));
                MessageBox.Show("저장 완료!");
            } catch (Exception ex) { ErrorService.Report(ex, "Settings.Save"); MessageBox.Show("실패: " + ex.Message); }
        }
    }
}
