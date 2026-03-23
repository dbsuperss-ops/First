using DupeFinderPro.Domain.Models.Organize;

namespace DupeFinderPro.Domain.Interfaces;

public interface IScenarioRepository
{
    IReadOnlyList<Scenario> GetAll();
    bool Save(IReadOnlyList<Scenario> scenarios);
    Scenario? GetById(Guid id);
}
