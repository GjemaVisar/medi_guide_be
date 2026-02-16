using medi_guide_be.Domain.Entities;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Data;
using MongoDB.Bson;
using MongoDB.Driver;

namespace medi_guide_be.Infrastructure.Repositories;

public class DiseaseVectorRepository : IDiseaseVectorRepository
{
    private const string DiseasesField = "diseases";
    private const string IdField = "_id";
    private readonly IMongoCollection<BsonDocument> _collection;

    // Cache symptom keys in memory so every method reuses the same array
    // and we only query MongoDB for them once per repository instance.
    private string[]? _symptomKeys;

    public DiseaseVectorRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<BsonDocument>("diseases");
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

        var cursor = await _collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync(cancellationToken);
        var results = new List<DiseaseVectorRecord>(4096);

        while (await cursor.MoveNextAsync(cancellationToken))
        {
            foreach (var doc in cursor.Current)
            {
                var id = doc[IdField].ToString();
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

        return results;
    }

    private async Task<string[]> GetSymptomKeysAsync(CancellationToken cancellationToken)
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
