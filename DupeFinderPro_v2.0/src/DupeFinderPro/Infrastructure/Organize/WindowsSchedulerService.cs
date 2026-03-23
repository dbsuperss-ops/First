using System.Diagnostics;
using System.Text.RegularExpressions;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Infrastructure.Organize;

public sealed class WindowsSchedulerService : ISchedulerService
{
    private const string TaskPrefix = "DupeFinderPro_";

    // 영문/한글/숫자/하이픈만 허용 — 인수 주입 방지
    private static string Sanitize(string name) =>
        Regex.Replace(name, @"[^\w가-힣\-]", "_");

    // HH:mm 형식, 범위 검증
    private static string ValidateTime(string t)
    {
        if (Regex.IsMatch(t, @"^\d{2}:\d{2}$"))
        {
            int h = int.Parse(t[..2]);
            int m = int.Parse(t[3..]);
            if (h is >= 0 and <= 23 && m is >= 0 and <= 59) return t;
        }
        return "09:00";
    }

    public bool RegisterTask(Scenario scenario)
    {
        try
        {
            var safeName = Sanitize(scenario.Name);
            var taskName = TaskPrefix + safeName;
            var ep = Process.GetCurrentProcess().MainModule?.FileName ?? "";
            if (string.IsNullOrEmpty(ep)) return false;

            DeleteTask(scenario.Name);

            var dayMap = new Dictionary<string, string>
            {
                ["월"] = "MON", ["화"] = "TUE", ["수"] = "WED", ["목"] = "THU",
                ["금"] = "FRI", ["토"] = "SAT", ["일"] = "SUN"
            };
            var days = string.Join(",", scenario.ScheduleDays
                .Where(d => dayMap.ContainsKey(d))
                .Select(d => dayMap[d]));
            if (string.IsNullOrEmpty(days)) days = "*";

            var time = ValidateTime(scenario.ScheduleTime);
            var safeEp = ep.Replace("\"", "");
            return Run($"/Create /TN \"{taskName}\" /TR \"\\\"{safeEp}\\\" --run-scenario \\\"{safeName}\\\"\" /SC WEEKLY /D {days} /ST {time} /F");
        }
        catch { return false; }
    }

    public bool DeleteTask(string scenarioName)
    {
        try { return Run($"/Delete /TN \"{TaskPrefix}{Sanitize(scenarioName)}\" /F"); }
        catch { return false; }
    }

    private static bool Run(string args)
    {
        var p = new Process
        {
            StartInfo = new()
            {
                FileName = "schtasks.exe",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            }
        };
        p.Start();
        p.WaitForExit();
        return p.ExitCode == 0;
    }
}
