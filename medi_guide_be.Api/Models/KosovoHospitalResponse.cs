using System.Text.Json.Serialization;

namespace medi_guide_be.Api.Models;

/// <summary>
/// API response shape matching the document as stored in MongoDB.
/// </summary>
public class KosovoHospitalResponse
{
    [JsonPropertyName("_id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("location")]
    public string Location { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("link")]
    public string Link { get; set; } = string.Empty;

    [JsonPropertyName("image")]
    public string Image { get; set; } = string.Empty;

    [JsonPropertyName("phone_numbers")]
    public IReadOnlyList<string> PhoneNumbers { get; set; } = Array.Empty<string>();

    [JsonPropertyName("website")]
    public string? Website { get; set; }
}
