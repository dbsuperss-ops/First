using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using FileFlow.Models;
using FileFlow.Services;
namespace FileFlow.Pages
{
    public partial class LogPage : Page, IRefreshable
    {
        private readonly ObservableCollection<LogEntry> _logs = new();
        public LogPage() { InitializeComponent(); LogListView.ItemsSource = _logs; Loaded += (_, _) => LoadLogs(); }
        public void Refresh() => LoadLogs();

        private void LoadLogs()
        {
            _logs.Clear();
            var all = LogService.Load().OrderByDescending(l => l.Timestamp).ToList();
            foreach (var l in all) _logs.Add(l);
            TxtLogCount.Text = $"총 {all.Count}건";
            if (all.Count > 0) { PnlEmptyState.Visibility = Visibility.Collapsed; LogListView.Visibility = Visibility.Visible; }
            else { PnlEmptyState.Visibility = Visibility.Visible; LogListView.Visibility = Visibility.Collapsed; }
        }

        private void BtnRefresh_Click(object s, RoutedEventArgs e) => LoadLogs();
        private void BtnClear_Click(object s, RoutedEventArgs e) { if (MessageBox.Show("모든 로그 삭제?", "확인", MessageBoxButton.YesNo) == MessageBoxResult.Yes) { LogService.Clear(); LoadLogs(); } }

        private void BtnUndo_Click(object s, RoutedEventArgs e)
        {
            var id = LogService.GetLastBatchId();
            if (id == System.Guid.Empty) { MessageBox.Show("되돌릴 작업 없음"); return; }
            if (MessageBox.Show("되돌리겠습니까?", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            var (ok, fail) = LogService.UndoLastBatch(id);
            MessageBox.Show($"성공:{ok} / 실패:{fail}"); LoadLogs();
        }
    }
}
