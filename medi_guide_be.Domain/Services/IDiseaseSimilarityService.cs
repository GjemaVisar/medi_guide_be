using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Services;

public interface IDiseaseSimilarityService
{
    Task<IReadOnlyList<DiseaseMatch>> GetTopMatchesAsync(
        IReadOnlyList<string> selectedSymptomKeys,
        int topN = 10,
        CancellationToken cancellationToken = default);
}
