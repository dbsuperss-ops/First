using System;
using System.IO;
using System.IO.Compression;
using System.Diagnostics;
using System.Windows.Forms;
using System.Threading;

// KSC Refiner v1.2 설치 프로그램
class Program
{
    [STAThread]
    static void Main()
    {
        try
    {
        // OutputEncoding 설정: 리디렉션되지 않은 경우에만 설정
        try
        {
            if (!Console.IsOutputRedirected)
                Console.OutputEncoding = System.Text.Encoding.UTF8;
        }
        catch { /* 실패해도 계속 진행 */ }

        Console.WriteLine("KSC Refiner v1.2 설치 프로그램");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");

        string exeDir    = AppContext.BaseDirectory;
        string assetsZip = Path.Combine(exeDir, "assets.zip");

        Console.WriteLine($"실행 경로: {exeDir}");
        Console.WriteLine($"Assets: {assetsZip}");

        if (!File.Exists(assetsZip))
        {
            Console.WriteLine($"\n❌ assets.zip를 찾을 수 없습니다!");
            MessageBox.Show($"assets.zip 파일을 찾을 수 없습니다.\n\n경로: {assetsZip}\n\nKscRefiner_Setup.exe와 assets.zip을 같은 폴더에 넣어주세요.",
                "KSC Refiner 설치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
            Console.WriteLine("\n아무 키나 눌러 종료...");
            Console.ReadKey();
            Environment.Exit(1);
        }

        Console.WriteLine("✓ assets.zip 발견");

        // 설치 경로 선택
        string defaultPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "KSC Refiner");

        Console.WriteLine($"\n설치 경로를 선택하세요 (기본: {defaultPath})");

        string installPath = defaultPath;
        using (var fb = new FolderBrowserDialog())
        {
            fb.Description         = "KSC Refiner를 설치할 폴더를 선택하세요.";
            fb.SelectedPath        = defaultPath;
            fb.ShowNewFolderButton = true;

            if (fb.ShowDialog() == DialogResult.OK)
                installPath = fb.SelectedPath;
            else
            {
                Console.WriteLine("\n설치가 취소되었습니다.");
                Thread.Sleep(1000);
                Environment.Exit(0);
            }
        }

        Console.WriteLine($"\n선택된 경로: {installPath}");

        // 실행 중인 프로세스 확인 및 종료 요청
        bool hasRunningProcess = CheckAndTerminateRunningProcesses(installPath);

        if (hasRunningProcess)
        {
            DialogResult retry = MessageBox.Show(
                "KSC Refiner가 현재 실행 중입니다.\n\n프로그램을 종료한 후 '다시 시도'를 눌러주세요.\n\n(프로세스를 강제 종료하시겠습니까?)",
                "프로그램 실행 중", MessageBoxButtons.RetryCancel, MessageBoxIcon.Warning);

            if (retry == DialogResult.Cancel)
            {
                Console.WriteLine("\n설치가 취소되었습니다.");
                Thread.Sleep(1000);
                Environment.Exit(0);
            }

            // 다시 확인
            hasRunningProcess = CheckAndTerminateRunningProcesses(installPath);
            if (hasRunningProcess)
            {
                MessageBox.Show(
                    "프로세스를 종료할 수 없습니다.\n\n작업 관리자에서 'KscRefiner.exe' 및 'ksc_engine.exe'를 수동으로 종료해주세요.",
                    "설치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);
                Environment.Exit(1);
            }
        }

        Console.WriteLine("\n파일 압축 해제 중...");

        // 기존 설치 정리
        if (Directory.Exists(installPath))
        {
            Console.WriteLine("  기존 설치 폴더 정리 중...");
            try
            {
                Directory.Delete(installPath, true);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ⚠ 일부 파일 삭제 실패 (사용 중): {ex.Message}");
            }
        }
        Directory.CreateDirectory(installPath);

        // assets.zip 압축 해제 (파일별로 시도)
        Console.WriteLine($"  압축 해제: assets.zip → {installPath}");
        int extractedCount = 0;
        int failedCount = 0;

        using (var archive = System.IO.Compression.ZipFile.OpenRead(assetsZip))
        {
            foreach (var entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name)) // 디렉토리 항목
                    continue;

                string destPath = Path.Combine(installPath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

                try
                {
                    entry.ExtractToFile(destPath, overwrite: true);
                    extractedCount++;
                }
                catch (IOException)
                {
                    Console.WriteLine($"  ⚠ 건너뜀 (사용 중): {entry.FullName}");
                    failedCount++;
                }
            }
        }

        Console.WriteLine($"  ✓ 압축 해제 완료 ({extractedCount}개 파일)");
        if (failedCount > 0)
        {
            Console.WriteLine($"  ⚠ {failedCount}개 파일은 사용 중이어서 건너뛰었습니다.");
            Console.WriteLine("     프로그램을 완전히 종료한 후 다시 설치하세요.");
        }

        Console.WriteLine("\n바로가기 생성 중...");

        string appExe     = Path.Combine(installPath, "ksc_engine", "KscRefiner.exe");
        string desktopLnk = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "KSC Refiner.lnk");
        string startLnk   = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            "KSC Refiner", "KSC Refiner.lnk");

        CreateShortcut(appExe, desktopLnk, installPath, "KSC 결산 자동화 도구");
        Console.WriteLine($"  ✓ 바탕화면 바로가기");

        Directory.CreateDirectory(Path.GetDirectoryName(startLnk)!);
        CreateShortcut(appExe, startLnk, installPath, "KSC 결산 자동화 도구");
        Console.WriteLine($"  ✓ 시작 메뉴 바로가기");

        // 제어판 프로그램 추가/제거 등록
        RegisterUninstall(installPath, appExe);

        Console.WriteLine("\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("✅ 설치가 완료되었습니다!");
        Console.WriteLine($"   설치 경로: {installPath}");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("\n💡 설치 로그를 복사하려면 콘솔 창에서 텍스트를 드래그하여 복사하세요.");

        DialogResult launch = MessageBox.Show(
            $"설치가 완료되었습니다!\n\n설치 경로: {installPath}\n\nKSC Refiner를 지금 실행하시겠습니까?",
            "설치 완료", MessageBoxButtons.YesNo, MessageBoxIcon.Information);

        if (launch == DialogResult.Yes)
        {
            Console.WriteLine("\nKSC Refiner 실행 중...");
            Process.Start(new ProcessStartInfo(appExe) { UseShellExecute = true });
        }

        Console.WriteLine("\n아무 키나 눌러 종료...");
        Console.ReadKey();
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"\n━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.Error.WriteLine($"❌ 오류 발생: {ex.Message}");
        Console.Error.WriteLine($"━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.Error.WriteLine($"\n상세 정보:\n{ex}");

        MessageBox.Show($"설치 중 오류가 발생했습니다:\n\n{ex.Message}\n\n상세 내용은 콘솔 창을 확인하세요.",
            "설치 오류", MessageBoxButtons.OK, MessageBoxIcon.Error);

        Console.WriteLine("\n아무 키나 눌러 종료...");
        Console.ReadKey();
        Environment.Exit(1);
        }
    }

    static bool CheckAndTerminateRunningProcesses(string installPath)
    {
        Console.WriteLine("\n실행 중인 프로세스 확인 중...");

        var processNames = new[] { "KscRefiner", "ksc_engine", "ksc_launcher" };
        var runningProcesses = new System.Collections.Generic.List<Process>();

        foreach (var name in processNames)
        {
            var procs = Process.GetProcessesByName(name);
            foreach (var proc in procs)
            {
                try
                {
                    // 설치 경로와 관련된 프로세스인지 확인
                    if (proc.MainModule?.FileName?.StartsWith(installPath, StringComparison.OrdinalIgnoreCase) == true)
                    {
                        Console.WriteLine($"  발견: {proc.ProcessName}.exe (PID: {proc.Id})");
                        runningProcesses.Add(proc);
                    }
                }
                catch { /* Access denied - 관리자 권한 필요 */ }
            }
        }

        if (runningProcesses.Count == 0)
        {
            Console.WriteLine("  ✓ 실행 중인 프로세스 없음");
            return false;
        }

        Console.WriteLine($"\n  ⚠ {runningProcesses.Count}개의 프로세스가 실행 중입니다.");
        Console.WriteLine("  프로세스 종료 시도 중...");

        foreach (var proc in runningProcesses)
        {
            try
            {
                proc.CloseMainWindow();
                if (!proc.WaitForExit(3000)) // 3초 대기
                {
                    proc.Kill();
                    proc.WaitForExit(1000);
                }
                Console.WriteLine($"  ✓ 종료됨: {proc.ProcessName}.exe");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"  ✗ 종료 실패: {proc.ProcessName}.exe - {ex.Message}");
            }
        }

        // 다시 확인
        Thread.Sleep(500);
        int stillRunning = 0;
        foreach (var name in processNames)
        {
            var procs = Process.GetProcessesByName(name);
            stillRunning += procs.Length;
            foreach (var p in procs) p.Dispose();
        }

        return stillRunning > 0;
    }

    static void CreateShortcut(string targetPath, string lnkPath, string workDir, string desc)
{
    try
    {
        // COM IShellLink 사용 (PowerShell보다 안정적)
        Type shellType = Type.GetTypeFromProgID("WScript.Shell")!;
        dynamic shell = Activator.CreateInstance(shellType)!;
        dynamic shortcut = shell.CreateShortcut(lnkPath);
        shortcut.TargetPath = targetPath;
        shortcut.WorkingDirectory = workDir;
        shortcut.Description = desc;
        shortcut.Save();

        System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);
        System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ⚠ 바로가기 생성 실패: {ex.Message}");
    }
    }

    static void RegisterUninstall(string installPath, string appExe)
{
    try
    {
        // 개선된 제거 스크립트: 자기 자신을 지연 삭제
        string uninstallBat = Path.Combine(installPath, "Uninstall.bat");
        string desktopLnk = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
            "KSC Refiner.lnk");
        string startMenuFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),
            "KSC Refiner");

        // 제거 배치 파일: 바로가기 제거 → 폴더 삭제 → 레지스트리 정리
        File.WriteAllText(uninstallBat, $@"@echo off
chcp 65001 >nul
echo ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
echo KSC Refiner 제거 프로그램
echo ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
echo.
echo 바로가기 제거 중...
if exist ""{desktopLnk}"" (
    del /f /q ""{desktopLnk}"" 2>nul
    if not exist ""{desktopLnk}"" echo   ✓ 바탕화면 바로가기 제거
)
if exist ""{startMenuFolder}"" (
    rmdir /s /q ""{startMenuFolder}"" 2>nul
    if not exist ""{startMenuFolder}"" echo   ✓ 시작 메뉴 폴더 제거
)
echo.
echo 프로그램 파일 제거 중...
echo   실행 중인 파일은 재부팅 후 제거됩니다.
cd /d ""%TEMP%""
ping 127.0.0.1 -n 2 >nul
rmdir /s /q ""{installPath}"" 2>nul
if not exist ""{installPath}"" (
    echo   ✓ 프로그램 폴더 제거 완료
) else (
    echo   ⚠ 일부 파일이 사용 중입니다. 재부팅 후 수동 삭제 필요
    echo   경로: {installPath}
)
echo.
echo 레지스트리 정리 중...
reg delete ""HKLM\SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KscRefiner"" /f >nul 2>&1
echo   ✓ 레지스트리 항목 제거
echo.
echo ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
echo ✅ KSC Refiner 제거가 완료되었습니다.
echo ━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━
echo.
pause
(goto) 2>nul & del /f /q ""%~f0"" & exit /b
");

        using var key = Microsoft.Win32.Registry.LocalMachine.CreateSubKey(
            @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\KscRefiner");
        if (key != null)
        {
            key.SetValue("DisplayName",      "KSC Refiner");
            key.SetValue("DisplayVersion",   "1.2.0");
            key.SetValue("Publisher",        "Kyungshin Group");
            key.SetValue("InstallLocation",  installPath);
            key.SetValue("UninstallString",  $"cmd /c \"\"{uninstallBat}\"\"");
            key.SetValue("NoModify",         1);
            key.SetValue("NoRepair",         1);
        }
        Console.WriteLine("  ✓ 제어판 등록 완료");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"  ⚠ 제어판 등록 실패 (관리자 권한 필요): {ex.Message}");
    }
    }
}
