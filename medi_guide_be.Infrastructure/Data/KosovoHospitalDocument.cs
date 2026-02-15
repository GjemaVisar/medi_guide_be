using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace medi_guide_be.Infrastructure.Data;

[BsonIgnoreExtraElements]
public class KosovoHospitalDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("name")]
    public string Name { get; set; } = string.Empty;

    [BsonElement("location")]
    public string Location { get; set; } = string.Empty;

    [BsonElement("description")]
    public string Description { get; set; } = string.Empty;

    [BsonElement("link")]
    public string Link { get; set; } = string.Empty;

    [BsonElement("image")]
    public string Image { get; set; } = string.Empty;

    [BsonElement("phone_numbers")]
    public List<string> PhoneNumbers { get; set; } = [];

    [BsonElement("website")]
    [BsonIgnoreIfNull]
    public string? Website { get; set; }
}
