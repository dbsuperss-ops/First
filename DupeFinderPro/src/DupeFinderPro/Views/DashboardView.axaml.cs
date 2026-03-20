using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.Views;

public partial class DashboardView : UserControl
{
    public DashboardView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is DashboardViewModel vm)
                vm.Refresh();
        };
    }

    private async void BrowseEmptyFolderRoot_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "폴더 선택", AllowMultiple = false });

            if (folders.Count == 0) return;
            var uri = folders[0].Path;
            if (uri is null) return;

            var path = uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;
            if (path is not null && DataContext is DashboardViewModel vm)
                vm.EmptyFolderRoot = path;
        }
        catch { /* 취소 또는 오류 무시 */ }
    }
}
