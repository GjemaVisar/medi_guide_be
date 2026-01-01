using medi_guide_be.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace medi_guide_be.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class DiseasesController : ControllerBase
{
    private readonly IDiseaseRepository _diseaseRepository;

    public DiseasesController(IDiseaseRepository diseaseRepository)
    {
        _diseaseRepository = diseaseRepository;
    }

    [HttpGet]
    public async Task<ActionResult<List<string>>> GetAllDiseases()
    {
        var diseases = await _diseaseRepository.GetAllDiseaseNamesAsync();
        return Ok(diseases);
    }
}

