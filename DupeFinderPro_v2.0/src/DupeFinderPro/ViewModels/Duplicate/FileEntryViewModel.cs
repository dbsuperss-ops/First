using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.ViewModels.Duplicate;

public sealed partial class FileEntryViewModel : ObservableObject
{
    private readonly Func<FileEntryViewModel, FileAction, Task> _runSingle;

    [ObservableProperty] private FileAction _selectedAction = FileAction.Keep;
    [ObservableProperty] private bool _isProcessing;
    [ObservableProperty] private bool _isDone;
    [ObservableProperty] private ActionOutcome _outcome;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private bool _isChecked = true;

    /// <summary>지금 파일이 있는 위치. 이동하면 새 위치로 바뀐다.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(FullPath))]
    private string _currentPath;

    /// <summary>이동해서 다른 폴더에 남아 있다. 같은 그룹 삭제의 남는 사본 후보가 된다.</summary>
    public bool IsMoved { get; private set; }

    // Injected by ResultsViewModel; updated whenever the paths change
    public string QuarantinePath { get; set; } = string.Empty;
    public string MoveToPath     { get; set; } = string.Empty;

    public FileEntry Entry { get; }

    public string FileName      => Entry.FileName;
    public string FullPath      => CurrentPath;
    public string SizeFormatted => FormatBytes(Entry.SizeBytes);
    public string LastModified  => Entry.LastModified.ToString("g");
    public string CreatedAt     => Entry.CreatedAt.ToString("g");

    public bool IsKeep         => SelectedAction == FileAction.Keep;
    public bool IsMoveToFolder => SelectedAction == FileAction.MoveToFolder;
    public bool IsQuarantine   => SelectedAction == FileAction.Quarantine;
    public bool IsDelete       => SelectedAction == FileAction.Delete;

    /// <param name="runSingle">이 파일 하나에 대한 작업을 그룹의 실행 경로로 보낸다(삭제 확인 포함).</param>
    public FileEntryViewModel(FileEntry entry, Func<FileEntryViewModel, FileAction, Task> runSingle)
    {
        Entry = entry;
        _currentPath = entry.FullPath;
        _runSingle = runSingle;
    }

    /// <summary>계획에 넣을 지금 상태의 파일 정보.</summary>
    public FileEntry Current => Entry with { FullPath = CurrentPath };

    partial void OnSelectedActionChanged(FileAction value)
    {
        OnPropertyChanged(nameof(IsKeep));
        OnPropertyChanged(nameof(IsMoveToFolder));
        OnPropertyChanged(nameof(IsQuarantine));
        OnPropertyChanged(nameof(IsDelete));
    }

    // ── Individual immediate-apply action commands ─────────────────────────
    /// <summary>"유지"는 선택만 바꾼다. 실행 완료로 잠그지 않으므로 나중에 다른 동작을 고를 수 있다.</summary>
    [RelayCommand]
    private void SetActionKeep()
    {
        if (IsDone || IsProcessing) return;
        SelectedAction = FileAction.Keep;
        Outcome = ActionOutcome.None;
        StatusMessage = "유지";
    }

    [RelayCommand]
    private Task SetActionMove() => _runSingle(this, FileAction.MoveToFolder);

    [RelayCommand]
    private Task SetActionQuarantine() => _runSingle(this, FileAction.Quarantine);

    [RelayCommand]
    private Task SetActionDelete() => _runSingle(this, FileAction.Delete);

    /// <summary>실행 결과를 표시한다. 성공만 완료로 잠근다.</summary>
    public void ShowResult(ActionResult result)
    {
        Outcome = result.Outcome;
        StatusMessage = result.Message;
        if (result.Outcome != ActionOutcome.Succeeded)
            return;

        IsDone = true;
        if (result.NewPath is { } path)
        {
            CurrentPath = path;
            IsMoved = SelectedAction == FileAction.MoveToFolder;
        }
    }

    // ── Preview helpers ────────────────────────────────────────────────────
    private static readonly HashSet<string> ImageExtensions =
        [".jpg", ".jpeg", ".png", ".bmp", ".gif", ".tiff", ".tif", ".webp", ".ico"];

    public static bool IsImageFile(string path) =>
        ImageExtensions.Contains(Path.GetExtension(path).ToLowerInvariant());

    private static string FormatBytes(long bytes)
    {
        if (bytes >= 1_073_741_824) return $"{bytes / 1_073_741_824.0:F1} GB";
        if (bytes >= 1_048_576)     return $"{bytes / 1_048_576.0:F1} MB";
        if (bytes >= 1_024)         return $"{bytes / 1_024.0:F1} KB";
        return $"{bytes} B";
    }
}
