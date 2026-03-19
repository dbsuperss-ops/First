using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FileFlow.Models;

namespace FileFlow.Services
{
    public static class WatcherService
    {
        private static readonly Dictionary<Guid, FileSystemWatcher> _w = new();
        public static bool Start(Scenario s)
        {
            if (!s.IsActive) return false;
            if (_w.ContainsKey(s.Id)) return true;
            if (string.IsNullOrEmpty(s.SourceFolder) || !Directory.Exists(s.SourceFolder)) return false;
            try
            {
                var w = new FileSystemWatcher(s.SourceFolder) { NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite, IncludeSubdirectories = s.IncludeSubfolders, EnableRaisingEvents = true };
                w.Created += async (_, e) =>
                {
                    await Task.Delay(1500);
                    await Task.Run(() =>
                    {
                        try
                        {
                            if (!File.Exists(e.FullPath)) return;
                            var fi = new FileInfo(e.FullPath);
                            foreach (var r in s.Rules)
                            {
                                if (ClassifyService.MatchesRule(fi, r))
                                {
                                    ClassifyService.Execute(new() { new ClassifyResult { FileName = fi.Name, SourcePath = e.FullPath, TargetPath = ClassifyService.BuildTargetPath(fi, r, s.TargetFolder), RuleName = r.RuleName, FileSize = fi.Length } }, s);
                                    break;
                                }
                            }
                        }
                        catch (Exception ex) { ErrorService.Report(ex, $"Watcher: {e.FullPath}"); }
                    });
                };
                _w[s.Id] = w; return true;
            }
            catch (Exception ex) { ErrorService.Report(ex, $"WatcherService.Start: {s.Name}"); return false; }
        }
        public static void Stop(Guid id) { if (_w.TryGetValue(id, out var w)) { w.EnableRaisingEvents = false; w.Dispose(); _w.Remove(id); } }
        public static void StopAll() { foreach (var id in _w.Keys.ToList()) Stop(id); }
        public static bool IsWatching(Guid id) => _w.ContainsKey(id);
    }
}
