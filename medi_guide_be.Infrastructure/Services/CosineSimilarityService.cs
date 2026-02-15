using System.Collections.Concurrent;
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

    private readonly IDiseaseVectorRepository _diseaseVectorRepository;
    private readonly IMemoryCache _cache;

    public CosineSimilarityService(IDiseaseVectorRepository diseaseVectorRepository, IMemoryCache cache)
    {
        _diseaseVectorRepository = diseaseVectorRepository;
        _cache = cache;
    }

    public async Task<IReadOnlyList<DiseaseMatch>> GetTopMatchesAsync(
        IReadOnlyList<string> selectedSymptomKeys,
        int topN = 10,
        CancellationToken cancellationToken = default)
    {
        var indexMap = await GetCachedSymptomIndexAsync(cancellationToken);
        var userIndices = selectedSymptomKeys
            .Select(k => k.Trim().ToLowerInvariant())
            .Where(k => indexMap.ContainsKey(k))
            .Select(k => indexMap[k])
            .Distinct()
            .ToArray();

        if (userIndices.Length == 0)
            return Array.Empty<DiseaseMatch>();

        var userMagnitude = Math.Sqrt(userIndices.Length);
        var diseases = await GetCachedDiseaseVectorsAsync(cancellationToken);

        if (diseases.Count == 0)
            return Array.Empty<DiseaseMatch>();

        var results = new DiseaseMatch[diseases.Count];

        Parallel.ForEach(
            Partitioner.Create(0, diseases.Count),
            new ParallelOptions { CancellationToken = cancellationToken },
            range =>
            {
                for (var i = range.Item1; i < range.Item2; i++)
                {
                    var d = diseases[i];
                    var dot = 0;
                    foreach (var idx in userIndices)
                        dot += d.Vector[idx];
                    var sim = userMagnitude > 0 && d.Magnitude > 0
                        ? dot / (userMagnitude * d.Magnitude)
                        : 0d;
                    results[i] = new DiseaseMatch(d.Id, d.Name, sim);
                }
            });

        // Deduplicate by disease name: keep best score per disease so we return distinct diseases
        return results
            .GroupBy(r => r.DiseaseName, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.OrderByDescending(x => x.SimilarityScore).First())
            .OrderByDescending(r => r.SimilarityScore)
            .Take(topN)
            .ToList();
    }

    private async Task<IReadOnlyDictionary<string, int>> GetCachedSymptomIndexAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(IndexCacheKey, out IReadOnlyDictionary<string, int>? cached))
            return cached!;
        var map = await _diseaseVectorRepository.GetSymptomToIndexMapAsync(cancellationToken);
        _cache.Set(IndexCacheKey, map, CacheExpiration);
        return map;
    }

    private async Task<IReadOnlyList<DiseaseVectorRecord>> GetCachedDiseaseVectorsAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(VectorsCacheKey, out IReadOnlyList<DiseaseVectorRecord>? cached))
            return cached!;
        var list = await _diseaseVectorRepository.GetAllDiseaseVectorsAsync(cancellationToken);
        _cache.Set(VectorsCacheKey, list, CacheExpiration);
        return list;
    }
}
