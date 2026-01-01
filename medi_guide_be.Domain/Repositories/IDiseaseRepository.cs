using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Repositories;

public interface IDiseaseRepository
{
    Task<List<string>> GetAllDiseaseNamesAsync();
}

