using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Services;
namespace FileFlow.Pages
{
    public partial class StatisticsPage : Page, IRefreshable
    {
        public StatisticsPage() { InitializeComponent(); Loaded += (_, _) => Load(); }
        public void Refresh() => Load();

        private void Load()
        {
            var rec = RecordService.LoadRecords();
            int tm = rec.Sum(r => r.FileCount); long tb = rec.Sum(r => r.TotalBytes); int tr = rec.Count;
            TxtTotalMoved.Text = tm.ToString("N0");
            TxtTotalSize.Text = Fmt(tb);
            TxtTotalRuns.Text = tr.ToString("N0"); TxtAvgPerRun.Text = (tr > 0 ? (double)tm / tr : 0).ToString("F1");
            var rs = rec.SelectMany(r => r.Files).GroupBy(f => f.RuleName).Select(g => new RSI { RuleName = g.Key, Count = g.Count() }).OrderByDescending(r => r.Count).ToList();
            if (rs.Count > 0) { LstRuleStats.ItemsSource = rs; PnlNoRuleStats.Visibility = Visibility.Collapsed; LstRuleStats.Visibility = Visibility.Visible; }
            else { PnlNoRuleStats.Visibility = Visibility.Visible; LstRuleStats.Visibility = Visibility.Collapsed; }

            var rr = rec.Take(20).Select(r => new RRI { ScenarioName = r.ScenarioName, ExecutedAt = r.ExecutedAt.ToString("yyyy-MM-dd HH:mm"), FileCount = r.FileCount, SizeText = Fmt(r.TotalBytes) }).ToList();
            if (rr.Count > 0) { LstRecentRuns.ItemsSource = rr; PnlNoRecentRuns.Visibility = Visibility.Collapsed; LstRecentRuns.Visibility = Visibility.Visible; }
            else { PnlNoRecentRuns.Visibility = Visibility.Visible; LstRecentRuns.Visibility = Visibility.Collapsed; }
        }

        private void BtnRefresh_Click(object s, RoutedEventArgs e) => Load();
        private string Fmt(long b) => b >= 1073741824 ? $"{b/1073741824.0:F2} GB" : b >= 1048576 ?
            $"{b/1048576.0:F2} MB" : b >= 1024 ? $"{b/1024.0:F2} KB" : b + " B";
    }
    public class RSI { public string RuleName { get; set; } = ""; public int Count { get; set; } }
    public class RRI { public string ScenarioName { get; set; } = ""; public string ExecutedAt { get; set; } = ""; public int FileCount { get; set; } public string SizeText { get; set; } = ""; }
}
