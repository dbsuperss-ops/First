using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Windows;
using System.Windows.Controls;
using Forms = System.Windows.Forms;
namespace FileFlow.Pages
{
    public partial class DuplicatePage : Page
    {
        private readonly ObservableCollection<DupItem> _items = new();
        public DuplicatePage() { InitializeComponent(); DuplicateList.ItemsSource = _items; }

        private void BtnBrowse_Click(object s, RoutedEventArgs e) { using var d = new Forms.FolderBrowserDialog();
            if (d.ShowDialog() == Forms.DialogResult.OK) TxtFolder.Text = d.SelectedPath; }

        private async void BtnScan_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrEmpty(TxtFolder.Text)) { MessageBox.Show("폴더를 선택하세요!"); return; }
            _items.Clear(); TxtResult.Text = "검사 중..."; BtnScan.IsEnabled = false;
            try
            {
                var dups = await System.Threading.Tasks.Task.Run(() => FindDups(TxtFolder.Text));
                int dc = 0;
                foreach (var g in dups) { bool first = true;
                    foreach (var p in g) { var i = new FileInfo(p);
                        _items.Add(new DupItem { FileName = (first ? "[원본] " : "[중복] ") + Path.GetFileName(p), FilePath = p, FileSize = FmtSize(i.Length), IsSelected = !first, CanSelect = !first });
                        if (!first) dc++; first = false; } }
                if (_items.Count > 0) { PnlEmptyState.Visibility = Visibility.Collapsed; PnlResult.Visibility = Visibility.Visible; }
                TxtResult.Text = $"완료! 중복: {dc}개";
            }
            catch (Exception ex) { Services.ErrorService.Report(ex, "Dup"); MessageBox.Show("오류: " + ex.Message); }
            finally { BtnScan.IsEnabled = true; }
        }

        private List<List<string>> FindDups(string folder)
        {
            var files = Directory.GetFiles(folder, "*", new EnumerationOptions { IgnoreInaccessible = true, RecurseSubdirectories = true });
            var sizeGroups = files.Select(f => { try { return (p: f, s: new FileInfo(f).Length); } catch { return (p: f, s: -1L); } })
                .Where(f => f.s > 0).GroupBy(f => f.s).Where(g => g.Count() > 1).ToList();
            var hashGroups = new Dictionary<string, List<string>>();
            foreach (var g in sizeGroups) foreach (var f in g)
                try { var h = Hash(f.p);
                    if (!hashGroups.ContainsKey(h)) hashGroups[h] = new(); hashGroups[h].Add(f.p); } catch { }
            return hashGroups.Values.Where(g => g.Count > 1).ToList();
        }

        private string Hash(string p) { using var s = SHA256.Create();
            using var f = File.OpenRead(p); return BitConverter.ToString(s.ComputeHash(f)); }
        private string FmtSize(long b) => b >= 1073741824 ?
            $"{b/1073741824.0:F2} GB" : b >= 1048576 ? $"{b/1048576.0:F2} MB" : b >= 1024 ?
            $"{b/1024.0:F2} KB" : b + " B";

        private void BtnSelectAll_Click(object s, RoutedEventArgs e) { foreach (var i in _items.Where(i => !i.FileName.StartsWith("[원본]"))) i.IsSelected = true;
            DuplicateList.Items.Refresh(); }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            var td = _items.Where(i => i.IsSelected && i.CanSelect).ToList();
            if (td.Count == 0) { MessageBox.Show("삭제할 중복 파일을 선택하세요!"); return; }
            if (MessageBox.Show($"중복 파일 {td.Count}개를 삭제합니까?\n원본 파일은 보존됩니다.", "확인", MessageBoxButton.YesNo) != MessageBoxResult.Yes) return;
            int d = 0; foreach (var i in td) { try { File.Delete(i.FilePath); _items.Remove(i); d++; } catch { } }
            MessageBox.Show($"{d}개 삭제 완료!");
        }
    }
    public class DupItem { public string FileName { get; set; } = ""; public string FilePath { get; set; } = ""; public string FileSize { get; set; } = ""; public bool IsSelected { get; set; } public bool CanSelect { get; set; } = true; }
}
