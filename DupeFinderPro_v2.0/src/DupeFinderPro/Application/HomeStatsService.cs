using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Application;

public sealed class HomeStatsService(
    IScenarioRepository scenarioRepo,
    IClassifyRecordRepository classifyRepo,
    ScanJobService scanJobService)
{
    public HomeStats GetStats()
    {
        var scenarios = scenarioRepo.GetAll();
        var records   = classifyRepo.GetAll();
        var jobs      = scanJobService.GetAllJobs();
        var completed = jobs.Where(j => j.Status == ScanJobStatus.Completed).ToList();

        return new HomeStats(
            ActiveScenarioCount:      scenarios.Count(s => s.IsActive),
            TotalScenariosCount:      scenarios.Count,
            TotalFilesOrganized:      records.Sum(r => r.FileCount),
            TotalBytesOrganized:      records.Sum(r => r.TotalBytes),
            LastOrganizeTime:         records.Count > 0
                                          ? records.Max(r => r.ExecutedAt).ToString("g")
                                          : "없음",
            TotalDuplicatesFound:     completed.Sum(j => j.Result?.DuplicateGroups.Count ?? 0),
            TotalWastedBytes:         completed.Sum(j => j.Result?.TotalWastedBytes ?? 0),
            TotalScansRun:            jobs.Count,
            LastScanTime:             jobs.Count > 0
                                          ? jobs.Max(j => j.CreatedAt).ToString("g")
                                          : "없음");
    }
}
