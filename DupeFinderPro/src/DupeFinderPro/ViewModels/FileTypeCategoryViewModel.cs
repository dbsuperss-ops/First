using CommunityToolkit.Mvvm.ComponentModel;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.ViewModels;

public sealed partial class FileTypeCategoryViewModel : ObservableObject
{
    [ObservableProperty] private bool _isSelected;

    public FileTypeCategory Category { get; }
    public string Label       => FileTypeCategoryExtensions.GetLabel(Category);
    public string Description => FileTypeCategoryExtensions.GetDescription(Category);

    public FileTypeCategoryViewModel(FileTypeCategory category, bool isSelected = false)
    {
        Category = category;
        _isSelected = isSelected;
    }
}
