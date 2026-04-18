namespace Lightcode.Registration.Domain.Entities;

public class WeatherForecast
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public DateOnly Date { get; set; }

    public int TemperatureC { get; set; }

    public int TemperatureF => 32 + (int)(TemperatureC / 0.5556);

    public string? Summary { get; set; }
}
