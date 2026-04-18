using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Services;

public sealed class WeatherForecastAppService(IWeatherForecastRepository repository) : IWeatherForecastAppService
{
    private static readonly string[] Summaries =
    [
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    ];

    public async Task<ServiceResult<IReadOnlyList<WeatherForecast>>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var items = await repository.ListAsync(cancellationToken);
        return ServiceResult<IReadOnlyList<WeatherForecast>>.Ok(items);
    }

    public async Task<ServiceResult<WeatherForecast>> AddSampleAsync(CancellationToken cancellationToken = default)
    {
        var entity = new WeatherForecast
        {
            Date = DateOnly.FromDateTime(DateTime.UtcNow),
            TemperatureC = Random.Shared.Next(-20, 55),
            Summary = Summaries[Random.Shared.Next(Summaries.Length)]
        };

        await repository.AddAsync(entity, cancellationToken);
        return ServiceResult<WeatherForecast>.Ok(entity, 201);
    }
}
