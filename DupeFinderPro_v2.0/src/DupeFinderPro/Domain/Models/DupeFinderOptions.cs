namespace DupeFinderPro.Domain.Models;

public sealed class DupeFinderOptions
{
    public const string SectionName = "DupeFinder";
    
    public int MaxDegreeOfParallelism { get; set; } = 0;
    public int PartialHashBytes { get; set; } = 4096;
}
