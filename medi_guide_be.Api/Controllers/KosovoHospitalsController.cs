using medi_guide_be.Api.Models;
using medi_guide_be.Domain.Repositories;
using Microsoft.AspNetCore.Mvc;

namespace medi_guide_be.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class KosovoHospitalsController : ControllerBase
{
    private readonly IKosovoHospitalRepository _repository;

    public KosovoHospitalsController(IKosovoHospitalRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Get hospitals in the given city. Matches documents where location or description contains the city name (e.g. "Lipjan").
    /// </summary>
    /// <param name="city">City name (e.g. Lipjan)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of hospital documents as stored in the database</returns>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<KosovoHospitalResponse>>> GetByCity(
        [FromQuery] string? city,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(city))
            return BadRequest("Query parameter 'city' is required.");

        var hospitals = await _repository.GetByCityAsync(city.Trim(), cancellationToken);

        var response = hospitals.Select(h => new KosovoHospitalResponse
        {
            Id = h.Id,
            Name = h.Name,
            Location = h.Location,
            Description = h.Description,
            Link = h.Link,
            Image = h.Image,
            PhoneNumbers = h.PhoneNumbers,
            Website = h.Website
        }).ToList();

        return Ok(response);
    }
}
