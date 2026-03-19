using System;
namespace FileFlow.Models { public class LogEntry { public Guid BatchId { get; set; } = Guid.Empty; public DateTime Timestamp { get; set; } public string FileName { get; set; } = ""; public string SourcePath { get; set; } = ""; public string TargetPath { get; set; } = ""; public string Action { get; set; } = ""; } }
