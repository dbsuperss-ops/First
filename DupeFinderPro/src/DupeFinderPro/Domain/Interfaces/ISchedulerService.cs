using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Domain.Interfaces;

public interface ISchedulerService
{
    bool RegisterTask(Scenario scenario);
    bool DeleteTask(string scenarioName);
}
