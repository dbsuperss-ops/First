using System;
using System.IO;
namespace FileFlow.Services
{
    public static class TestDataService
    {
        public static int GenerateTestFiles(string src)
        {
            if (string.IsNullOrEmpty(src)) return 0;
            try
            {
                Directory.CreateDirectory(src);
                var tf = new[] { "sample_photo.jpg","vacation_2024.png","screenshot.gif","movie_clip.mp4","tutorial.avi", "presentation.pptx","report_2024.pdf","notes.txt","spreadsheet.xlsx","document.docx" };
                int c = 0;
                foreach (var f in tf)
                {
                    var p = Path.Combine(src, f);
                    if (!File.Exists(p)) { File.WriteAllText(p, $"테스트 파일: {f}\n{DateTime.Now}"); c++; }
                }
                return c;
            }
            catch (Exception ex) { ErrorService.Report(ex, "TestDataService"); return 0; }
        }
        public static void ClearTestFiles(string src)
        {
            if (string.IsNullOrEmpty(src) || !Directory.Exists(src)) return;
            try { foreach (var f in Directory.GetFiles(src)) { try { if (File.ReadAllText(f).StartsWith("테스트 파일:")) File.Delete(f); } catch { } } }
            catch (Exception ex) { ErrorService.Report(ex, "TestDataService.Clear"); }
        }
    }
}
