using System;

namespace FileLister.Models
{
    public class FileItem
    {
        public string FileName { get; set; } = "";
        public string Extension { get; set; } = "";
        public string Category { get; set; } = "";
        public long SizeBytes { get; set; }

        // [수정] string → DateTime: DataGrid 날짜 정렬 정상 동작
        public DateTime CreatedTime { get; set; }
        public DateTime LastWriteTime { get; set; }

        public string DirectoryPath { get; set; } = "";
        public string FullPath { get; set; } = "";

        // [추가] 사람이 읽기 편한 크기 표시 (KB/MB/GB 자동 단위)
        public string SizeFormatted => SizeBytes switch
        {
            < 1024 => $"{SizeBytes} B",
            < 1024 * 1024 => $"{SizeBytes / 1024.0:F1} KB",
            < 1024 * 1024 * 1024 => $"{SizeBytes / (1024.0 * 1024):F1} MB",
            _ => $"{SizeBytes / (1024.0 * 1024 * 1024):F2} GB"
        };
    }
}
