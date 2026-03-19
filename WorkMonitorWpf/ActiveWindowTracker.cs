using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace WorkMonitorWpf
{
    internal static class Win32Api
    {
        [DllImport("user32.dll")]
        public static extern IntPtr GetForegroundWindow();

        [DllImport("user32.dll")]
        public static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

        [DllImport("user32.dll")]
        public static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        public static extern bool GetLastInputInfo(ref LASTINPUTINFO plii);

        [StructLayout(LayoutKind.Sequential)]
        public struct LASTINPUTINFO
        {
            public uint cbSize;
            public uint dwTime;
        }
    }

    public record ActivityLog(DateTime Timestamp, string ProcessName, string WindowTitle, bool IsIdle);

    public class ActiveWindowTracker
    {
        private string _lastProcess = "";
        private string _lastTitle = "";
        private bool _lastIsIdle = false;

        private readonly int _idleThresholdSeconds;
        private readonly TimeSpan _pollInterval;

        public event Action<ActivityLog>? ActivityChanged;

        public ActiveWindowTracker(int idleThresholdSeconds = 60, int pollIntervalMs = 1000)
        {
            _idleThresholdSeconds = idleThresholdSeconds;
            _pollInterval = TimeSpan.FromMilliseconds(pollIntervalMs);
        }

        public async Task StartAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                try
                {
                    IntPtr handle = Win32Api.GetForegroundWindow();
                    string processName = GetProcessName(handle);
                    string title = GetWindowTitle(handle);
                    bool isIdle = GetIdleSeconds() > _idleThresholdSeconds;

                    if (processName != _lastProcess || title != _lastTitle || isIdle != _lastIsIdle)
                    {
                        var log = new ActivityLog(DateTime.Now, processName, title, isIdle);
                        ActivityChanged?.Invoke(log);

                        _lastProcess = processName;
                        _lastTitle = title;
                        _lastIsIdle = isIdle;
                    }

                    await Task.Delay(_pollInterval, ct);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"[오류] {ex.Message}");
                    await Task.Delay(_pollInterval).ConfigureAwait(false);
                }
            }
        }

        private string GetWindowTitle(IntPtr handle)
        {
            const int nChars = 256;
            var buffer = new StringBuilder(nChars);
            return Win32Api.GetWindowText(handle, buffer, nChars) > 0
                ? buffer.ToString()
                : "Desktop / Task Switcher";
        }

        private string GetProcessName(IntPtr handle)
        {
            if (handle == IntPtr.Zero) return "Idle";
            Win32Api.GetWindowThreadProcessId(handle, out uint pid);
            try
            {
                using var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return "System / Unknown";
            }
        }

        private double GetIdleSeconds()
        {
            var info = new Win32Api.LASTINPUTINFO
            {
                cbSize = (uint)Marshal.SizeOf<Win32Api.LASTINPUTINFO>()
            };
            Win32Api.GetLastInputInfo(ref info);
            uint elapsed = (uint)(Environment.TickCount64 & 0xFFFFFFFF) - info.dwTime;
            return elapsed / 1000.0;
        }
    }
}
