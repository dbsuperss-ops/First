using DupeFinderPro.Domain.Interfaces;
using DupeFinderPro.Domain.Models;

namespace DupeFinderPro.Application;

public sealed class ScanJobService
{
    private readonly IScanJobRepository _repository;
    private readonly ScanOrchestrator _scanOrchestrator;

    public ScanJobService(IScanJobRepository repository, ScanOrchestrator scanOrchestrator)
    {
        _repository = repository;
        _scanOrchestrator = scanOrchestrator;
    }

    public ScanJob CreateJob(string name, ScanFilter filter)
    {
        var job = new ScanJob(name, filter);
        _repository.Add(job);
        return job;
    }

    public async Task RunJobAsync(
        ScanJob job,
        IProgress<ScanProgress> progress,
        CancellationToken ct = default)
    {
        job.Status = ScanJobStatus.Running;
        _repository.Update(job);

        try
        {
            var result = await _scanOrchestrator.RunAsync(job.Filter, progress, ct);
            job.Result = result;
            job.Status = ScanJobStatus.Completed;
        }
        catch (OperationCanceledException)
        {
            job.Status = ScanJobStatus.Cancelled;
        }
        catch (Exception ex)
        {
            job.Status = ScanJobStatus.Failed;
            job.ErrorMessage = ex.Message;
        }
        finally
        {
            _repository.Update(job);
        }
    }

    public IReadOnlyList<ScanJob> GetAllJobs() => _repository.GetAll();
    public ScanJob? GetLatestCompleted() => _repository.GetLatestCompleted();
}
