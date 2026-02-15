namespace medi_guide_be.Domain.Entities;

/// <summary>
/// Represents a hospital/business record from the Kosovo hospitals database.
/// Returned as stored in the database (same shape for API responses).
/// </summary>
public class KosovoHospital
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Location { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Link { get; set; } = string.Empty;
    public string Image { get; set; } = string.Empty;
    public IReadOnlyList<string> PhoneNumbers { get; set; } = Array.Empty<string>();
    public string? Website { get; set; }
}
