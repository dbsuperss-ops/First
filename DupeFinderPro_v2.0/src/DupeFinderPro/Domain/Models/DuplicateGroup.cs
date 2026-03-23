namespace DupeFinderPro.Domain.Models;

public sealed class DuplicateGroup
{
    public string Hash { get; }
    public IReadOnlyList<FileEntry> Files { get; }
    public long SizeBytes { get; }
    public long WastedBytes { get; }
    public FileEntry? SuggestedKeep { get; init; }

    public DuplicateGroup(string hash, IReadOnlyList<FileEntry> files)
    {
        Hash = hash;
        Files = files;
        SizeBytes = files.FirstOrDefault()?.SizeBytes ?? 0;
        WastedBytes = SizeBytes * (files.Count - 1);
    }
}
