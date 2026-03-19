using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace DuplicateFinder
{
    public partial class MainWindow : Window
    {
        public ObservableCollection<FileNode> DisplayList { get; set; } = new();

        public MainWindow()
        {
            InitializeComponent();
            ResultGrid.ItemsSource = DisplayList;
        }

        public class FileNode
        {
            public bool IsSelected { get; set; }
            public string FullPath { get; set; }
            public string FileName => Path.GetFileName(FullPath);
            public long SizeRaw { get; set; }
            public string SizeMB => (SizeRaw / 1024.0 / 1024.0).ToString("N2");
            public DateTime ModifiedDate { get; set; }
            public string PartialHash { get; set; } // 1단계 해시용
            public string FullHash { get; set; }    // 2단계 해시용 (최종 비교)
        }

        private async void OnScanClick(object sender, RoutedEventArgs e)
        {
            string[] paths = PathInput.Text?.Split(';', StringSplitOptions.RemoveEmptyEntries);
            if (paths == null || paths.Length == 0) return;

            DisplayList.Clear();
            StatusLabel.Text = "파일 목록 수집 중...";

            // 스캔 및 필터링 비동기 진행
            var allFiles = await Task.Run(() => ScanFiles(paths));
            StatusLabel.Text = $"빠른 해시 비교 중 ({allFiles.Count}개 파일)...";

            // 해시 분석 진행
            var duplicates = await Task.Run(() => FindDuplicates(allFiles));
            
            // 성능을 위해 Collection을 재생성하여 UI 바인딩 갱신
            DisplayList = new ObservableCollection<FileNode>(duplicates);
            ResultGrid.ItemsSource = DisplayList;
            
            StatusLabel.Text = $"검색 완료: {duplicates.Count}개의 중복 파일 발견";
        }

        private List<FileNode> ScanFiles(string[] rootPaths)
        {
            var fileList = new List<FileNode>();
            
            var includeExt = ExtInclude.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(x => x.Trim().ToLower()).ToHashSet();
            var excludeExt = ExtExclude.Text?.Split(',', StringSplitOptions.RemoveEmptyEntries)
                                           .Select(x => x.Trim().ToLower()).ToHashSet();
            string keyword = KeywordFilter.Text?.Trim();

            // 접근 권한 에러 방지 옵션 (중요)
            var enumOptions = new EnumerationOptions
            {
                IgnoreInaccessible = true,
                RecurseSubdirectories = CheckSubfolders.IsChecked == true,
            };

            foreach (var root in rootPaths)
            {
                if (!Directory.Exists(root)) continue;
                
                try 
                {
                    foreach (var path in Directory.EnumerateFiles(root, "*.*", enumOptions))
                    {
                        // 1. 시스템 폴더 보호
                        if (path.StartsWith(@"C:\Windows", StringComparison.OrdinalIgnoreCase)) continue;

                        var info = new FileInfo(path);
                        
                        // 시스템, 숨김 파일 보호
                        if (info.Attributes.HasFlag(FileAttributes.System) || info.Attributes.HasFlag(FileAttributes.ReparsePoint)) continue;

                        // 2. 필터링 (대소문자 무시 적용)
                        string ext = info.Extension.ToLower();
                        if (includeExt?.Any() == true && !includeExt.Contains(ext)) continue;
                        if (excludeExt?.Any() == true && excludeExt.Contains(ext)) continue;
                        
                        long minSize = (long)(MinSizeNum.Value ?? 0) * 1024;
                        if (info.Length < minSize) continue;
                        
                        if (DateFrom.SelectedDate.HasValue && info.LastWriteTime < DateFrom.SelectedDate.Value.DateTime) continue;
                        if (!string.IsNullOrEmpty(keyword) && !info.Name.Contains(keyword, StringComparison.OrdinalIgnoreCase)) continue;

                        fileList.Add(new FileNode { FullPath = path, SizeRaw = info.Length, ModifiedDate = info.LastWriteTime });
                    }
                } 
                catch { /* 최상위 폴더 자체 접근 불가 무시 */ }
            }
            return fileList;
        }

        private List<FileNode> FindDuplicates(List<FileNode> files)
        {
            var result = new ConcurrentBag<FileNode>(); // 병렬 처리를 위한 스레드 안전 컬렉션

            // 1단계: 크기가 같은 파일 그룹화 (1Byte라도 다르면 중복 아님)
            var sizeGroups = files.GroupBy(f => f.SizeRaw).Where(g => g.Count() > 1);

            Parallel.ForEach(sizeGroups, sizeGroup =>
            {
                // 2단계: 크기가 같은 파일들끼리 파일 앞부분(Max 4KB)만 빠르게 읽어 부분 해시 비교
                var partialGroups = new Dictionary<string, List<FileNode>>();
                foreach (var file in sizeGroup)
                {
                    file.PartialHash = GetPartialHash(file.FullPath, 4096);
                    if (file.PartialHash == null) continue;

                    if (!partialGroups.ContainsKey(file.PartialHash)) partialGroups[file.PartialHash] = new List<FileNode>();
                    partialGroups[file.PartialHash].Add(file);
                }

                // 3단계: 부분 해시까지 똑같은 잠재적 그룹들만 "전체 해시" 정밀 비교
                foreach (var pGroup in partialGroups.Values.Where(v => v.Count > 1))
                {
                    var fullHashGroups = new Dictionary<string, List<FileNode>>();
                    foreach (var file in pGroup)
                    {
                        file.FullHash = GetFullHash(file.FullPath);
                        if (file.FullHash == null) continue;

                        if (!fullHashGroups.ContainsKey(file.FullHash)) fullHashGroups[file.FullHash] = new List<FileNode>();
                        fullHashGroups[file.FullHash].Add(file);
                    }

                    // 4단계: 진짜 중복된 애들만 추출하여 하나 빼고 삭제 체크박스 On
                    foreach (var fGroup in fullHashGroups.Values.Where(v => v.Count > 1))
                    {
                        // 날짜가 가장 오래된 것을 원본, 최신 것을 사본으로 가정하여 최신 파일들을 선택되도록 정렬 로직을 추가해도 좋습니다.
                        // fGroup.Sort((a, b) => a.ModifiedDate.CompareTo(b.ModifiedDate)); 
                        
                        for (int i = 0; i < fGroup.Count; i++)
                        {
                            fGroup[i].IsSelected = (i > 0);
                            result.Add(fGroup[i]);
                        }
                    }
                }
            });

            // 크기/이름 기준으로 보기 좋게 정렬하여 반환
            return result.OrderByDescending(x => x.SizeRaw).ThenBy(x => x.FileName).ToList();
        }

        // 빠른 부분 해시 계산 (파일 헤더 4KB)
        private string GetPartialHash(string path, int bytesToRead)
        {
            try {
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                byte[] buffer = new byte[bytesToRead];
                int read = fs.Read(buffer, 0, bytesToRead);
                if (read == 0) return "empty";
                
                using var sha = SHA256.Create();
                return Convert.ToHexString(sha.ComputeHash(buffer, 0, read));
            } catch { return null; }
        }

        // 전체 파일 해시 계산
        private string GetFullHash(string path)
        {
            try {
                using var sha = SHA256.Create();
                // FileShare.ReadWrite 적용하여 다른 프로그램이 사용중이어도 읽기 시도
                using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                return Convert.ToHexString(sha.ComputeHash(fs));
            } catch { return null; }
        }

        private async void OnDeleteClick(object sender, RoutedEventArgs e)
        {
            var targets = DisplayList.Where(x => x.IsSelected).ToList();
            if (targets.Count == 0) return;

            StatusLabel.Text = "삭제 진행 중...";
            int count = 0;

            // UI 블로킹 방지를 위해 백그라운드 처리
            await Task.Run(() =>
            {
                foreach (var file in targets)
                {
                    try {
                        File.Delete(file.FullPath);
                        count++;
                    } catch { } // 삭제 실패 무시
                }
            });

            // 삭제 성공한 항목만 UI 목록에서 제거
            foreach(var file in targets) 
                DisplayList.Remove(file);

            StatusLabel.Text = $"{count}개의 파일이 영구 삭제되었습니다.";
        }

        private async void OnQuarantineClick(object sender, RoutedEventArgs e)
        {
            var targets = DisplayList.Where(x => x.IsSelected).ToList();
            if (targets.Count == 0) return;

            string qPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Quarantine");
            if (!Directory.Exists(qPath)) Directory.CreateDirectory(qPath);

            StatusLabel.Text = "격리 폴더로 이동 중...";
            int count = 0;

            await Task.Run(() =>
            {
                foreach (var file in targets)
                {
                    try {
                        string dest = Path.Combine(qPath, Guid.NewGuid() + "_" + file.FileName);
                        File.Move(file.FullPath, dest);
                        count++;
                    } catch { }
                }
            });

            foreach(var file in targets) 
                DisplayList.Remove(file);

            StatusLabel.Text = $"{count}개의 파일을 격리({qPath})했습니다.";
        }

        private async void OnSaveLogClick(object sender, RoutedEventArgs e)
        {
            if (DisplayList.Count == 0) return;
            
            var log = DisplayList.Select(x => $"{x.FileName} | {x.FullPath} | {x.SizeMB}MB");
            await File.WriteAllLinesAsync("ScanResult.txt", log);
            StatusLabel.Text = "결과가 애플리케이션 폴더의 ScanResult.txt에 저장되었습니다.";
        }
    }
}