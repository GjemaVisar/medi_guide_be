using medi_guide_be.Domain.Entities;

namespace medi_guide_be.Domain.Services;

public interface IDiseaseSimilarityService
{
    /// <summary>Pre-loads symptom index and disease vectors into cache.</summary>
    Task WarmUpAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DiseaseMatch>> GetTopMatchesAsync(
        IReadOnlyList<string> selectedSymptomKeys,
        int topN = 10,
        CancellationToken cancellationToken = default);
}
