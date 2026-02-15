using medi_guide_be.Domain.Entities;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Data;
using Microsoft.Extensions.Configuration;
using MongoDB.Driver;

namespace medi_guide_be.Infrastructure.Repositories;

public class KosovoHospitalRepository : IKosovoHospitalRepository
{
    private readonly IMongoCollection<KosovoHospitalDocument> _collection;

    public KosovoHospitalRepository(KosovoHospitalsDbContext context, IConfiguration configuration)
    {
        var collectionName = configuration["MongoDB:KosovoHospitals:CollectionName"] ?? "hospitals";
        _collection = context.GetCollection<KosovoHospitalDocument>(collectionName);
    }

    public async Task<IReadOnlyList<KosovoHospital>> GetByCityAsync(string city, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(city))
            return Array.Empty<KosovoHospital>();

        // Match city name in either location OR description (case-insensitive)
        var cityRegex = new MongoDB.Bson.BsonRegularExpression(city.Trim(), "i");

        var locationFilter = Builders<KosovoHospitalDocument>.Filter.Regex(x => x.Location, cityRegex);
        var descriptionFilter = Builders<KosovoHospitalDocument>.Filter.Regex(x => x.Description, cityRegex);
        var filter = Builders<KosovoHospitalDocument>.Filter.Or(locationFilter, descriptionFilter);

        var cursor = await _collection.FindAsync(filter, cancellationToken: cancellationToken);
        var documents = await cursor.ToListAsync(cancellationToken);

        return documents.Select(MapToEntity).ToList();
    }

    private static KosovoHospital MapToEntity(KosovoHospitalDocument doc)
    {
        return new KosovoHospital
        {
            Id = doc.Id,
            Name = doc.Name ?? string.Empty,
            Location = doc.Location ?? string.Empty,
            Description = doc.Description ?? string.Empty,
            Link = doc.Link ?? string.Empty,
            Image = doc.Image ?? string.Empty,
            PhoneNumbers = doc.PhoneNumbers ?? new List<string>(),
            Website = doc.Website
        };
    }
}
