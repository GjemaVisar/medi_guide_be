using medi_guide_be.Domain.Entities;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Domain.Services;
using Microsoft.Extensions.Caching.Memory;

namespace medi_guide_be.Infrastructure.Services;

public class CosineSimilarityService : IDiseaseSimilarityService
{
    private const string VectorsCacheKey = "disease_vectors";
    private const string IndexCacheKey = "symptom_index";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24);

    private static readonly SemaphoreSlim _vectorsLock = new(1, 1);
    private static readonly SemaphoreSlim _indexLock = new(1, 1);

    private readonly IDiseaseVectorRepository _diseaseVectorRepository;
    private readonly IMemoryCache _cache;

    public CosineSimilarityService(IDiseaseVectorRepository diseaseVectorRepository, IMemoryCache cache)
    {
        _diseaseVectorRepository = diseaseVectorRepository;
        _cache = cache;
    }

    public async Task WarmUpAsync(CancellationToken cancellationToken = default)
    {
        await GetCachedSymptomIndexAsync(cancellationToken);
        await GetCachedDiseaseVectorsAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DiseaseMatch>> GetTopMatchesAsync(
        IReadOnlyList<string> selectedSymptomKeys,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        var indexMap = await GetCachedSymptomIndexAsync(cancellationToken);
        var userIndexSet = new HashSet<int>(
            selectedSymptomKeys
                .Select(k => k.Trim().ToLowerInvariant())
                .Where(k => indexMap.ContainsKey(k))
                .Select(k => indexMap[k]));

        if (userIndexSet.Count == 0)
            return Array.Empty<DiseaseMatch>();

        var userMagnitude = Math.Sqrt(userIndexSet.Count);
        var diseases = await GetCachedDiseaseVectorsAsync(cancellationToken);

        if (diseases.Count == 0)
            return Array.Empty<DiseaseMatch>();

        var scores = new DiseaseMatch[diseases.Count];

        Parallel.For(0, diseases.Count,
            new ParallelOptions
            {
                CancellationToken = cancellationToken,
                MaxDegreeOfParallelism = Math.Max(1, Environment.ProcessorCount)
            },
            i =>
            {
                var d = diseases[i];
                var dot = 0;
                foreach (var idx in d.ActiveIndices)
                {
                    if (userIndexSet.Contains(idx))
                        dot++;
                }

                var sim = dot > 0 && d.Magnitude > 0
                    ? dot / (userMagnitude * d.Magnitude)
                    : 0d;

                scores[i] = new DiseaseMatch(d.Id, d.Name, sim);
            });

        var best = new Dictionary<string, DiseaseMatch>(StringComparer.OrdinalIgnoreCase);
        foreach (var m in scores)
        {
            if (m.SimilarityScore <= 0)
                continue;

            if (!best.TryGetValue(m.DiseaseName, out var existing) ||
                m.SimilarityScore > existing.SimilarityScore)
            {
                best[m.DiseaseName] = m;
            }
        }

        return best.Values
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topN)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, int>> GetCachedSymptomIndexAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(IndexCacheKey, out IReadOnlyDictionary<string, int>? cached))
            return cached!;

        await _indexLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(IndexCacheKey, out cached))
                return cached!;

            var map = await _diseaseVectorRepository.GetSymptomToIndexMapAsync(cancellationToken);
            _cache.Set(IndexCacheKey, map, CacheExpiration);
            return map;
        }
        finally
        {
            _indexLock.Release();
        }
    }

    private async Task<IReadOnlyList<DiseaseVectorRecord>> GetCachedDiseaseVectorsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(VectorsCacheKey, out IReadOnlyList<DiseaseVectorRecord>? cached))
            return cached!;

        await _vectorsLock.WaitAsync(cancellationToken);
        try
        {
            if (_cache.TryGetValue(VectorsCacheKey, out cached))
                return cached!;

            var list = await _diseaseVectorRepository.GetAllDiseaseVectorsAsync(cancellationToken);
            _cache.Set(VectorsCacheKey, list, CacheExpiration);
            return list;
        }
        finally
        {
            _vectorsLock.Release();
        }
    }
}
