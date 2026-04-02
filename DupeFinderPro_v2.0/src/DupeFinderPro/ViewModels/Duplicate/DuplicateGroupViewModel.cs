using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels.Duplicate;

public sealed partial class DuplicateGroupViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;

    public int    GroupNumber { get; set; }
    public string GroupLabel  => $"#{GroupNumber}";

    public string Hash        => Group.Hash[..Math.Min(8, Group.Hash.Length)] + "…";
    public string FileCount   => $"{Group.Files.Count}개 파일";
    public string WastedBytes => FormatBytes(Group.WastedBytes);
    public string ExpandIcon  => IsExpanded ? "▲" : "▼";
    public string TotalSize   => FormatBytes(Group.SizeBytes * Group.Files.Count);
    public DuplicateGroup Group { get; }

    public ObservableCollection<FileEntryViewModel> Files { get; }

    // True when ALL files in the group are checked; setting it checks/unchecks all
    public bool IsGroupChecked
    {
        get => Files.Count > 0 && Files.All(f => f.IsChecked);
        set
        {
            foreach (var f in Files)
                f.IsChecked = value;
            OnPropertyChanged(nameof(IsGroupChecked));
        }
    }

    public DuplicateGroupViewModel(DuplicateGroup group, CleanupOrchestrator cleanup)
    {
        Group = group;
        Files = new ObservableCollection<FileEntryViewModel>(
            group.Files.Select((f, i) =>
            {
                var vm = new FileEntryViewModel(f, cleanup);
                if (group.SuggestedKeep?.FullPath == f.FullPath)
                    vm.SelectedAction = FileAction.Keep;
                else if (i > 0)
                    vm.SelectedAction = FileAction.Delete;
                return vm;
            }));

        // Bubble file-level checkbox changes up to group checkbox
        foreach (var vm in Files)
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(FileEntryViewModel.IsChecked))
                    OnPropertyChanged(nameof(IsGroupChecked));
            };
    }

    partial void OnIsExpandedChanged(bool value) => OnPropertyChanged(nameof(ExpandIcon));

    [RelayCommand]
    private void ToggleExpanded() => IsExpanded = !IsExpanded;

    [RelayCommand]
    private void AutoSelect()
    {
        bool first = true;
        foreach (var file in Files)
        {
            file.SelectedAction = first ? FileAction.Keep : FileAction.Delete;
            first = false;
        }
    }

    [RelayCommand]
    private void KeepAll()
    {
        foreach (var file in Files)
            file.SelectedAction = FileAction.Keep;
    }

    // Bulk apply — processes only checked, non-done files
    public async Task ApplyCheckedAsync(string quarantinePath, string moveToPath, CancellationToken ct)
    {
        foreach (var file in Files.Where(f => f.IsChecked))
            await file.ApplyActionAsync(quarantinePath, moveToPath, ct);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
