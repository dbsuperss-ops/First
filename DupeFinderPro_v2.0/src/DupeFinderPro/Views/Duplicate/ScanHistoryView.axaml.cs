using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using DupeFinderPro.ViewModels.Duplicate;

namespace DupeFinderPro.Views.Duplicate;

public partial class ScanHistoryView : UserControl
{
    public ScanHistoryView()
    {
        InitializeComponent();
        DataContextChanged += (_, _) =>
        {
            if (DataContext is ScanHistoryViewModel vm)
                vm.Refresh();
        };
    }

    private async void Export_Click(object? sender, RoutedEventArgs e)
    {
        if (DataContext is not ScanHistoryViewModel vm) return;

        var topLevel = TopLevel.GetTopLevel(this);
        if (topLevel is null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "스캔 기록 내보내기",
            DefaultExtension = "csv",
            SuggestedFileName = $"DupeFinderPro_기록_{DateTime.Now:yyyyMMdd}",
            FileTypeChoices = [new FilePickerFileType("CSV 파일") { Patterns = ["*.csv"] }]
        });

        if (file is null) return;

        await using var stream = await file.OpenWriteAsync();
        await using var writer = new StreamWriter(stream, System.Text.Encoding.UTF8);
        await writer.WriteAsync(vm.GetExportCsv());
    }
}
