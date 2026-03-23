using System;
using System.Diagnostics;
using System.IO;
using System.Windows.Forms;

namespace KscRefinerSetup
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            string exeDir = AppDomain.CurrentDomain.BaseDirectory;
            string installerPath = Path.Combine(exeDir, "KscRefiner_Setup_v1.3.0.exe");

            if (!File.Exists(installerPath))
            {
                MessageBox.Show(
                    $"설치 파일을 찾을 수 없습니다:\n{installerPath}",
                    "KSC Refiner Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
                return;
            }

            try
            {
                var processInfo = new ProcessStartInfo
                {
                    FileName = installerPath,
                    UseShellExecute = true,
                    Verb = "runas" // 관리자 권한 요청
                };

                Process.Start(processInfo);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"설치 프로그램 실행 실패:\n{ex.Message}",
                    "KSC Refiner Setup",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error
                );
            }
        }
    }
}
