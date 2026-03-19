using System;
using System.Collections.Generic;
namespace FileFlow.Models
{
    public class Scenario
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public string SourceFolder { get; set; } = "";
        public string TargetFolder { get; set; } = "";
        public bool IncludeSubfolders { get; set; } = false;
        public bool ExcludeSystemFiles { get; set; } = true;
        public bool CleanupEmptyFolders { get; set; } = false;
        public ConflictMode ConflictMode { get; set; } = ConflictMode.Rename;
        public List<ClassifyRule> Rules { get; set; } = new();
        public bool IsScheduled { get; set; } = false;
        public string ScheduleTime { get; set; } = "00:00";
        public List<string> ScheduleDays { get; set; } = new();
    }
}
