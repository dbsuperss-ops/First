using DupeFinderPro.Domain.Models.Organize;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed class ClassifyResultItemViewModel : ViewModelBase
{
    public string FileName { get; }
    public string SourcePath { get; }
    public string TargetPath { get; }
    public string RuleName { get; }
    public long FileSize { get; }
    public string FileSizeText { get; }

    public ClassifyResultItemViewModel(ClassifyResult result)
    {
        FileName = result.FileName;
        SourcePath = result.SourcePath;
        TargetPath = result.TargetPath;
        RuleName = result.RuleName;
        FileSize = result.FileSize;
        FileSizeText = FormatSize(result.FileSize);
    }

    private static string FormatSize(long bytes) =>
        bytes >= 1_073_741_824 ? $"{bytes / 1_073_741_824.0:F1} GB" :
        bytes >= 1_048_576     ? $"{bytes / 1_048_576.0:F1} MB"     :
        bytes >= 1_024         ? $"{bytes / 1_024.0:F1} KB"         :
                                 $"{bytes} B";
}
