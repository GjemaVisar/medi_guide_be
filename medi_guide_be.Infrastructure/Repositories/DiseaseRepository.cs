using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Data;
using Microsoft.Extensions.Caching.Memory;
using MongoDB.Bson;
using MongoDB.Driver;

namespace medi_guide_be.Infrastructure.Repositories;

public class DiseaseRepository : IDiseaseRepository
{
    private readonly IMongoCollection<BsonDocument> _collection;
    private readonly IMemoryCache _cache;
    private const string CacheKey = "all_disease_names";
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromHours(24); // Cache for 24 hours

    public DiseaseRepository(MongoDbContext context, IMemoryCache cache)
    {
        _collection = context.GetCollection<BsonDocument>("diseases");
        _cache = cache;
    }

    public async Task<List<string>> GetAllDiseaseNamesAsync()
    {
        // Try to get from cache first
        if (_cache.TryGetValue(CacheKey, out List<string>? cachedDiseases))
        {
            return cachedDiseases ?? new List<string>();
        }

        // Use aggregation pipeline for optimal performance
        var pipeline = new BsonDocument[]
        {
            // Project only the diseases field
            new BsonDocument("$project", new BsonDocument("diseases", 1)),
            // Group by diseases to get distinct values
            new BsonDocument("$group", new BsonDocument("_id", "$diseases")),
            // Filter out null/empty values
            new BsonDocument("$match", new BsonDocument("$and", new BsonArray
            {
                new BsonDocument("_id", new BsonDocument("$ne", BsonNull.Value)),
                new BsonDocument("_id", new BsonDocument("$ne", BsonString.Empty))
            })),
            // Sort alphabetically
            new BsonDocument("$sort", new BsonDocument("_id", 1))
        };

        var results = await _collection.Aggregate<BsonDocument>(pipeline).ToListAsync();
        
        var diseases = results
            .Select(doc => doc["_id"].AsString)
            .Where(d => !string.IsNullOrWhiteSpace(d))
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        // Cache the result
        _cache.Set(CacheKey, diseases, CacheExpiration);

        return diseases;
    }
}

