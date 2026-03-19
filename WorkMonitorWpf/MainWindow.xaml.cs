using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;

namespace WorkMonitorWpf;

public partial class MainWindow : Window
{
    private readonly ObservableCollection<ActivityLog> _logs = new();
    private CancellationTokenSource? _cts;
    private ActiveWindowTracker? _tracker;
    private int _activeCount = 0;
    private int _idleCount = 0;

    private static readonly SolidColorBrush GreenBrush  = new(Color.FromRgb(0x10, 0x7C, 0x10));
    private static readonly SolidColorBrush AmberBrush  = new(Color.FromRgb(0x9D, 0x5D, 0x00));
    private static readonly SolidColorBrush RedBrush    = new(Color.FromRgb(0xD1, 0x34, 0x38));
    private static readonly SolidColorBrush GreenBgBrush = new(Color.FromRgb(0xDF, 0xF6, 0xDD));
    private static readonly SolidColorBrush AmberBgBrush = new(Color.FromRgb(0xFF, 0xF4, 0xCE));

    public MainWindow()
    {
        InitializeComponent();
        LogGrid.ItemsSource = _logs;
    }

    private void ToggleButton_Click(object sender, RoutedEventArgs e)
    {
        if (_cts == null) StartTracking();
        else StopTracking();
    }

    private void StartTracking()
    {
        _cts = new CancellationTokenSource();
        _tracker = new ActiveWindowTracker(idleThresholdSeconds: 60, pollIntervalMs: 1000);
        _tracker.ActivityChanged += OnActivityChanged;

        ToggleButton.Style = (Style)FindResource("StopButton");
        ToggleButton.Content = "모니터링 중지";
        StatusText.Text = "모니터링 중...";
        StatusDot.Fill = GreenBrush;

        _ = _tracker.StartAsync(_cts.Token);
    }

    private void StopTracking()
    {
        _cts?.Cancel();
        _cts = null;
        _tracker = null;

        ToggleButton.Style = (Style)FindResource("PrimaryButton");
        ToggleButton.Content = "모니터링 시작";
        StatusText.Text = "중지됨";
        StatusDot.Fill = RedBrush;
    }

    private void OnActivityChanged(ActivityLog log)
    {
        Dispatcher.Invoke(() =>
        {
            // 최신 항목 맨 위에 추가, 최대 500건 유지
            _logs.Insert(0, log);
            if (_logs.Count > 500)
                _logs.RemoveAt(_logs.Count - 1);

            // 현재 상태 카드 업데이트
            CurrentApp.Text = log.ProcessName;
            CurrentTitle.Text = log.WindowTitle;

            if (log.IsIdle)
            {
                IdleText.Text = "유휴";
                IdleText.Foreground = AmberBrush;
                IdleBadge.Background = AmberBgBrush;
            }
            else
            {
                IdleText.Text = "활성";
                IdleText.Foreground = GreenBrush;
                IdleBadge.Background = GreenBgBrush;
            }

            // 통계 업데이트
            if (log.IsIdle) _idleCount++;
            else _activeCount++;

            TotalCount.Text = _logs.Count.ToString();
            ActiveCount.Text = _activeCount.ToString();
            IdleCount.Text = _idleCount.ToString();
        });
    }

    private void ExportCsv_Click(object sender, RoutedEventArgs e)
    {
        if (_logs.Count == 0)
        {
            MessageBox.Show("내보낼 기록이 없습니다.", "알림", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        var dialog = new SaveFileDialog
        {
            Title = "CSV로 내보내기",
            Filter = "CSV 파일 (*.csv)|*.csv",
            FileName = $"WorkMonitor_{DateTime.Now:yyyyMMdd_HHmmss}.csv",
            DefaultExt = ".csv"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sb = new StringBuilder();
            sb.AppendLine("시간,앱,창 제목,상태");

            foreach (var log in _logs.Reverse())
            {
                var title = log.WindowTitle.Replace("\"", "\"\"");
                var process = log.ProcessName.Replace("\"", "\"\"");
                sb.AppendLine($"{log.Timestamp:yyyy-MM-dd HH:mm:ss},\"{process}\",\"{title}\",{(log.IsIdle ? "유휴" : "활성")}");
            }

            File.WriteAllText(dialog.FileName, sb.ToString(), Encoding.UTF8);
            MessageBox.Show($"저장 완료\n{dialog.FileName}", "내보내기 성공", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show($"저장 실패: {ex.Message}", "오류", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void StatsButton_Click(object sender, RoutedEventArgs e)
    {
        bool isTracking = _cts != null;
        var snapshot = _logs.ToList();
        var statsWindow = new StatsWindow(snapshot, isTracking) { Owner = this };
        statsWindow.ShowDialog();
    }

    private void ClearLogs_Click(object sender, RoutedEventArgs e)
    {
        _logs.Clear();
        _activeCount = 0;
        _idleCount = 0;
        TotalCount.Text = "0";
        ActiveCount.Text = "0";
        IdleCount.Text = "0";
    }

    protected override void OnClosed(EventArgs e)
    {
        _cts?.Cancel();
        base.OnClosed(e);
    }
}
