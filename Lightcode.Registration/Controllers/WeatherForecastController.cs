using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize(Policy = "HasTenant")]
public sealed class WeatherForecastController(IWeatherForecastAppService weatherForecastAppService) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetAll(CancellationToken cancellationToken)
    {
        var result = await weatherForecastAppService.GetAllAsync(cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("sample")]
    public async Task<IActionResult> AddSample(CancellationToken cancellationToken)
    {
        var result = await weatherForecastAppService.AddSampleAsync(cancellationToken);
        return result.ToApiResponse();
    }
}
