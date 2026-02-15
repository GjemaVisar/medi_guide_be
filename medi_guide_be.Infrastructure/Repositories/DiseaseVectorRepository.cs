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

    public DiseaseVectorRepository(MongoDbContext context)
    {
        _collection = context.GetCollection<BsonDocument>("diseases");
    }

    public async Task<IReadOnlyDictionary<string, int>> GetSymptomToIndexMapAsync(CancellationToken cancellationToken = default)
    {
        var orderedKeys = await GetOrderedSymptomKeysAsync(cancellationToken);
        var map = new Dictionary<string, int>(orderedKeys.Length, StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < orderedKeys.Length; i++)
            map[orderedKeys[i].Trim().ToLowerInvariant()] = i;
        return map;
    }

    public async Task<IReadOnlyList<DiseaseVectorRecord>> GetAllDiseaseVectorsAsync(CancellationToken cancellationToken = default)
    {
        var orderedKeys = await GetOrderedSymptomKeysAsync(cancellationToken);
        var vectorLength = orderedKeys.Length;

        var cursor = await _collection.Find(FilterDefinition<BsonDocument>.Empty).ToCursorAsync(cancellationToken);
        var results = new List<DiseaseVectorRecord>();

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
                    var key = orderedKeys[i];
                    var val = 0;
                    if (doc.Contains(key))
                    {
                        var elem = doc[key];
                        if (elem.BsonType == BsonType.Int32)
                            val = elem.AsInt32;
                        else if (elem.BsonType == BsonType.Double)
                            val = (int)elem.AsDouble;
                    }
                    vector[i] = (byte)(val != 0 ? 1 : 0);
                    if (vector[i] == 1) ones++;
                }
                var magnitude = ones > 0 ? Math.Sqrt(ones) : 0;
                results.Add(new DiseaseVectorRecord(id, name, vector, magnitude));
            }
        }

        return results;
    }

    private async Task<string[]> GetOrderedSymptomKeysAsync(CancellationToken cancellationToken)
    {
        var sample = await _collection
            .Find(FilterDefinition<BsonDocument>.Empty)
            .FirstOrDefaultAsync(cancellationToken);
        if (sample == null)
            return [];

        var keys = sample.Names
            .Where(n => n != IdField && n != DiseasesField)
            .OrderBy(n => n, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        return keys;
    }
}
