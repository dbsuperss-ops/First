using System;
using System.Diagnostics;
using System.IO;
using System.Text;

// ksc_engine.exe: embedded Python으로 KSC Refiner engine.py 실행
string exeDir = AppContext.BaseDirectory;
string pythonExe = Path.Combine(exeDir, "python", "python.exe");
string engineScript = Path.Combine(exeDir, "app", "engine.py");

if (!File.Exists(pythonExe))
{
    Console.Error.WriteLine($"[ERROR] Python을 찾을 수 없습니다: {pythonExe}");
    Environment.Exit(1);
}
if (!File.Exists(engineScript))
{
    Console.Error.WriteLine($"[ERROR] 엔진을 찾을 수 없습니다: {engineScript}");
    Environment.Exit(1);
}

// 환경변수 설정
var envVars = new System.Collections.Generic.Dictionary<string, string>
{
    ["KSC_CONFIG_DIR"]   = Path.Combine(exeDir, "config"),
    ["KSC_OUTPUT_DIR"]   = Path.Combine(exeDir, "output"),
    ["PYTHONPATH"]       = Path.Combine(exeDir, "python", "Lib", "site-packages")
                         + ";" + Path.Combine(exeDir, "app"),
    ["PYTHONIOENCODING"] = "utf-8",
};

string[] cmdArgs    = Environment.GetCommandLineArgs()[1..];
string   quotedArgs = string.Join(" ", Array.ConvertAll(cmdArgs, a => $"\"{a}\""));

var psi = new ProcessStartInfo
{
    FileName               = pythonExe,
    Arguments              = $"\"{engineScript}\" {quotedArgs}",
    UseShellExecute        = false,
    CreateNoWindow         = false,
    RedirectStandardOutput = false,
    RedirectStandardError  = false,
    RedirectStandardInput  = false,
};

foreach (var kv in envVars)
    psi.EnvironmentVariables[kv.Key] = kv.Value;

using var proc = Process.Start(psi);
proc?.WaitForExit();
Environment.Exit(proc?.ExitCode ?? 1);
