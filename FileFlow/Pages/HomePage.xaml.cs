using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Services;

namespace FileFlow.Pages
{
    public partial class HomePage : Page, IRefreshable
    {
        public HomePage() { InitializeComponent(); Loaded += (_, _) => LoadStatistics(); }
        public void Refresh() => LoadStatistics();

        private void LoadStatistics()
        {
            var logs = LogService.Load();
            var scenarios = ScenarioService.Load();
            var records = RecordService.LoadRecords();
            TxtTotalFiles.Text = logs.Count(l => l.Action == "이동").ToString();
            TxtExecutions.Text = records.Count.ToString();
            TxtScenarios.Text = scenarios.Count.ToString();
            TxtRecentCount.Text = logs.Count.ToString();
            if (logs.Count > 0)
            {
                PnlNoActivity.Visibility = Visibility.Collapsed;
                RecentActivityList.Visibility = Visibility.Visible;
                RecentActivityList.ItemsSource = logs.OrderByDescending(l => l.Timestamp).Take(5)
                    .Select(l => $"{l.Timestamp:MM/dd HH:mm} - {l.FileName} → {l.Action} 완료").ToList();
            }
            else { PnlNoActivity.Visibility = Visibility.Visible; RecentActivityList.Visibility = Visibility.Collapsed; }
        }

        private void BtnClassify_Click(object sender, RoutedEventArgs e) => Nav("classify");
        private void BtnDuplicate_Click(object sender, RoutedEventArgs e) => Nav("duplicate");
        private void BtnScenario_Click(object sender, RoutedEventArgs e) => Nav("scenario");
        private void Nav(string page) { if (Window.GetWindow(this) is MainWindow mw) mw.NavigateToPage(page); }
    }
}
