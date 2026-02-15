using medi_guide_be.Api.Models;
using medi_guide_be.Domain.Repositories;
using medi_guide_be.Domain.Services;
using Microsoft.AspNetCore.Mvc;

namespace medi_guide_be.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiseasesController : ControllerBase
{
    private readonly IDiseaseRepository _diseaseRepository;
    private readonly IDiseaseSimilarityService _similarityService;

    public DiseasesController(IDiseaseRepository diseaseRepository, IDiseaseSimilarityService similarityService)
    {
        _diseaseRepository = diseaseRepository;
        _similarityService = similarityService;
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetAllDiseases()
    {
        var diseases = await _diseaseRepository.GetAllDiseaseNamesAsync();
        return Ok(diseases);
    }

    [HttpPost("selectedSymptoms")]
    public async Task<ActionResult<IReadOnlyList<object>>> SelectedSymptoms(
        [FromBody] SelectedSymptomsRequest request,
        [FromQuery] int topN = 10,
        CancellationToken cancellationToken = default)
    {
        if (request.Symptoms == null || request.Symptoms.Count == 0)
            return Ok(Array.Empty<object>());

        var matches = await _similarityService.GetTopMatchesAsync(
            request.Symptoms,
            topN,
            cancellationToken);

        var response = matches.Select(m => new
        {
            diseaseName = m.DiseaseName,
            similarityScore = Math.Round(m.SimilarityScore, 2)
        }).ToList();

        return Ok(response);
    }
}

