#pragma warning disable CS0618 // DragEventArgs.Data / DataFormats.Files obsolete in Avalonia 11 preview
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DupeFinderPro.ViewModels.Organize;

namespace DupeFinderPro.Views.Organize;

public partial class OrganizeRunView : UserControl
{
    public OrganizeRunView()
    {
        InitializeComponent();

        var zone = this.FindControl<Border>("DropZone");
        if (zone is not null)
        {
            DragDrop.SetAllowDrop(zone, true);
            zone.AddHandler(DragDrop.DragEnterEvent, OnDragEnter);
            zone.AddHandler(DragDrop.DragLeaveEvent, OnDragLeave);
            zone.AddHandler(DragDrop.DropEvent,      OnDrop);
        }
    }

    private void OnDragEnter(object? sender, DragEventArgs e)
    {
        if (e.Data.Contains(DataFormats.Files))
            e.DragEffects = DragDropEffects.Copy;
    }

    private void OnDragLeave(object? sender, DragEventArgs e) { }

    private void OnDrop(object? sender, DragEventArgs e)
    {
        if (!e.Data.Contains(DataFormats.Files)) return;

        var files = e.Data.GetFiles();
        if (files is null) return;

        foreach (var item in files)
        {
            if (item is IStorageFolder folder && DataContext is OrganizeRunViewModel vm)
            {
                var path = folder.Path?.IsAbsoluteUri == true
                    ? folder.Path.LocalPath
                    : folder.Path?.OriginalString;

                if (path is not null) vm.SetOverrideFolderCommand.Execute(path);
                break;
            }
        }
    }

    private async void BrowseOverrideFolder_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null && DataContext is OrganizeRunViewModel vm)
            vm.SetOverrideFolderCommand.Execute(path);
    }

    private async Task<string?> PickFolderAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return null;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "오버라이드 폴더 선택", AllowMultiple = false });

            if (folders.Count == 0) return null;
            var uri = folders[0].Path;
            return uri?.IsAbsoluteUri == true ? uri.LocalPath : uri?.OriginalString;
        }
        catch { return null; }
    }
}
