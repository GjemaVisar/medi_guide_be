using medi_guide_be.Domain.Entities;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace medi_guide_be.Infrastructure.Repositories;

public class DiseaseVectorRepository : IDiseaseVectorRepository
{
    private const string MetadataId = "metadata";
    private readonly IMongoCollection<BsonDocument> _collection;

    private string[]? _symptomKeys;

    public DiseaseVectorRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<BsonDocument>("precomputed_vectors");
    }

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
        var keys = await GetSymptomKeysAsync(cancellationToken);
        var vectorLength = keys.Length;

        var filter = Builders<BsonDocument>.Filter.Ne("_id", MetadataId);
        var projection = Builders<BsonDocument>.Projection
            .Include("disease_name")
            .Include("active_indices")
            .Include("magnitude");

        var cursor = await _collection
            .Find(filter)
            .Project(projection)
            .ToCursorAsync(cancellationToken);

        var results = new List<DiseaseVectorRecord>(4096);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var doc in cursor.Current)
            {
                var id = doc["_id"].ToString()!;
                var name = doc.Contains("disease_name") ? doc["disease_name"].AsString : string.Empty;
                var magnitude = doc.Contains("magnitude") ? doc["magnitude"].ToDouble() : 0;

                var vector = new byte[vectorLength];
                if (doc.Contains("active_indices"))
                {
                    foreach (var idx in doc["active_indices"].AsBsonArray)
                        vector[idx.AsInt32] = 1;
                }

                results.Add(new DiseaseVectorRecord(id, name, vector, magnitude));
            }
        }

        return results;
    }

    private async Task<string[]> GetSymptomKeysAsync(CancellationToken cancellationToken)
    {
        if (_symptomKeys is not null)
            return _symptomKeys;

        var filter = Builders<BsonDocument>.Filter.Eq("_id", MetadataId);
        var metadataDoc = await _collection
            .Find(filter)
            .FirstOrDefaultAsync(cancellationToken);

        _symptomKeys = metadataDoc?["symptom_keys"]
            .AsBsonArray
            .Select(v => v.AsString)
            .ToArray() ?? [];

        return _symptomKeys;
    }
}
