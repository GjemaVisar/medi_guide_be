using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Repositories;

public interface IDiseaseVectorRepository
{
    Task<IReadOnlyDictionary<string, int>> GetSymptomToIndexMapAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiseaseVectorRecord>> GetAllDiseaseVectorsAsync(CancellationToken cancellationToken = default);
}
