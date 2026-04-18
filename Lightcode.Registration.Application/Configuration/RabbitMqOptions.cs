namespace Lightcode.Registration.Application.Configuration;

public sealed class RabbitMqOptions
{
    public const string SectionName = "RabbitMQ";

    public string HostName { get; set; } = "localhost";

    public int Port { get; set; } = 5672;

    public string UserName { get; set; } = "guest";

    public string Password { get; set; } = "guest";

    public string VirtualHost { get; set; } = "/";

    /// <summary>Intervalo entre execuções completas do scan (expiração e publicação de lembretes).</summary>
    public int ScanIntervalMinutes { get; set; } = 360;
}
