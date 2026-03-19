using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Models;
using FileFlow.Services;
using Forms = System.Windows.Forms;

namespace FileFlow.Pages
{
    public partial class ClassifyPage : Page, IRefreshable
    {
        private readonly ObservableCollection<FileItem> _files = new();
        private List<ClassifyResult> _preview = new();
        private List<Scenario> _scenarios = new();

        public ClassifyPage() { InitializeComponent(); FileListView.ItemsSource = _files; Loaded += (_, _) => LoadInfo(); }

        public void Refresh() => LoadInfo();

        private void LoadInfo()
        {
            _scenarios = ScenarioService.Load();
            CmbScenario.Items.Clear();
            foreach (var s in _scenarios) CmbScenario.Items.Add(s.Name);
            if (_scenarios.Count > 0)
            {
                CmbScenario.SelectedIndex = 0;
                TxtScenarioInfo.Text = $"{_scenarios[0].Name} — {_scenarios[0].Rules.Count}개 규칙";
            }
            else TxtScenarioInfo.Text = "시나리오 없음";
        }

        private void CmbScenario_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            int idx = CmbScenario.SelectedIndex;
            if (idx < 0 || idx >= _scenarios.Count) return;
            var s = _scenarios[idx];
            TxtScenarioInfo.Text = $"{s.Name} — {s.Rules.Count}개 규칙";
            if (!string.IsNullOrEmpty(s.SourceFolder)) TxtSourceFolder.Text = s.SourceFolder;
            if (!string.IsNullOrEmpty(s.TargetFolder)) TxtTargetFolder.Text = s.TargetFolder;
        }

        private void BtnReloadScenarios_Click(object sender, RoutedEventArgs e) => LoadInfo();

        private Scenario? GetSelectedScenario()
        {
            int idx = CmbScenario.SelectedIndex;
            if (idx >= 0 && idx < _scenarios.Count) return _scenarios[idx];
            return _scenarios.Count > 0 ? _scenarios[0] : null;
        }

        private void BtnBrowseSource_Click(object sender, RoutedEventArgs e)
        { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtSourceFolder.Text = d.SelectedPath; }

        private void BtnBrowseTarget_Click(object sender, RoutedEventArgs e)
        { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtTargetFolder.Text = d.SelectedPath; }

        private void BtnPreview_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtSourceFolder.Text)) { MessageBox.Show("원본 폴더를 선택하세요!"); return; }
            if (!Directory.Exists(TxtSourceFolder.Text)) { MessageBox.Show("원본 폴더가 없습니다!"); return; }
            
            _files.Clear(); _preview.Clear();
            TxtEmptyState.Text = "검색 중...";
            PnlEmptyState.Visibility = Visibility.Visible;
            PnlFileList.Visibility = Visibility.Collapsed;
            Scenario s = GetSelectedScenario() ?? new Scenario();
            s = new Scenario
            {
                Id = s.Id, Name = s.Name, Rules = new List<ClassifyRule>(s.Rules),
                ExcludeSystemFiles = s.ExcludeSystemFiles, CleanupEmptyFolders = s.CleanupEmptyFolders,
                ConflictMode = s.ConflictMode, SourceFolder = TxtSourceFolder.Text,
                TargetFolder = TxtTargetFolder.Text,
                IncludeSubfolders = ChkIncludeSubfolders.IsChecked == true
            };

            bool manualOnly = ChkManualOnly.IsChecked == true;
            if (manualOnly)
            {
                s.Rules.Clear();
                if (string.IsNullOrEmpty(TxtTargetFolder.Text))
                    s.TargetFolder = Path.Combine(s.SourceFolder, "수동분류_결과");
            }
            else
            {
                if (string.IsNullOrEmpty(TxtTargetFolder.Text)) { MessageBox.Show("대상 폴더를 선택하세요!"); return; }
            }

            string ext = TxtExtFilter.Text.Trim();
            string kw = TxtKeywordFilter.Text.Trim();
            string exclExt = TxtExcludeExtFilter.Text.Trim();
            string exclKw = TxtExcludeKeywordFilter.Text.Trim();
            
            if (!string.IsNullOrEmpty(ext) || !string.IsNullOrEmpty(kw) || !string.IsNullOrEmpty(exclExt) || !string.IsNullOrEmpty(exclKw))
            {
                var dynRule = new ClassifyRule { RuleName = "수동 검색 필터", ConditionOperator = "AND", TargetPath = "수동검색" };
                if (!string.IsNullOrEmpty(ext)) dynRule.Conditions.Add(new FileCondition { Type = "Extension", Operator = "Equals", Value = ext });
                if (!string.IsNullOrEmpty(kw)) dynRule.Conditions.Add(new FileCondition { Type = "Keyword", Operator = "Contains", Value = kw });
                if (!string.IsNullOrEmpty(exclExt)) dynRule.Conditions.Add(new FileCondition { Type = "Extension", Operator = "DoesNotContain", Value = exclExt });
                if (!string.IsNullOrEmpty(exclKw)) dynRule.Conditions.Add(new FileCondition { Type = "Keyword", Operator = "DoesNotContain", Value = exclKw });
                s.Rules.Insert(0, dynRule);
            }

            if (s.Rules.Count == 0) { MessageBox.Show("적용할 규칙이나 필터가 없습니다."); return; }

            try { _preview = ClassifyService.Preview(s); }
            catch (Exception ex) { ErrorService.Report(ex, "ClassifyPage.Preview"); MessageBox.Show("미리보기 중 오류가 발생했습니다:\n" + ex.Message); return; }

            foreach (var r in _preview) _files.Add(new FileItem { FileName = r.FileName, SourcePath = r.SourcePath, RuleName = r.RuleName, TargetPath = r.TargetPath });

            if (_files.Count > 0)
            {
                PnlEmptyState.Visibility = Visibility.Collapsed;
                PnlFileList.Visibility = Visibility.Visible;
                TxtStatus.Text = $"{_files.Count}개 파일 분류 대기";
                BtnExecute.IsEnabled = true;
            }
            else
            {
                PnlEmptyState.Visibility = Visibility.Visible;
                PnlFileList.Visibility = Visibility.Collapsed;
                TxtEmptyState.Text = "조건에 맞는 파일이 없습니다";
                BtnExecute.IsEnabled = false;
            }
        }

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (_preview.Count == 0) return;
            if (MessageBox.Show($"{_preview.Count}개 파일을 분류?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            BtnExecute.IsEnabled = false; TxtStatus.Text = "분류 중...";
            try
            {
                var base_ = GetSelectedScenario() ?? new Scenario();
                var s = new Scenario
                {
                    Id = base_.Id, Name = base_.Name, Rules = new List<ClassifyRule>(base_.Rules),
                    ExcludeSystemFiles = base_.ExcludeSystemFiles, CleanupEmptyFolders = base_.CleanupEmptyFolders,
                    ConflictMode = base_.ConflictMode,
                    SourceFolder = TxtSourceFolder.Text,
                    TargetFolder = (ChkManualOnly.IsChecked == true && string.IsNullOrEmpty(TxtTargetFolder.Text))
                        ? Path.Combine(TxtSourceFolder.Text, "수동분류_결과")
                        : TxtTargetFolder.Text
                };
                var progress = new Progress<int>(v => {
                    Application.Current.Dispatcher.InvokeAsync(() => {
                        PbProgress.Value = v;
                        TxtStatus.Text = $"분류 중... ({v}/{_preview.Count})";
                    });
                });
                PbProgress.Maximum = _preview.Count;
                PbProgress.Value = 0;
                PbProgress.Visibility = Visibility.Visible;

                int c = await ClassifyService.ExecuteAsync(_preview, s, progress);
                MessageBox.Show($"{c}개 완료!"); _files.Clear(); _preview.Clear();
                TxtStatus.Text = "완료!"; 
                PbProgress.Visibility = Visibility.Collapsed;
                PnlEmptyState.Visibility = Visibility.Visible; PnlFileList.Visibility = Visibility.Collapsed;
            }
            catch (Exception ex) { ErrorService.Report(ex, "Classify"); MessageBox.Show("오류: " + ex.Message); }
            finally { BtnExecute.IsEnabled = false; }
        }
    }
    public class FileItem { public string FileName { get; set; } = ""; public string SourcePath { get; set; } = ""; public string RuleName { get; set; } = ""; public string TargetPath { get; set; } = ""; }
}
