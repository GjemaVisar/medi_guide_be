using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Repositories;

public interface IKosovoHospitalRepository
{
    /// <summary>
    /// Returns hospitals whose location or description contains the given city name (case-insensitive).
    /// </summary>
    Task<IReadOnlyList<KosovoHospital>> GetByCityAsync(string city, CancellationToken cancellationToken = default);
}
