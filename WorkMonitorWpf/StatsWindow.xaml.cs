using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace WorkMonitorWpf;

public partial class StatsWindow : Window
{
    public StatsWindow(IReadOnlyList<ActivityLog> logs, bool isTracking)
    {
        InitializeComponent();
        var stats = ComputeStats(logs, isTracking);
        StatsGrid.ItemsSource = stats;
        TotalSessionText.Text = FormatDuration(stats.Sum(s => s.TotalTime.TotalSeconds));
    }

    private static List<AppStatRow> ComputeStats(IReadOnlyList<ActivityLog> logs, bool isTracking)
    {
        if (logs.Count == 0)
            return [];

        // logs is newest-first; Reverse() to oldest-first for duration pairing
        var ordered = logs.Reverse().ToList();
        var accumulator = new Dictionary<string, (double active, double idle)>(StringComparer.OrdinalIgnoreCase);

        for (int i = 0; i < ordered.Count; i++)
        {
            var entry = ordered[i];
            double durationSeconds;

            if (i < ordered.Count - 1)
            {
                durationSeconds = (ordered[i + 1].Timestamp - entry.Timestamp).TotalSeconds;
            }
            else
            {
                durationSeconds = isTracking ? (DateTime.Now - entry.Timestamp).TotalSeconds : 0;
            }

            durationSeconds = Math.Max(0, durationSeconds);

            var key = entry.ProcessName;
            if (!accumulator.TryGetValue(key, out var current))
                current = (0, 0);

            if (entry.IsIdle)
                accumulator[key] = (current.active, current.idle + durationSeconds);
            else
                accumulator[key] = (current.active + durationSeconds, current.idle);
        }

        double totalSeconds = accumulator.Values.Sum(v => v.active + v.idle);

        return accumulator
            .Select(kvp =>
            {
                double total = kvp.Value.active + kvp.Value.idle;
                double ratio = totalSeconds > 0 ? total / totalSeconds * 100.0 : 0;
                return new AppStatRow(
                    kvp.Key,
                    TimeSpan.FromSeconds(kvp.Value.active),
                    TimeSpan.FromSeconds(kvp.Value.idle),
                    TimeSpan.FromSeconds(total),
                    ratio);
            })
            .OrderByDescending(r => r.TotalTime)
            .ToList();
    }

    private static string FormatDuration(double totalSeconds)
    {
        var ts = TimeSpan.FromSeconds(totalSeconds);
        return ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m {ts.Seconds:D2}s"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
    }
}

public record AppStatRow(
    string ProcessName,
    TimeSpan ActiveTime,
    TimeSpan IdleTime,
    TimeSpan TotalTime,
    double Ratio)
{
    public string ActiveTimeText => FormatTs(ActiveTime);
    public string IdleTimeText   => FormatTs(IdleTime);
    public string TotalTimeText  => FormatTs(TotalTime);
    public string RatioText      => $"{Ratio:F1}%";

    private static string FormatTs(TimeSpan ts) =>
        ts.TotalHours >= 1
            ? $"{(int)ts.TotalHours}h {ts.Minutes:D2}m"
            : $"{ts.Minutes}m {ts.Seconds:D2}s";
}
