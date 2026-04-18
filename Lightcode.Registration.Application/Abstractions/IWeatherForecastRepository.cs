using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IWeatherForecastRepository
{
    Task<IReadOnlyList<WeatherForecast>> ListAsync(CancellationToken cancellationToken = default);

    Task AddAsync(WeatherForecast entity, CancellationToken cancellationToken = default);
}
