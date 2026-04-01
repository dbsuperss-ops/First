using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.ViewModels;

namespace DupeFinderPro.ViewModels.Organize;

public sealed partial class OrganizeLogViewModel : ViewModelBase
{
    private readonly IOrganizeLogRepository _repo;

    public ObservableCollection<OrganizeLogEntryViewModel> Entries { get; } = [];

    [ObservableProperty] private string _statusText = string.Empty;
    [ObservableProperty] private bool _hasEntries;

    public OrganizeLogViewModel(IOrganizeLogRepository repo)
    {
        _repo = repo;
        LoadEntries();
    }

    public void Refresh() => LoadEntries();

    [RelayCommand]
    private void UndoLast()
    {
        var batchId = _repo.GetLastBatchId();
        if (batchId == Guid.Empty)
        {
            StatusText = "되돌릴 작업이 없습니다.";
            return;
        }

        var (success, fail) = _repo.UndoBatch(batchId);
        StatusText = $"되돌리기 완료: 성공 {success}개, 실패 {fail}개";
        LoadEntries();
    }

    [RelayCommand]
    private void ClearLog()
    {
        _repo.Clear();
        LoadEntries();
        StatusText = "로그가 삭제되었습니다.";
    }

    private void LoadEntries()
    {
        Entries.Clear();
        foreach (var entry in _repo.GetAll().OrderByDescending(e => e.Timestamp))
            Entries.Add(new OrganizeLogEntryViewModel(entry));
        HasEntries = Entries.Count > 0;
    }
}
