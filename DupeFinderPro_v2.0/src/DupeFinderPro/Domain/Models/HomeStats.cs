namespace DupeFinderPro.Domain.Models;

public sealed record HomeStats(
    int    ActiveScenarioCount,
    int    TotalScenariosCount,
    int    TotalFilesOrganized,
    long   TotalBytesOrganized,
    string LastOrganizeTime,
    int    TotalDuplicatesFound,
    long   TotalWastedBytes,
    int    TotalScansRun,
    string LastScanTime);
