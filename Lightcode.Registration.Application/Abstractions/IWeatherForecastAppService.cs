using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Abstractions;

public interface IWeatherForecastAppService
{
    Task<ServiceResult<IReadOnlyList<WeatherForecast>>> GetAllAsync(CancellationToken cancellationToken = default);

    Task<ServiceResult<WeatherForecast>> AddSampleAsync(CancellationToken cancellationToken = default);
}
