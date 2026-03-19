using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FileLister.Models;

namespace FileLister.Services
{
    public class FileScanService
    {
        public List<FileItem> ScanFolders(
            IEnumerable<string> folderPaths,
            bool includeSubfolders,
            bool excludeHidden,
            bool excludeSystem,
            IProgress<int>? progress = null)
        {
            var items = new List<FileItem>();
            // 복수 폴더 스캔 시 동일 경로 중복 방지
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            int count = 0;

            foreach (var folderPath in folderPaths)
            {
                var di = new DirectoryInfo(folderPath);
                if (!di.Exists) continue;

                foreach (var file in EnumerateFilesSafe(di, includeSubfolders))
                {
                    // [기능1] 숨김/시스템 파일 제외
                    if (excludeHidden && file.Attributes.HasFlag(FileAttributes.Hidden)) continue;
                    if (excludeSystem && file.Attributes.HasFlag(FileAttributes.System)) continue;

                    // 중복 경로 건너뜀
                    if (!seen.Add(file.FullName)) continue;

                    items.Add(new FileItem
                    {
                        FileName = file.Name,
                        Extension = file.Extension,
                        Category = CategorizeFile(file.Extension),
                        SizeBytes = GetFileSizeSafe(file),
                        CreatedTime = file.CreationTime,
                        LastWriteTime = file.LastWriteTime,
                        DirectoryPath = file.DirectoryName ?? "",
                        FullPath = file.FullName
                    });

                    count++;
                    // [기능5] 50개 단위로 진행률 보고 (UI 업데이트 빈도 조절)
                    if (count % 50 == 0)
                        progress?.Report(count);
                }
            }

            progress?.Report(count);
            return items.OrderBy(x => x.FileName).ToList();
        }

        // 폴더별 예외 처리: 접근 불가 폴더 있어도 나머지 스캔 계속
        private IEnumerable<FileInfo> EnumerateFilesSafe(DirectoryInfo dir, bool includeSubfolders)
        {
            IEnumerable<FileInfo> files;
            try
            {
                files = dir.EnumerateFiles("*", SearchOption.TopDirectoryOnly);
            }
            catch (UnauthorizedAccessException) { yield break; }
            catch (IOException) { yield break; }

            foreach (var file in files)
                yield return file;

            if (!includeSubfolders) yield break;

            IEnumerable<DirectoryInfo> subdirs;
            try
            {
                subdirs = dir.EnumerateDirectories();
            }
            catch (UnauthorizedAccessException) { yield break; }
            catch (IOException) { yield break; }

            foreach (var subdir in subdirs)
                foreach (var file in EnumerateFilesSafe(subdir, true))
                    yield return file;
        }

        private static long GetFileSizeSafe(FileInfo file)
        {
            try { return file.Length; }
            catch { return 0; }
        }

        // [기능2] 카테고리 재구성: 스프레드시트·프레젠테이션 → 문서로 통합
        private static string CategorizeFile(string extension)
        {
            return extension.ToLower() switch
            {
                ".doc" or ".docx" or ".txt" or ".pdf" or ".hwp" or ".rtf"
                    or ".xls" or ".xlsx" or ".csv"
                    or ".ppt" or ".pptx" => "문서",
                ".jpg" or ".jpeg" or ".png" or ".gif" or ".bmp"
                    or ".webp" or ".svg" or ".ico" or ".tiff" => "이미지",
                ".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" => "압축",
                ".cs" or ".js" or ".ts" or ".py" or ".java" or ".cpp" or ".c" or ".h"
                    or ".xml" or ".json" or ".yaml" or ".yml" or ".config"
                    or ".html" or ".css" or ".md" => "코드/설정",
                ".mp3" or ".mp4" or ".avi" or ".mov" or ".wav" or ".flac" or ".mkv" => "미디어",
                ".exe" or ".dll" or ".msi" or ".bat" or ".cmd" => "실행파일",
                _ => "기타"
            };
        }
    }
}
