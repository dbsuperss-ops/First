using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text.RegularExpressions;
using FileFlow.Models;
namespace FileFlow.Services
{
    public static class SchedulerService
    {
        // 영문/한글/숫자/하이픈만 허용 — 공백·특수문자 제거로 인수 주입 방지
        private static string San(string n) => Regex.Replace(n, @"[^\w가-힣\-]", "_");

        // HH:mm 형식만 허용, 시(0-23)·분(0-59) 범위 검증
        private static string ValidateTime(string t)
        {
            if (Regex.IsMatch(t, @"^\d{2}:\d{2}$"))
            {
                int h = int.Parse(t.Substring(0, 2)), m = int.Parse(t.Substring(3, 2));
                if (h >= 0 && h <= 23 && m >= 0 && m <= 59) return t;
            }
            return "09:00";
        }

        public static bool RegisterTask(Scenario s)
        {
            try
            {
                string safeName = San(s.Name);
                string tn = "FileFlow_" + safeName;
                string ep = Process.GetCurrentProcess().MainModule?.FileName ?? "";
                if (string.IsNullOrEmpty(ep)) return false;
                DeleteTask(s.Name);
                var dm = new Dictionary<string, string> { ["월"]="MON",["화"]="TUE",["수"]="WED",["목"]="THU",["금"]="FRI",["토"]="SAT",["일"]="SUN" };
                string days = string.Join(",", s.ScheduleDays.Where(d => dm.ContainsKey(d)).Select(d => dm[d]));
                if (string.IsNullOrEmpty(days)) days = "*";
                string time = ValidateTime(s.ScheduleTime);
                // ep는 레지스트리에서 온 실행 파일 경로이므로 따옴표 이스케이프만 처리
                string safeEp = ep.Replace("\"", "");
                return Run($"/Create /TN \"{tn}\" /TR \"\\\"{safeEp}\\\" --run-scenario \\\"{safeName}\\\"\" /SC WEEKLY /D {days} /ST {time} /F");
            }
            catch (Exception ex) { ErrorService.Report(ex, "SchedulerService"); return false; }
        }
        public static bool DeleteTask(string n) { try { return Run($"/Delete /TN \"FileFlow_{San(n)}\" /F"); } catch { return false; } }
        private static bool Run(string a)
        {
            var p = new Process { StartInfo = new() { FileName = "schtasks.exe", Arguments = a, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true } };
            p.Start(); p.WaitForExit(); return p.ExitCode == 0;
        }
    }
}
