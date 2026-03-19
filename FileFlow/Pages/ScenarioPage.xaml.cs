using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Models;
using FileFlow.Services;
using Forms = System.Windows.Forms;
using Microsoft.Win32;

namespace FileFlow.Pages
{
    public partial class ScenarioPage : Page, IRefreshable
    {
        private List<Scenario> _scenarios = new();
        private Scenario? _cur;

        public ScenarioPage() { InitializeComponent(); Loaded += (_, _) => LoadScenarios(); }
        public void Refresh() => LoadScenarios();

        private void LoadScenarios()
        {
            _scenarios = ScenarioService.Load();
            ScenarioList.Items.Clear();
            foreach (var s in _scenarios)
            {
                var sp = new StackPanel();
                sp.Children.Add(new TextBlock { Text = s.Name, FontWeight = FontWeights.SemiBold, FontSize = 14, Foreground = s.IsActive ? System.Windows.Media.Brushes.Black : System.Windows.Media.Brushes.Gray });
                sp.Children.Add(new TextBlock { Text = $"{(s.IsActive ? "활성화" : "비활성화")} | {s.Rules.Count}개 규칙", FontSize = 12, Opacity = 0.6 });
                ScenarioList.Items.Add(new ListBoxItem { Content = sp, Padding = new Thickness(12, 10, 12, 10) });
            }
            if (ScenarioList.Items.Count > 0) ScenarioList.SelectedIndex = 0;
        }

        private void ScenarioList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (ScenarioList.SelectedIndex < 0 || ScenarioList.SelectedIndex >= _scenarios.Count) return;
            _cur = _scenarios[ScenarioList.SelectedIndex];
            
            ChkIsActive.IsChecked = _cur.IsActive;
            TxtScenarioName.Text = _cur.Name; TxtSourceFolder.Text = _cur.SourceFolder; TxtTargetFolder.Text = _cur.TargetFolder;
            ChkSubfolders.IsChecked = _cur.IncludeSubfolders; ChkExcludeSystem.IsChecked = _cur.ExcludeSystemFiles;
            ChkCleanupEmpty.IsChecked = _cur.CleanupEmptyFolders;
            RbConflictSkip.IsChecked = _cur.ConflictMode == ConflictMode.Skip;
            RbConflictRename.IsChecked = _cur.ConflictMode == ConflictMode.Rename;
            RbConflictOverwrite.IsChecked = _cur.ConflictMode == ConflictMode.Overwrite;
            ChkIsScheduled.IsChecked = _cur.IsScheduled;
            PnlSchedule.IsEnabled = _cur.IsScheduled;
            TxtScheduleTime.Text = _cur.ScheduleTime;
            ChkMon.IsChecked = _cur.ScheduleDays.Contains("월");
            ChkTue.IsChecked = _cur.ScheduleDays.Contains("화");
            ChkWed.IsChecked = _cur.ScheduleDays.Contains("수");
            ChkThu.IsChecked = _cur.ScheduleDays.Contains("목");
            ChkFri.IsChecked = _cur.ScheduleDays.Contains("금");
            ChkSat.IsChecked = _cur.ScheduleDays.Contains("토");
            ChkSun.IsChecked = _cur.ScheduleDays.Contains("일");

            LoadRules();
        }

        private void ChkIsActive_Click(object sender, RoutedEventArgs e)
        {
            if (_cur != null) _cur.IsActive = ChkIsActive.IsChecked == true;
        }

        private void ChkIsScheduled_Click(object sender, RoutedEventArgs e)
        {
            PnlSchedule.IsEnabled = ChkIsScheduled.IsChecked == true;
        }

        private void LoadRules()
        {
            RuleList.ItemsSource = null;
            if (_cur != null) RuleList.ItemsSource = _cur.Rules;
        }

        private void BtnBrowseSource_Click(object s, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtSourceFolder.Text = d.SelectedPath; }
        private void BtnBrowseTarget_Click(object s, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtTargetFolder.Text = d.SelectedPath; }

        private void BtnAddScenario_Click(object s, RoutedEventArgs e)
        { _scenarios.Add(new Scenario { Name = "새 시나리오 " + (_scenarios.Count + 1), IsActive = true });
            ScenarioService.Save(_scenarios); LoadScenarios(); ScenarioList.SelectedIndex = ScenarioList.Items.Count - 1; }

        private void BtnDeleteScenario_Click(object s, RoutedEventArgs e)
        {
            if (_cur == null) return;
            if (MessageBox.Show($"'{_cur.Name}' 삭제?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            WatcherService.Stop(_cur.Id); SchedulerService.DeleteTask(_cur.Name);
            _scenarios.Remove(_cur); ScenarioService.Save(_scenarios); LoadScenarios();
        }

        private void BtnAddRule_Click(object s, RoutedEventArgs e)
        {
            if (_cur == null) return;
            var w = new RuleEditWindow();
            if (w.ShowDialog() == true && w.Rule != null) { _cur.Rules.Add(w.Rule); Save(); LoadRules(); }
        }

        private void BtnEditRule_Click(object s, RoutedEventArgs e)
        {
            if (_cur == null || RuleList.SelectedItem is not ClassifyRule r) return;
            var w = new RuleEditWindow(r);
            if (w.ShowDialog() == true && w.Rule != null)
            {
                int i = _cur.Rules.IndexOf(r);
                if (i >= 0) _cur.Rules[i] = w.Rule;
                Save(); LoadRules();
            }
        }

        private void BtnDeleteRule_Click(object s, RoutedEventArgs e)
        {
            if (_cur == null || RuleList.SelectedItem is not ClassifyRule r) return;
            if (MessageBox.Show("규칙을 삭제합니까?", "확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes)
            { _cur.Rules.Remove(r); Save(); LoadRules(); }
        }

        private void BtnSuggestRules_Click(object s, RoutedEventArgs e)
        {
            if (_cur == null) return;
            string dir = TxtSourceFolder.Text;
            if (string.IsNullOrEmpty(dir) || !Directory.Exists(dir)) { MessageBox.Show("원본 폴더를 먼저 지정하시오."); return; }

            try {
                var files = Directory.GetFiles(dir);
                var extGroups = files.Select(f => Path.GetExtension(f).ToLower())
                                     .Where(x => !string.IsNullOrEmpty(x))
                                     .GroupBy(x => x)
                                     .Where(g => g.Count() >= 3)
                                     .ToList();
                int added = 0;
                foreach(var g in extGroups)
                {
                    string ext = g.Key;
                    if (_cur.Rules.Any(r => r.Conditions.Any(c => c.Value.Contains(ext)))) continue;

                    var rule = new ClassifyRule
                    {
                        RuleName = $"{ext.TrimStart('.').ToUpper()} 자동 분류",
                        TargetPath = $"{ext.TrimStart('.').ToUpper()} 파일",
                        ConditionOperator = "OR",
                        Conditions = new List<FileCondition> { new FileCondition { Type = "Extension", Operator = "Equals", Value = ext } }
                    };
                    _cur.Rules.Add(rule);
                    added++;
                }
                if (added > 0) { Save(); LoadRules(); MessageBox.Show($"{added}개의 규칙이 자동 추가되었소."); }
                else { MessageBox.Show("추천할 만한 새로운 규칙 패턴이 없소."); }
            } catch (Exception ex) { ErrorService.Report(ex, "Suggest"); MessageBox.Show("오류 발생."); }
        }

        private void BtnSave_Click(object s, RoutedEventArgs e)
        { 
            Save();
            if (_cur == null) return; 
            bool ok = ScenarioService.Save(_scenarios); 
            MessageBox.Show(ok ? "저장 완료!" : "저장 실패!"); 
            if (ok) LoadScenarios();
        }

        private void BtnExportScenario_Click(object s, RoutedEventArgs e)
        {
            if (_cur == null) { MessageBox.Show("내보낼 시나리오를 먼저 선택하시오."); return; }
            var dlg = new SaveFileDialog { Filter = "JSON 파일 (*.json)|*.json", FileName = _cur.Name + ".json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = JsonSerializer.Serialize(_cur, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(dlg.FileName, json);
                MessageBox.Show("내보내기 완료!");
            }
            catch (Exception ex) { ErrorService.Report(ex, "ExportScenario"); MessageBox.Show("내보내기 실패!"); }
        }

        private void BtnImportScenario_Click(object s, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "JSON 파일 (*.json)|*.json" };
            if (dlg.ShowDialog() != true) return;
            try
            {
                var json = File.ReadAllText(dlg.FileName).Trim();
                List<Scenario> importedList;

                if (json.StartsWith("["))
                    importedList = JsonSerializer.Deserialize<List<Scenario>>(json) ?? new();
                else
                {
                    var single = JsonSerializer.Deserialize<Scenario>(json);
                    importedList = single != null ? new List<Scenario> { single } : new();
                }

                if (importedList.Count == 0) { MessageBox.Show("올바르지 않은 파일 형식입니다."); return; }

                int added = 0;
                foreach (var item in importedList)
                {
                    item.Id = Guid.NewGuid();
                    if (_scenarios.Any(sc => sc.Name == item.Name))
                        item.Name = item.Name + " (가져오기)";
                    _scenarios.Add(item);
                    added++;
                }

                ScenarioService.Save(_scenarios);
                LoadScenarios();
                ScenarioList.SelectedIndex = ScenarioList.Items.Count - 1;
                MessageBox.Show($"{added}개 시나리오 가져오기 완료!");
            }
            catch (Exception ex) { ErrorService.Report(ex, "ImportScenario"); MessageBox.Show("가져오기 실패!"); }
        }

        private void BtnWatchStart_Click(object s, RoutedEventArgs e)
        {
            Save();
            if (_cur == null) return;
            if (WatcherService.IsWatching(_cur.Id)) { WatcherService.Stop(_cur.Id); MessageBox.Show("감시 중지"); }
            else { bool ok = WatcherService.Start(_cur); MessageBox.Show(ok ? "감시 시작!" : "실패"); }
        }

        private void Save()
        {
            if (_cur == null) return;
            _cur.IsActive = ChkIsActive.IsChecked == true;
            _cur.Name = TxtScenarioName.Text; _cur.SourceFolder = TxtSourceFolder.Text; _cur.TargetFolder = TxtTargetFolder.Text;
            _cur.IncludeSubfolders = ChkSubfolders.IsChecked == true; _cur.ExcludeSystemFiles = ChkExcludeSystem.IsChecked == true;
            _cur.CleanupEmptyFolders = ChkCleanupEmpty.IsChecked == true;
            _cur.ConflictMode = RbConflictSkip.IsChecked == true ? ConflictMode.Skip : RbConflictOverwrite.IsChecked == true ? ConflictMode.Overwrite : ConflictMode.Rename;
            _cur.IsScheduled = ChkIsScheduled.IsChecked == true;
            _cur.ScheduleTime = TxtScheduleTime.Text;
            _cur.ScheduleDays.Clear();
            if (ChkMon.IsChecked == true) _cur.ScheduleDays.Add("월");
            if (ChkTue.IsChecked == true) _cur.ScheduleDays.Add("화");
            if (ChkWed.IsChecked == true) _cur.ScheduleDays.Add("수");
            if (ChkThu.IsChecked == true) _cur.ScheduleDays.Add("목");
            if (ChkFri.IsChecked == true) _cur.ScheduleDays.Add("금");
            if (ChkSat.IsChecked == true) _cur.ScheduleDays.Add("토");
            if (ChkSun.IsChecked == true) _cur.ScheduleDays.Add("일");

            if (_cur.IsActive && _cur.IsScheduled) SchedulerService.RegisterTask(_cur);
            else SchedulerService.DeleteTask(_cur.Name);
        }
    }
}
