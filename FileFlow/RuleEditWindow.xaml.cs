using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Models;

namespace FileFlow
{
    public partial class RuleEditWindow : Window
    {
        public ClassifyRule? Rule { get; private set; }
        private readonly ObservableCollection<CondDisp> _conds = new();
        
        public RuleEditWindow(ClassifyRule? existing = null)
        {
            InitializeComponent();
            ConditionList.ItemsSource = _conds;
            CmbType.SelectedIndex = 0; CmbOperator.SelectedIndex = 0; CmbUnit.SelectedIndex = 0;
            if (existing != null)
            {
                TxtRuleName.Text = existing.RuleName;
                TxtTargetPath.Text = existing.TargetPath;
                RbAnd.IsChecked = existing.ConditionOperator == "AND"; RbOr.IsChecked = existing.ConditionOperator == "OR";
                foreach (var c in existing.Conditions) _conds.Add(new CondDisp { Display = $"{c.Type} {c.Operator} {c.Value}{c.Unit}", Condition = c });
            }
        }

        private void BtnAddCondition_Click(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtValue.Text)) { MessageBox.Show("값을 입력하세요!"); return; }
            var ti = CmbType.SelectedItem as ComboBoxItem;
            var oi = CmbOperator.SelectedItem as ComboBoxItem; var ui = CmbUnit.SelectedItem as ComboBoxItem;
            string t = ti?.Tag?.ToString() ?? "Extension", o = oi?.Tag?.ToString() ?? "Equals", u = ui?.Tag?.ToString() ?? "";
            if (t == "DateYear") { t = "Date"; o = "Year"; } else if (t == "DateMonth") { t = "Date"; o = "Month"; }
            _conds.Add(new CondDisp { Display = $"{ti?.Content} {oi?.Content} {TxtValue.Text}{u}",
                Condition = new FileCondition { Type = t, Operator = o, Value = TxtValue.Text.Trim(), Unit = u } });
            TxtValue.Text = "";
        }

        private void BtnRemoveCondition_Click(object s, RoutedEventArgs e)
        { if (s is Button b && b.Tag is CondDisp d) _conds.Remove(d); }

        private void BtnOk_Click(object s, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(TxtRuleName.Text)) { MessageBox.Show("규칙 이름!"); return; }
            if (_conds.Count == 0) { MessageBox.Show("조건 추가!"); return; }
            if (string.IsNullOrWhiteSpace(TxtTargetPath.Text)) { MessageBox.Show("대상 경로!"); return; }
            Rule = new ClassifyRule { RuleName = TxtRuleName.Text.Trim(), ConditionOperator = RbAnd.IsChecked == true ? "AND" : "OR", TargetPath = TxtTargetPath.Text.Trim(), Conditions = _conds.Select(c => c.Condition).ToList() };
            DialogResult = true; Close();
        }

        private void BtnCancel_Click(object s, RoutedEventArgs e) { DialogResult = false; Close(); }
    }

    public class CondDisp { public string Display { get; set; } = ""; public FileCondition Condition { get; set; } = new(); }
}
// ============================================================
// END OF FileFlow v7.0
// ============================================================
