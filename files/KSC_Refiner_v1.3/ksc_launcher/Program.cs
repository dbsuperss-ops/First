using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace KscRefinerLauncher
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string enginePath = Path.Combine(exeDir, "ksc_engine.exe");

            if (!File.Exists(enginePath))
            {
                // 디버그 정보 포함
                string debugInfo = $"Engine file not found:\n{enginePath}\n\n";
                debugInfo += $"Current Directory: {Directory.GetCurrentDirectory()}\n";
                debugInfo += $"Base Directory: {exeDir}\n\n";
                debugInfo += "Files in directory:\n";

                try
                {
                    var files = Directory.GetFiles(exeDir, "*.exe");
                    foreach (var f in files)
                    {
                        debugInfo += $"  - {Path.GetFileName(f)}\n";
                    }
                }
                catch (Exception ex)
                {
                    debugInfo += $"Error listing files: {ex.Message}";
                }

                MessageBox.Show(
                    debugInfo,
                    "KSC Refiner v1.3 - Debug Info",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            // 폴더 선택 대화상자
            using (var folderDialog = new FolderBrowserDialog())
            {
                folderDialog.Description = "엑셀 파일이 있는 폴더를 선택하세요";
                folderDialog.ShowNewFolderButton = false;

                if (folderDialog.ShowDialog() != DialogResult.OK)
                {
                    return;
                }

                string inputDir = folderDialog.SelectedPath;

                // 연도 입력 대화상자
                string year = Microsoft.VisualBasic.Interaction.InputBox(
                    "기준 연도를 입력하세요:",
                    "KSC Refiner v1.3",
                    "2026"
                );

                if (string.IsNullOrWhiteSpace(year))
                {
                    return;
                }

                // 엔진 실행
                try
                {
                    var processInfo = new ProcessStartInfo
                    {
                        FileName = enginePath,
                        Arguments = $"\"{inputDir}\" {year}",
                        UseShellExecute = false,
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = false,
                        WorkingDirectory = exeDir
                    };

                    using (var process = Process.Start(processInfo))
                    {
                        if (process == null)
                        {
                            MessageBox.Show(
                                "엔진 실행에 실패했습니다.",
                                "KSC Refiner v1.3",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Error
                            );
                            return;
                        }

                        process.WaitForExit();

                        if (process.ExitCode == 0)
                        {
                            MessageBox.Show(
                                "통합 DB 생성 완료!\n\n출력 폴더를 여시겠습니까?",
                                "KSC Refiner v1.3",
                                MessageBoxButtons.YesNo,
                                MessageBoxIcon.Information
                            );

                            string outputDir = Path.Combine(exeDir, "output");
                            if (Directory.Exists(outputDir))
                            {
                                Process.Start("explorer.exe", outputDir);
                            }
                        }
                        else
                        {
                            MessageBox.Show(
                                "처리 중 오류가 발생했습니다.\n\n콘솔 출력을 확인하세요.",
                                "KSC Refiner v1.3",
                                MessageBoxButtons.OK,
                                MessageBoxIcon.Warning
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        $"오류 발생:\n{ex.Message}",
                        "KSC Refiner v1.3",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error
                    );
                }
            }
        }
    }
}
