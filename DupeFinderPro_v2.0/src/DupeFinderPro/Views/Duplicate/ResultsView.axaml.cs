#pragma warning disable CS0618 // DataFormats.Files obsolete in Avalonia 11 preview
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using Avalonia.VisualTree;
using DupeFinderPro.ViewModels.Duplicate;

namespace DupeFinderPro.Views.Duplicate;

public partial class ResultsView : UserControl
{
    private Point _pressPoint;
    private FileEntryViewModel? _pendingDrag;

    public ResultsView()
    {
        InitializeComponent();
        AddHandler(PointerPressedEvent, OnPointerPressed, handledEventsToo: false);
        AddHandler(PointerMovedEvent, OnPointerMoved, handledEventsToo: false);
        AddHandler(PointerReleasedEvent, (object? _, PointerReleasedEventArgs _) => _pendingDrag = null,
            handledEventsToo: false);
    }

    private async void BrowseQuarantineFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ResultsViewModel vm) return;
        var folder = await PickFolderAsync();
        if (folder is not null)
            vm.QuarantinePath = folder;
    }

    private async void BrowseMoveToFolder_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ResultsViewModel vm) return;
        var folder = await PickFolderAsync();
        if (folder is not null)
            vm.MoveToPath = folder;
    }

    private async Task<string?> PickFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;
        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "폴더 선택", AllowMultiple = false });
        return folders is [var f, ..] ? f.TryGetLocalPath() : null;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;
        _pressPoint = e.GetPosition(this);
        _pendingDrag = FindFileEntryVm(e.Source as Visual);
    }

    private async void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_pendingDrag is null) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { _pendingDrag = null; return; }

        var pos = e.GetPosition(this);
        if (Math.Abs(pos.X - _pressPoint.X) < 8 && Math.Abs(pos.Y - _pressPoint.Y) < 8) return;

        var vm = _pendingDrag;
        _pendingDrag = null;
        if (!File.Exists(vm.FullPath)) return;

        var data = new DataObject();
        data.Set(DataFormats.Files, new[] { vm.FullPath });
        await DragDrop.DoDragDrop(e, data, DragDropEffects.Copy | DragDropEffects.Link);
    }

    private static FileEntryViewModel? FindFileEntryVm(Visual? visual)
    {
        var v = visual;
        while (v is not null)
        {
            if (v is Control c && c.DataContext is FileEntryViewModel vm)
                return vm;
            v = v.GetVisualParent();
        }
        return null;
    }
}
