using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DupeFinderPro.Application;
using DupeFinderPro.Domain.Models;
using System.Collections.ObjectModel;

namespace DupeFinderPro.ViewModels.Duplicate;

public sealed partial class DuplicateScanViewModel : ViewModelBase
{
    private readonly ScanJobService  _scanJobService;
    private readonly ResultsViewModel _resultsVm;

    public event Action? ScanStarted;

    // ── Paths ────────────────────────────────────────────────────────────
    public ObservableCollection<string> IncludePaths { get; } = [];
    public ObservableCollection<string> ExcludePaths { get; } = [];

    // ── File Types ────────────────────────────────────────────────────────
    public ObservableCollection<FileTypeCategoryViewModel> FileTypeCategories { get; } =
    [
        new(FileTypeCategory.Documents),
        new(FileTypeCategory.Images),
        new(FileTypeCategory.Videos),
        new(FileTypeCategory.Audio),
        new(FileTypeCategory.Archives),
        new(FileTypeCategory.Installers),
        new(FileTypeCategory.Other),
    ];

    // ── Extension overrides ──────────────────────────────────────────────
    [ObservableProperty] private string _includeExtensionsRaw = string.Empty;
    [ObservableProperty] private string _excludeExtensionsRaw = string.Empty;

    // ── Keywords ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _includeKeywordsRaw = string.Empty;
    [ObservableProperty] private string _excludeKeywordsRaw = string.Empty;

    // ── Size filter ───────────────────────────────────────────────────────
    [ObservableProperty] private long  _minSizeKb;
    [ObservableProperty] private long? _maxSizeKb;
    [ObservableProperty] private bool  _hasMaxSize;

    // ── Date filters ──────────────────────────────────────────────────────
    [ObservableProperty] private DateTime? _createdFrom;
    [ObservableProperty] private DateTime? _createdTo;
    [ObservableProperty] private DateTime? _modifiedFrom;
    [ObservableProperty] private DateTime? _modifiedTo;

    // ── Safety ────────────────────────────────────────────────────────────
    [ObservableProperty] private bool _excludeSystemFiles = true;
    [ObservableProperty] private bool _recursive          = true;

    // ── Job meta ─────────────────────────────────────────────────────────
    [ObservableProperty] private string _scanName = $"스캔 {DateTime.Now:yyyy-MM-dd HH:mm}";

    // ── Path text input (batch) ───────────────────────────────────────────
    [ObservableProperty] private string _includePathInput = string.Empty;
    [ObservableProperty] private string _excludePathInput = string.Empty;

    // ── Validation ────────────────────────────────────────────────────────
    [ObservableProperty] private string _validationError = string.Empty;
    [ObservableProperty] private bool   _hasValidationError;

    public DuplicateScanViewModel(ScanJobService scanJobService, ResultsViewModel resultsVm)
    {
        _scanJobService = scanJobService;
        _resultsVm      = resultsVm;
    }

    // ── Path commands ─────────────────────────────────────────────────────
    [RelayCommand]
    private void AddIncludePath(string path)
    {
        path = path.Trim();
        if (!string.IsNullOrEmpty(path) && !IncludePaths.Contains(path))
            IncludePaths.Add(path);
    }

    [RelayCommand]
    private void RemoveIncludePath(string path) => IncludePaths.Remove(path);

    [RelayCommand]
    private void AddExcludePath(string path)
    {
        path = path.Trim();
        if (!string.IsNullOrEmpty(path) && !ExcludePaths.Contains(path))
            ExcludePaths.Add(path);
    }

    [RelayCommand]
    private void RemoveExcludePath(string path) => ExcludePaths.Remove(path);

    [RelayCommand]
    private void AddIncludePathsFromInput()
    {
        foreach (var path in SplitPaths(IncludePathInput))
            AddIncludePath(path);
        IncludePathInput = string.Empty;
    }

    [RelayCommand]
    private void AddExcludePathsFromInput()
    {
        foreach (var path in SplitPaths(ExcludePathInput))
            AddExcludePath(path);
        ExcludePathInput = string.Empty;
    }

    private static IEnumerable<string> SplitPaths(string raw) =>
        raw.Split(['\n', '\r', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // ── File-type commands ────────────────────────────────────────────────
    [RelayCommand]
    private void SelectAllFileTypes()
    {
        foreach (var ft in FileTypeCategories) ft.IsSelected = true;
    }

    [RelayCommand]
    private void ClearAllFileTypes()
    {
        foreach (var ft in FileTypeCategories) ft.IsSelected = false;
    }

    // ── Scan ──────────────────────────────────────────────────────────────
    [RelayCommand]
    private void StartScan()
    {
        if (!Validate()) return;

        var filter = BuildFilter();
        var name   = string.IsNullOrWhiteSpace(ScanName)
            ? $"스캔 {DateTime.Now:yyyy-MM-dd HH:mm}"
            : ScanName.Trim();

        var job = _scanJobService.CreateJob(name, filter);
        _resultsVm.StartScan(job);
        ScanStarted?.Invoke();

        ResetForm();
    }

    // ── Helpers ───────────────────────────────────────────────────────────
    private bool Validate()
    {
        if (IncludePaths.Count == 0)
        {
            ValidationError    = "검색할 폴더를 최소 하나 이상 추가해야 합니다.";
            HasValidationError = true;
            return false;
        }
        HasValidationError = false;
        ValidationError    = string.Empty;
        return true;
    }

    private ScanFilter BuildFilter()
    {
        var selectedTypes = FileTypeCategories
            .Where(ft => ft.IsSelected)
            .Select(ft => ft.Category)
            .ToHashSet();

        return new ScanFilter(
            IncludePaths:      [.. IncludePaths],
            ExcludePaths:      [.. ExcludePaths],
            FileTypes:         selectedTypes,
            IncludeExtensions: SplitRaw(IncludeExtensionsRaw),
            ExcludeExtensions: SplitRaw(ExcludeExtensionsRaw),
            IncludeKeywords:   SplitRaw(IncludeKeywordsRaw),
            ExcludeKeywords:   SplitRaw(ExcludeKeywordsRaw),
            MinSizeBytes:      MinSizeKb * 1024,
            MaxSizeBytes:      HasMaxSize && MaxSizeKb.HasValue ? MaxSizeKb.Value * 1024 : null,
            CreatedFrom:       CreatedFrom,
            CreatedTo:         CreatedTo,
            ModifiedFrom:      ModifiedFrom,
            ModifiedTo:        ModifiedTo,
            ExcludeSystemFiles: ExcludeSystemFiles,
            Recursive:         Recursive);
    }

    private void ResetForm()
    {
        ScanName = $"스캔 {DateTime.Now:yyyy-MM-dd HH:mm}";
        IncludePaths.Clear();
        ExcludePaths.Clear();
        IncludePathInput     = string.Empty;
        ExcludePathInput     = string.Empty;
        IncludeExtensionsRaw = string.Empty;
        ExcludeExtensionsRaw = string.Empty;
        IncludeKeywordsRaw   = string.Empty;
        ExcludeKeywordsRaw   = string.Empty;
        MinSizeKb            = 0;
        MaxSizeKb            = null;
        HasMaxSize           = false;
        CreatedFrom          = null;
        CreatedTo            = null;
        ModifiedFrom         = null;
        ModifiedTo           = null;
        ExcludeSystemFiles   = true;
        Recursive            = true;
        foreach (var ft in FileTypeCategories) ft.IsSelected = false;
    }

    private static IReadOnlyList<string> SplitRaw(string raw) =>
        raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
           .ToList()
           .AsReadOnly();
}
