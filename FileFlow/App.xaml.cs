using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using FileFlow.Services;

namespace FileFlow
{
    public partial class App : Application
    {
        private void Application_Startup(object sender, StartupEventArgs e)
        {
            this.DispatcherUnhandledException += (s, exArgs) =>
            {
                try { ErrorService.Report(exArgs.Exception, "App.DispatcherUnhandledException"); } catch { }
                try { File.WriteAllText("crash.log", exArgs.Exception.ToString()); } catch { }
                MessageBox.Show(exArgs.Exception.Message, "예외", MessageBoxButton.OK, MessageBoxImage.Error);
                exArgs.Handled = true;
            };
            AppDomain.CurrentDomain.UnhandledException += (s, exArgs) =>
            {
                try { if (exArgs.ExceptionObject is Exception ex) ErrorService.Report(ex, "AppDomain.UnhandledException"); } catch { }
                try { File.WriteAllText("crash.log", exArgs.ExceptionObject.ToString()); } catch { }
            };

            try
            {
                if (e.Args != null && e.Args.Length >= 2)
                {
                    var idx = Array.IndexOf(e.Args, "--run-scenario");
                    if (idx >= 0 && idx + 1 < e.Args.Length)
                    {
                        string scenarioName = e.Args[idx + 1];
                        RunScenarioAndExit(scenarioName);
                        Shutdown();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                ErrorService.Report(ex, "App.Startup.ArgParse");
            }

            var win = new MainWindow();
            win.Show();
        }

        private static string San(string n) => Regex.Replace(n, @"[^\w가-힣\-]", "_");

        private void RunScenarioAndExit(string scenarioName)
        {
            try
            {
                var scenarios = ScenarioService.Load();
                var s = scenarios.FirstOrDefault(x =>
                    x.Name.Equals(scenarioName, StringComparison.OrdinalIgnoreCase) ||
                    San(x.Name).Equals(scenarioName, StringComparison.OrdinalIgnoreCase));
                if (s == null || !s.IsActive) return;
                var preview = ClassifyService.Preview(s);
                if (preview.Count == 0) return;
                ClassifyService.Execute(preview, s);
            }
            catch (Exception ex)
            {
                ErrorService.Report(ex, "App.RunScenarioAndExit");
            }
        }
    }
}
