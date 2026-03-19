using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.ViewModels;

public enum FileAction { Keep, MoveToFolder, Quarantine, Delete }

public sealed partial class FileEntryViewModel : ObservableObject
{
    private readonly CleanupOrchestrator _cleanup;

    [ObservableProperty] private FileAction _selectedAction = FileAction.Keep;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public FileEntry Entry { get; }

    public string FileName     => Entry.FileName;
    public string FullPath     => Entry.FullPath;
    public string SizeFormatted => FormatBytes(Entry.SizeBytes);
    public string LastModified => Entry.LastModified.ToString("g");
    public string CreatedAt    => Entry.CreatedAt.ToString("g");

    public bool IsKeep        => SelectedAction == FileAction.Keep;
    public bool IsMoveToFolder => SelectedAction == FileAction.MoveToFolder;
    public bool IsQuarantine  => SelectedAction == FileAction.Quarantine;
    public bool IsDelete      => SelectedAction == FileAction.Delete;

    public FileEntryViewModel(FileEntry entry, CleanupOrchestrator cleanup)
    {
        Entry = entry;
        _cleanup = cleanup;
    }

    partial void OnSelectedActionChanged(FileAction value)
    {
        OnPropertyChanged(nameof(IsKeep));
        OnPropertyChanged(nameof(IsMoveToFolder));
        OnPropertyChanged(nameof(IsQuarantine));
        OnPropertyChanged(nameof(IsDelete));
    }

    [RelayCommand]
    private void SetActionKeep()       => SelectedAction = FileAction.Keep;

    [RelayCommand]
    private void SetActionMove()       => SelectedAction = FileAction.MoveToFolder;

    [RelayCommand]
    private void SetActionQuarantine() => SelectedAction = FileAction.Quarantine;

    [RelayCommand]
    private void SetActionDelete()     => SelectedAction = FileAction.Delete;

    public async Task ApplyActionAsync(string quarantinePath, string moveToPath, CancellationToken ct)
    {
        if (SelectedAction == FileAction.Keep || IsDone) return;

        IsProcessing = true;
        try
        {
            switch (SelectedAction)
            {
                case FileAction.Delete:
                    await _cleanup.DeleteAsync([Entry], null, ct);
                    break;
                case FileAction.Quarantine:
                    await _cleanup.MoveToFolderAsync([Entry], quarantinePath, null, ct);
                    break;
                case FileAction.MoveToFolder:
                    await _cleanup.MoveToFolderAsync([Entry], moveToPath, null, ct);
                    break;
            }
            IsDone = true;
            StatusMessage = "Done";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
