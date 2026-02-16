using medi_guide_be.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace medi_guide_be.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AdminController : ControllerBase
{
    private readonly IDiseaseVectorRepository _diseaseVectorRepository;
    private readonly ILogger<AdminController> _logger;

    public AdminController(
        IDiseaseVectorRepository diseaseVectorRepository,
        ILogger<AdminController> logger)
    {
        _diseaseVectorRepository = diseaseVectorRepository;
        _logger = logger;
    }

    /// <summary>
    /// Reads every document from the raw "diseases" collection, pre-computes
    /// binary vectors, and stores them in the compact "vector_cache" collection.
    /// 
    /// This needs to run ONCE (or whenever the raw disease data changes).
    /// After this, every cold-start loads from the cache in seconds instead of minutes.
    /// 
    /// NOTE: On the M0 free tier this may take 15-20 minutes because it reads
    /// all 50k wide documents. Increase Cloud Run request timeout to 1800s
    /// or run against the same MongoDB from a local machine.
    /// </summary>
    [HttpPost("rebuild-vectors")]
    public async Task<IActionResult> RebuildVectors(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Admin triggered vector cache rebuild.");

        var sw = Stopwatch.StartNew();
        var count = await _diseaseVectorRepository.RebuildVectorCacheAsync(cancellationToken);
        sw.Stop();

        return Ok(new
        {
            message = "Vector cache rebuilt successfully.",
            vectorCount = count,
            elapsedSeconds = Math.Round(sw.Elapsed.TotalSeconds, 1)
        });
    }
}
