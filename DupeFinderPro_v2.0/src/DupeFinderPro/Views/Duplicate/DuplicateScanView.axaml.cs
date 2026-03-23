#pragma warning disable CS0618 // DragEventArgs.Data / DataFormats.Files obsolete in Avalonia 11 preview
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DupeFinderPro.ViewModels.Duplicate;

namespace DupeFinderPro.Views.Duplicate;

public partial class DuplicateScanView : UserControl
{
    public DuplicateScanView()
    {
        InitializeComponent();

        WireDragDrop("IncludeDropZone", isInclude: true);
        WireDragDrop("ExcludeDropZone", isInclude: false);
    }

    private void WireDragDrop(string controlName, bool isInclude)
    {
        var zone = this.FindControl<Border>(controlName);
        if (zone is null) return;

        DragDrop.SetAllowDrop(zone, true);
        zone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
        zone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
        zone.AddHandler(DragDrop.DropEvent, isInclude ? OnIncludeDrop : OnExcludeDrop);
    }

    private static void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
    }

    private static void OnDragLeave(object? sender, DragEventArgs e) { }

    private void OnIncludeDrop(object? sender, DragEventArgs e) =>
        AddDroppedFolders(e, include: true);

    private void OnExcludeDrop(object? sender, DragEventArgs e) =>
        AddDroppedFolders(e, include: false);

    private void AddDroppedFolders(DragEventArgs e, bool include)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;
        var files = e.Data.GetFiles();
        if (files is null) return;

        if (DataContext is not DuplicateScanViewModel vm) return;

        foreach (var item in files)
        {
            if (item is not IStorageFolder folder) continue;

            var path = folder.Path?.IsAbsoluteUri == true
                ? folder.Path.LocalPath
                : folder.Path?.OriginalString;

            if (path is null) continue;

            if (include)
                vm.AddIncludePathCommand.Execute(path);
            else
                vm.AddExcludePathCommand.Execute(path);
        }
    }

    private async void BrowseIncludeFolder_Click(object? sender, RoutedEventArgs e)
    {
        var paths = await PickFoldersAsync();
        if (DataContext is not DuplicateScanViewModel vm) return;
        foreach (var path in paths)
            vm.AddIncludePathCommand.Execute(path);
    }

    private async void BrowseExcludeFolder_Click(object? sender, RoutedEventArgs e)
    {
        var paths = await PickFoldersAsync();
        if (DataContext is not DuplicateScanViewModel vm) return;
        foreach (var path in paths)
            vm.AddExcludePathCommand.Execute(path);
    }

    private async Task<IReadOnlyList<string>> PickFoldersAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return [];

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "폴더 선택", AllowMultiple = true });

            return folders
                .Select(f => f.Path?.IsAbsoluteUri == true ? f.Path.LocalPath : f.Path?.OriginalString)
                .OfType<string>()
                .ToList()
                .AsReadOnly();
        }
        catch { return []; }
    }
}
