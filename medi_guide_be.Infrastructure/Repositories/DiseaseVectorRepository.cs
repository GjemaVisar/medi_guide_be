using medi_guide_be.Domain.Entities;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Data;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using System.Diagnostics;

namespace medi_guide_be.Infrastructure.Repositories;

public class DiseaseVectorRepository : IDiseaseVectorRepository
{
    private const string DiseasesField = "diseases";
    private const string IdField = "_id";
    private const string CacheCollectionName = "vector_cache";
    private const string MetaDocumentId = "symptom_meta";

    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly IMongoCollection<BsonDocument> _vectorCache;
    private readonly ILogger<DiseaseVectorRepository> _logger;

    private string[]? _symptomKeys;

    public DiseaseVectorRepository(MongoDbContext context, ILogger<DiseaseVectorRepository> logger)
    {
        _collection = context.GetCollection<BsonDocument>("diseases");
        _vectorCache = context.GetCollection<BsonDocument>(CacheCollectionName);
        _logger = logger;
    }

    // ───────────────────────── public read methods ─────────────────────────

    public async Task<IReadOnlyList<string>> GetAllSymptomNamesAsync(CancellationToken cancellationToken = default)
    {
        return await GetSymptomKeysAsync(cancellationToken);
    }

    public async Task<IReadOnlyDictionary<string, int>> GetSymptomToIndexMapAsync(CancellationToken cancellationToken = default)
    {
        var keys = await GetSymptomKeysAsync(cancellationToken);
        var map = new Dictionary<string, int>(keys.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < keys.Length; i++)
            map[keys[i].Trim().ToLowerInvariant()] = i;
        return map;
    }

    public async Task<IReadOnlyList<DiseaseVectorRecord>> GetAllDiseaseVectorsAsync(CancellationToken cancellationToken = default)
    {
        // Fast path: try compact cache collection first
        var cached = await TryLoadFromCacheAsync(cancellationToken);
        if (cached is not null)
            return cached;

        // Slow path: build vectors from the wide "diseases" collection
        _logger.LogWarning(
            "vector_cache collection is empty — falling back to the slow raw-collection path. " +
            "Call POST /api/admin/rebuild-vectors to pre-compute the cache.");
        return await BuildVectorsFromRawCollectionAsync(cancellationToken);
    }

    // ───────────────────── rebuild (admin trigger) ─────────────────────

    public async Task<int> RebuildVectorCacheAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        _logger.LogInformation("Rebuilding vector cache from raw diseases collection...");

        // 1. Build vectors from raw collection
        var keys = await GetRawSymptomKeysAsync(cancellationToken);
        var vectors = await BuildVectorsFromRawCollectionAsync(cancellationToken);

        _logger.LogInformation("Built {Count} vectors in {Elapsed}s. Writing to cache collection...",
            vectors.Count, sw.Elapsed.TotalSeconds.ToString("F1"));

        // 2. Drop old cache and write compact documents
        await _vectorCache.DeleteManyAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken);

        // 2a. Write the metadata document (symptom key order)
        var metaDoc = new BsonDocument
        {
            { IdField, MetaDocumentId },
            { "keys", new BsonArray(keys) },
            { "rebuiltAt", DateTime.UtcNow }
        };
        await _vectorCache.InsertOneAsync(metaDoc, cancellationToken: cancellationToken);

        // 2b. Write vector documents in batches (ordered:false for max throughput)
        const int batchSize = 2000;
        var totalWritten = 0;

        for (var i = 0; i < vectors.Count; i += batchSize)
        {
            var batch = new List<BsonDocument>(Math.Min(batchSize, vectors.Count - i));
            for (var j = i; j < Math.Min(i + batchSize, vectors.Count); j++)
            {
                var v = vectors[j];
                batch.Add(new BsonDocument
                {
                    { "diseaseId", v.Id },
                    { "name", v.Name },
                    { "vector", new BsonBinaryData(v.Vector) },
                    { "magnitude", v.Magnitude }
                });
            }

            await _vectorCache.InsertManyAsync(batch,
                new InsertManyOptions { IsOrdered = false },
                cancellationToken);

            totalWritten += batch.Count;

            if (totalWritten % 10000 == 0 || totalWritten == vectors.Count)
                _logger.LogInformation("  wrote {Written}/{Total} vector docs...", totalWritten, vectors.Count);
        }

        sw.Stop();
        _logger.LogInformation(
            "Vector cache rebuild complete. {Count} vectors written in {Elapsed}s.",
            totalWritten, sw.Elapsed.TotalSeconds.ToString("F1"));

        // Reset in-memory key cache so next read picks up the fresh data
        _symptomKeys = keys;

        return totalWritten;
    }

    // ──────────────────── fast path: read from cache ────────────────────

    private async Task<IReadOnlyList<DiseaseVectorRecord>?> TryLoadFromCacheAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();

        // Check if cache collection has any vector documents
        var estimatedCount = await _vectorCache.EstimatedDocumentCountAsync(new EstimatedDocumentCountOptions(), cancellationToken);
        if (estimatedCount <= 1) // 0 = empty, 1 = only metadata doc
            return null;

        _logger.LogInformation("Loading vectors from compact cache collection (~{Count} docs)...", estimatedCount);

        // Read all vector documents (exclude the metadata doc)
        var filter = Builders<BsonDocument>.Filter.Ne(IdField, MetaDocumentId);
        var findOptions = new FindOptions<BsonDocument> { BatchSize = 5000 };
        var cursor = await _vectorCache.FindAsync(filter, findOptions, cancellationToken);

        var results = new List<DiseaseVectorRecord>((int)estimatedCount);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var doc in cursor.Current)
            {
                var id = doc.GetValue("diseaseId", "").AsString;
                var name = doc.GetValue("name", "").AsString;
                var vector = doc["vector"].AsByteArray;
                var magnitude = doc["magnitude"].AsDouble;

                results.Add(new DiseaseVectorRecord(id, name, vector, magnitude));
            }
        }

        sw.Stop();
        _logger.LogInformation(
            "Loaded {Count} vectors from cache in {Elapsed}ms (fast path).",
            results.Count, sw.ElapsedMilliseconds);

        return results.Count > 0 ? results : null;
    }

    // ──────────────── slow path: build from raw collection ────────────────

    private async Task<IReadOnlyList<DiseaseVectorRecord>> BuildVectorsFromRawCollectionAsync(CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        var keys = await GetSymptomKeysAsync(cancellationToken);
        var vectorLength = keys.Length;

        var cursor = await _collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync(cancellationToken);
        var results = new List<DiseaseVectorRecord>(4096);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var doc in cursor.Current)
            {
                var id = doc[IdField].ToString() ?? string.Empty;
                var name = doc.Contains(DiseasesField) ? doc[DiseasesField].AsString : string.Empty;

                var vector = new byte[vectorLength];
                var ones = 0;
                for (var i = 0; i < vectorLength; i++)
                {
                    if (doc.TryGetValue(keys[i], out var elem))
                    {
                        var val = elem.BsonType == BsonType.Int32 ? elem.AsInt32
                                : elem.BsonType == BsonType.Double ? (int)elem.AsDouble
                                : 0;
                        if (val != 0)
                        {
                            vector[i] = 1;
                            ones++;
                        }
                    }
                }

                var magnitude = ones > 0 ? Math.Sqrt(ones) : 0;
                results.Add(new DiseaseVectorRecord(id, name, vector, magnitude));
            }
        }

        sw.Stop();
        _logger.LogInformation(
            "Built {Count} vectors from raw collection in {Elapsed}s (slow path).",
            results.Count, sw.Elapsed.TotalSeconds.ToString("F1"));

        return results;
    }

    // ────────────────────── symptom keys helpers ──────────────────────

    private async Task<string[]> GetSymptomKeysAsync(CancellationToken cancellationToken)
    {
        if (_symptomKeys is not null)
            return _symptomKeys;

        // Try reading from the cache metadata first
        var cachedKeys = await TryLoadSymptomKeysFromCacheAsync(cancellationToken);
        if (cachedKeys is not null)
        {
            _symptomKeys = cachedKeys;
            return _symptomKeys;
        }

        // Fall back to sampling the raw collection
        return await GetRawSymptomKeysAsync(cancellationToken);
    }

    private async Task<string[]?> TryLoadSymptomKeysFromCacheAsync(CancellationToken cancellationToken)
    {
        var filter = Builders<BsonDocument>.Filter.Eq(IdField, MetaDocumentId);
        var metaDoc = await _vectorCache.Find(filter).FirstOrDefaultAsync(cancellationToken);

        if (metaDoc is null || !metaDoc.Contains("keys"))
            return null;

        var keys = metaDoc["keys"].AsBsonArray
            .Select(b => b.AsString)
            .ToArray();

        _logger.LogInformation("Loaded {Count} symptom keys from cache metadata.", keys.Length);
        return keys.Length > 0 ? keys : null;
    }

    private async Task<string[]> GetRawSymptomKeysAsync(CancellationToken cancellationToken)
    {
        if (_symptomKeys is not null)
            return _symptomKeys;

        var sample = await _collection
            .Find(FilterDefinition<BsonDocument>.Empty)
            .FirstOrDefaultAsync(cancellationToken);

        _symptomKeys = sample?.Names
            .Where(n => n != IdField && n != DiseasesField)
            .ToArray() ?? [];

        return _symptomKeys;
    }
}
