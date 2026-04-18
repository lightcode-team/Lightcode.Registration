using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public sealed class WeatherForecastRepositoryAdapter(MongoRepository<WeatherForecast> inner) : IWeatherForecastRepository
{
    public async Task<IReadOnlyList<WeatherForecast>> ListAsync(CancellationToken cancellationToken = default)
    {
        var list = await inner.GetAllAsync(cancellationToken);
        return list;
    }

    public Task AddAsync(WeatherForecast entity, CancellationToken cancellationToken = default) =>
        inner.InsertAsync(entity, cancellationToken);
}
