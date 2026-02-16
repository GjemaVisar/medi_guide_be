using Microsoft.AspNetCore.Mvc;

namespace medi_guide_be.Api.Controllers;

[ApiController]
[Route("[controller]")]
public class HealthController : ControllerBase
{
    /// <summary>
    /// Lightweight health-check endpoint.
    /// Point an external ping service (UptimeRobot, cron-job.org, Google Cloud Scheduler)
    /// at this URL every 5 minutes to keep the Cloud Run container alive
    /// and prevent cold-start cache eviction.
    /// </summary>
    [HttpGet("/health")]
    public IActionResult Health()
    {
        return Ok(new
        {
            status = "healthy",
            timestamp = DateTime.UtcNow
        });
    }
}
