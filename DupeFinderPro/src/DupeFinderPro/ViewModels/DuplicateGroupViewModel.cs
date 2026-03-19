using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels;

public sealed partial class DuplicateGroupViewModel : ObservableObject
{
    [ObservableProperty] private bool _isExpanded = true;

    public string Hash          => Group.Hash[..Math.Min(8, Group.Hash.Length)] + "…";
    public string FileCount     => $"{Group.Files.Count} files";
    public string WastedBytes   => FormatBytes(Group.WastedBytes);
    public string TotalSize     => FormatBytes(Group.SizeBytes * Group.Files.Count);
    public DuplicateGroup Group { get; }

    public ObservableCollection<FileEntryViewModel> Files { get; }

    public DuplicateGroupViewModel(DuplicateGroup group, CleanupOrchestrator cleanup)
    {
        Group = group;
        Files = new ObservableCollection<FileEntryViewModel>(
            group.Files.Select((f, i) =>
            {
                var vm = new FileEntryViewModel(f, cleanup);
                // Mark the suggested-keep file as Keep
                if (group.SuggestedKeep?.FullPath == f.FullPath)
                    vm.SelectedAction = FileAction.Keep;
                else if (i > 0)
                    vm.SelectedAction = FileAction.Delete;
                return vm;
            }));
    }

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

    public async Task ApplyAsync(string quarantinePath, string moveToPath, CancellationToken ct)
    {
        foreach (var file in Files)
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
