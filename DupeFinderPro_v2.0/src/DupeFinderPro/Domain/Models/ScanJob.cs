namespace DupeFinderPro.Domain.Models;

public sealed class ScanJob
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Name { get; }
    public ScanJobStatus Status { get; set; } = ScanJobStatus.Pending;
    public ScanFilter Filter { get; }
    public DateTime CreatedAt { get; } = DateTime.Now;
    public ScanResult? Result { get; set; }
    public string? ErrorMessage { get; set; }

    public ScanJob(string name, ScanFilter filter)
    {
        Name = name;
        Filter = filter;
    }

    public string PathsSummary => Filter.IncludePaths.Count switch
    {
        0 => "(no paths)",
        1 => Filter.IncludePaths[0],
        _ => $"{Filter.IncludePaths[0]} +{Filter.IncludePaths.Count - 1} more"
    };

    public string FileTypesSummary
    {
        get
        {
            if (Filter.FileTypes.Count == 0)
                return "All types";

            return string.Join(", ", Filter.FileTypes.Select(FileTypeCategoryExtensions.GetLabel));
        }
    }
}

public enum ScanJobStatus
{
    Pending,
    Running,
    Completed,
    Cancelled,
    Failed
}
