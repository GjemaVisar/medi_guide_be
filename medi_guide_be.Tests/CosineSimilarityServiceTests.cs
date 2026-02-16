using medi_guide_be.Domain.Entities;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Infrastructure.Services;
using Microsoft.Extensions.Caching.Memory;
using NSubstitute;

namespace medi_guide_be.Tests;

public class CosineSimilarityServiceTests
{
    private readonly IDiseaseVectorRepository _repository;
    private readonly IMemoryCache _cache;
    private readonly CosineSimilarityService _sut;

    public CosineSimilarityServiceTests()
    {
        _repository = Substitute.For<IDiseaseVectorRepository>();
        _cache = new MemoryCache(new MemoryCacheOptions());
        _sut = new CosineSimilarityService(_repository, _cache);
    }

    [Fact]
    public async Task GetTopMatchesAsync_WhenUserSymptomsMatchDiseaseExactly_ReturnsSimilarityOne()
    {
        // Arrange: 3 symptoms (indices 0,1,2). One disease has exactly those symptoms.
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["s0"] = 0, ["s1"] = 1, ["s2"] = 2
        };
        var vectorFullMatch = new byte[] { 1, 1, 1 }; // magnitude = sqrt(3)
        var magnitudeFull = Math.Sqrt(3);
        var diseases = new List<DiseaseVectorRecord>
        {
            new("id1", "FullMatch", vectorFullMatch, magnitudeFull)
        };

        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);
        _repository.GetAllDiseaseVectorsAsync(Arg.Any<CancellationToken>()).Returns(diseases);

        // Act: user selects the same 3 symptoms
        var result = await _sut.GetTopMatchesAsync(new[] { "s0", "s1", "s2" }, topN: 5);

        // Assert
        var match = result.Single();
        Assert.Equal("id1", match.DiseaseId);
        Assert.Equal("FullMatch", match.DiseaseName);
        Assert.Equal(1.0, match.SimilarityScore, precision: 5);
    }

    [Fact]
    public async Task GetTopMatchesAsync_WithMultipleDiseases_RanksByDescendingSimilarity()
    {
        // Arrange: 3 symptoms (indices 0,1,2). Three diseases with different overlap.
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase)
        {
            ["s0"] = 0, ["s1"] = 1, ["s2"] = 2
        };
        // Disease A: [1,1,1] mag=sqrt(3)  -> dot=3, cos=3/(sqrt(3)*sqrt(3))=1.0
        // Disease B: [1,1,0] mag=sqrt(2)  -> dot=2, cos=2/(sqrt(3)*sqrt(2))=2/sqrt(6)
        // Disease C: [0,0,0] mag=0        -> similarity 0
        var diseases = new List<DiseaseVectorRecord>
        {
            new("idA", "DiseaseA", new byte[] { 1, 1, 1 }, Math.Sqrt(3)),
            new("idB", "DiseaseB", new byte[] { 1, 1, 0 }, Math.Sqrt(2)),
            new("idC", "DiseaseC", new byte[] { 0, 0, 0 }, 0)
        };

        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);
        _repository.GetAllDiseaseVectorsAsync(Arg.Any<CancellationToken>()).Returns(diseases);

        // Act: user selects s0, s1, s2
        var result = await _sut.GetTopMatchesAsync(new[] { "s0", "s1", "s2" }, topN: 10);

        // Assert: order must be A (1.0), B (2/sqrt(6)); C (0) is excluded (zero-score entries are skipped)
        Assert.Equal(2, result.Count);
        Assert.Equal("idA", result[0].DiseaseId);
        Assert.Equal(1.0, result[0].SimilarityScore, precision: 5);
        Assert.Equal("idB", result[1].DiseaseId);
        Assert.Equal(2.0 / Math.Sqrt(6), result[1].SimilarityScore, precision: 5);
    }

    [Fact]
    public async Task GetTopMatchesAsync_RespectsTopN()
    {
        // Arrange: 5 diseases, ask for top 2
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["s0"] = 0 };
        var diseases = new List<DiseaseVectorRecord>
        {
            new("1", "D1", new byte[] { 1 }, 1),
            new("2", "D2", new byte[] { 1 }, 1),
            new("3", "D3", new byte[] { 1 }, 1),
            new("4", "D4", new byte[] { 0 }, 0),
            new("5", "D5", new byte[] { 0 }, 0)
        };
        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);
        _repository.GetAllDiseaseVectorsAsync(Arg.Any<CancellationToken>()).Returns(diseases);

        // Act
        var result = await _sut.GetTopMatchesAsync(new[] { "s0" }, topN: 2);

        // Assert
        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task GetTopMatchesAsync_WhenNoSymptomsSelected_ReturnsEmpty()
    {
        // Arrange
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["s0"] = 0 };
        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);

        // Act
        var result = await _sut.GetTopMatchesAsync(Array.Empty<string>(), topN: 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopMatchesAsync_WhenAllSymptomKeysUnknown_ReturnsEmpty()
    {
        // Arrange: index only has "s0", user sends "unknown1", "unknown2"
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["s0"] = 0 };
        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);

        // Act
        var result = await _sut.GetTopMatchesAsync(new[] { "unknown1", "unknown2" }, topN: 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopMatchesAsync_NormalizesSymptomKeys_IgnoresCaseAndWhitespace()
    {
        // Arrange: index has lowercase key "s0"
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["s0"] = 0 };
        var diseases = new List<DiseaseVectorRecord>
        {
            new("id1", "D1", new byte[] { 1 }, 1.0)
        };
        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);
        _repository.GetAllDiseaseVectorsAsync(Arg.Any<CancellationToken>()).Returns(diseases);

        // Act: send mixed case and spaces
        var result = await _sut.GetTopMatchesAsync(new[] { "  S0  " }, topN: 10);

        // Assert
        Assert.Single(result);
        Assert.Equal(1.0, result[0].SimilarityScore, precision: 5);
    }

    [Fact]
    public async Task GetTopMatchesAsync_WhenNoDiseases_ReturnsEmpty()
    {
        // Arrange
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["s0"] = 0 };
        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);
        _repository.GetAllDiseaseVectorsAsync(Arg.Any<CancellationToken>()).Returns(new List<DiseaseVectorRecord>());

        // Act
        var result = await _sut.GetTopMatchesAsync(new[] { "s0" }, topN: 10);

        // Assert
        Assert.Empty(result);
    }

    [Fact]
    public async Task GetTopMatchesAsync_CosineFormula_MatchesExpectedValue()
    {
        // Arrange: user has 2 symptoms (indices 0,1). Disease has 2 symptoms at 0,1. cos = 2/(sqrt(2)*sqrt(2)) = 1
        var symptomIndex = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase) { ["a"] = 0, ["b"] = 1 };
        var diseases = new List<DiseaseVectorRecord>
        {
            new("id1", "D1", new byte[] { 1, 1 }, Math.Sqrt(2))
        };
        _repository.GetSymptomToIndexMapAsync(Arg.Any<CancellationToken>()).Returns(symptomIndex);
        _repository.GetAllDiseaseVectorsAsync(Arg.Any<CancellationToken>()).Returns(diseases);

        // Act
        var result = await _sut.GetTopMatchesAsync(new[] { "a", "b" }, topN: 1);

        // Assert
        Assert.Single(result);
        Assert.Equal(1.0, result[0].SimilarityScore, precision: 8);
    }
}
