using Microsoft.AspNetCore.Mvc;
using Zootact.API.Services;

namespace Zootact.API.Controllers;

[ApiController]
[Route("health")]
public sealed class HealthController(IHealthStatusService healthStatusService) : ControllerBase
{
    [HttpGet("live")]
    public async Task<IActionResult> GetLive(CancellationToken cancellationToken)
    {
        var response = await healthStatusService.GetLiveAsync();
        return Ok(response);
    }

    [HttpGet("ready")]
    public async Task<IActionResult> GetReady(CancellationToken cancellationToken)
    {
        var response = await healthStatusService.GetReadyAsync(cancellationToken);
        return response.Status == "healthy"
            ? Ok(response)
            : StatusCode(StatusCodes.Status503ServiceUnavailable, response);
    }

    [HttpGet("dependencies")]
    public async Task<IActionResult> GetDependencies(CancellationToken cancellationToken)
    {
        var response = await healthStatusService.GetDependenciesAsync(cancellationToken);
        return Ok(response);
    }
}
