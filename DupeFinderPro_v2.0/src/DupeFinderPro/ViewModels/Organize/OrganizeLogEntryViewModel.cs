using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed class OrganizeLogEntryViewModel : ViewModelBase
{
    public Guid BatchId { get; }
    public string Timestamp { get; }
    public string FileName { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }
    public string Action { get; }

    public OrganizeLogEntryViewModel(OrganizeLogEntry entry)
    {
        BatchId = entry.BatchId;
        Timestamp = entry.Timestamp.ToString("yyyy-MM-dd HH:mm:ss");
        FileName = entry.FileName;
        SourcePath = entry.SourcePath;
        TargetPath = entry.TargetPath;
        Action = entry.Action;
    }
}
