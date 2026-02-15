using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Repositories;

public interface IDiseaseVectorRepository
{
    Task<IReadOnlyList<string>> GetAllSymptomNamesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetSymptomToIndexMapAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiseaseVectorRecord>> GetAllDiseaseVectorsAsync(CancellationToken cancellationToken = default);
}
