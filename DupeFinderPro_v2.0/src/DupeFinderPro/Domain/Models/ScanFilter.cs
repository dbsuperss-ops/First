namespace DupeFinderPro.Domain.Models;

public sealed record ScanFilter(
    IReadOnlyList<string> IncludePaths,
    IReadOnlyList<string> ExcludePaths,
    IReadOnlySet<FileTypeCategory> FileTypes,
    IReadOnlyList<string> IncludeExtensions,
    IReadOnlyList<string> ExcludeExtensions,
    IReadOnlyList<string> IncludeKeywords,
    IReadOnlyList<string> ExcludeKeywords,
    long MinSizeBytes,
    long? MaxSizeBytes,
    DateTime? CreatedFrom,
    DateTime? CreatedTo,
    DateTime? ModifiedFrom,
    DateTime? ModifiedTo,
    bool ExcludeSystemFiles,
    bool Recursive)
{
    public static readonly ScanFilter Empty = new(
        IncludePaths: [],
        ExcludePaths: [],
        FileTypes: new HashSet<FileTypeCategory>(),
        IncludeExtensions: [],
        ExcludeExtensions: [],
        IncludeKeywords: [],
        ExcludeKeywords: [],
        MinSizeBytes: 0,
        MaxSizeBytes: null,
        CreatedFrom: null,
        CreatedTo: null,
        ModifiedFrom: null,
        ModifiedTo: null,
        ExcludeSystemFiles: true,
        Recursive: true);

    public IReadOnlySet<string> GetAllowedExtensions()
    {
        var exts = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var cat in FileTypes)
        {
            foreach (var ext in FileTypeCategoryExtensions.GetExtensions(cat))
                exts.Add(ext);
        }

        foreach (var ext in IncludeExtensions)
            exts.Add(ext.StartsWith('.') ? ext : "." + ext);

        return exts;
    }

    public bool HasFileTypeFilter => FileTypes.Count > 0 || IncludeExtensions.Count > 0;
}
