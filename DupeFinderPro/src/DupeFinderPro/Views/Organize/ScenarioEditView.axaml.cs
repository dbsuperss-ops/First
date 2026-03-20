using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DupeFinderPro.ViewModels.Organize;

namespace DupeFinderPro.Views.Organize;

public partial class ScenarioEditView : UserControl
{
    public ScenarioEditView() => InitializeComponent();

    private async void BrowseSourceFolder_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null && DataContext is ScenarioEditViewModel vm)
            vm.SourceFolder = path;
    }

    private async void BrowseTargetFolder_Click(object? sender, RoutedEventArgs e)
    {
        var path = await PickFolderAsync();
        if (path is not null && DataContext is ScenarioEditViewModel vm)
            vm.TargetFolder = path;
    }

    private async Task<string?> PickFolderAsync()
    {
        try
        {
            var topLevel = TopLevel.GetTopLevel(this);
            if (topLevel is null) return null;

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(
                new FolderPickerOpenOptions { Title = "폴더 선택", AllowMultiple = false });

            if (folders.Count == 0) return null;
            var uri = folders[0].Path;
            return uri?.IsAbsoluteUri == true ? uri.LocalPath : uri?.OriginalString;
        }
        catch { return null; }
    }
}
