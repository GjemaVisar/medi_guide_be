using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Repositories;

public interface IDiseaseVectorRepository
{
    Task<IReadOnlyList<string>> GetAllSymptomNamesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<string, int>> GetSymptomToIndexMapAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DiseaseVectorRecord>> GetAllDiseaseVectorsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Reads every document from the raw "diseases" collection, builds binary
    /// vectors, and stores them in a compact "vector_cache" collection.
    /// After this runs once, cold-start loads read from the cache collection
    /// (seconds) instead of rebuilding from the wide collection (minutes).
    /// </summary>
    Task<int> RebuildVectorCacheAsync(CancellationToken cancellationToken = default);
}
