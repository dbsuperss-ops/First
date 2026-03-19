using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.Views;

public partial class NewScanView : UserControl
{
    public NewScanView() => InitializeComponent();

    private async void BrowseIncludeFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFolderAsync();
            if (path is not null && DataContext is NewScanViewModel vm)
                vm.NewIncludePath = path;
        }
        catch (Exception ex)
        {
            if (DataContext is NewScanViewModel vm)
            {
                vm.ValidationError = $"폴더 선택 오류: {ex.GetType().Name}: {ex.Message}";
                vm.HasValidationError = true;
            }
        }
    }

    private async void BrowseExcludeFolder_Click(object? sender, RoutedEventArgs e)
    {
        try
        {
            var path = await PickFolderAsync();
            if (path is not null && DataContext is NewScanViewModel vm)
                vm.NewExcludePath = path;
        }
        catch (Exception ex)
        {
            if (DataContext is NewScanViewModel vm)
            {
                vm.ValidationError = $"폴더 선택 오류: {ex.GetType().Name}: {ex.Message}";
                vm.HasValidationError = true;
            }
        }
    }

    private async Task<string?> PickFolderAsync()
    {
        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return null;

        var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
            new FolderPickerOpenOptions { Title = "폴더 선택", AllowMultiple = false });

        if (folders.Count == 0) return null;
        var uri = folders[0].Path;
        if (uri is null) return null;
        return uri.IsAbsoluteUri ? uri.LocalPath : uri.OriginalString;
    }
}
