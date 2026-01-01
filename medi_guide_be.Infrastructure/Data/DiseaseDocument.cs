using MongoDB.Bson;
using MongoDB.Bson.Serialization.Attributes;

namespace medi_guide_be.Infrastructure.Data;

[BsonIgnoreExtraElements]
public class DiseaseDocument
{
    [BsonId]
    [BsonRepresentation(BsonType.ObjectId)]
    public string Id { get; set; } = string.Empty;

    [BsonElement("diseases")]
    public string Diseases { get; set; } = string.Empty;
}