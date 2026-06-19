using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Infrastructure;
using Lightcode.Registration.Infrastructure.Notifications;
using Lightcode.Registration.Infrastructure.Persistence.Mongo;
using Lightcode.Registration.Worker;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;
using System.Net.Sockets;

RegistrationHostEnvironment.LoadDotEnvIfPresent();

static bool IsTransientRabbitFailure(Exception ex)
{
    if (ex is AggregateException agg)
        return agg.Flatten().InnerExceptions.Any(IsTransientRabbitFailureCore);

    return IsTransientRabbitFailureCore(ex);
}

static bool IsTransientRabbitFailureCore(Exception ex)
{
    for (var e = ex; e is not null; e = e.InnerException!)
    {
        if (e is BrokerUnreachableException)
            return true;
        if (e is SocketException { SocketErrorCode: SocketError.ConnectionRefused })
            return true;
    }

    return false;
}

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<MasterOptions>(builder.Configuration.GetSection(MasterOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));
builder.Services.Configure<MasterSmtpOptions>(builder.Configuration.GetSection(MasterSmtpOptions.SectionName));

var mongoConn = builder.Configuration.GetSection(MongoOptions.SectionName)["ConnectionString"]
    ?? throw new InvalidOperationException("Mongo:ConnectionString em falta.");
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConn));

MongoSerialization.EnsureRegistered();
builder.Services.AddHttpContextAccessor();
builder.Services.AddInfrastructure();

builder.Services.AddSingleton<IConnection>(sp =>
{
    var o = sp.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
    var log = sp.GetRequiredService<ILoggerFactory>().CreateLogger("RabbitMqConnection");
    var f = new ConnectionFactory
    {
        HostName = o.HostName,
        Port = o.Port,
        UserName = o.UserName,
        Password = o.Password,
        VirtualHost = o.VirtualHost,
        // Obrigatório para AsyncEventingBasicConsumer (consumidores em EmailDispatch / Reminder).
        DispatchConsumersAsync = true
    };

    const int maxAttempts = 45;
    var delay = TimeSpan.FromSeconds(2);
    Exception? lastFailure = null;
    for (var attempt = 1; attempt <= maxAttempts; attempt++)
    {
        try
        {
            var conn = f.CreateConnection("lightcode-registration-worker");
            if (attempt > 1)
                log.LogInformation("Ligado ao RabbitMQ em {Host}:{Port} após {Attempt} tentativa(s).", o.HostName, o.Port, attempt);
            return conn;
        }
        catch (Exception ex) when (IsTransientRabbitFailure(ex))
        {
            lastFailure = ex;
            log.LogWarning(
                ex,
                "RabbitMQ indisponível em {Host}:{Port} (tentativa {Attempt}/{Max}); nova tentativa em {Delay}s.",
                o.HostName,
                o.Port,
                attempt,
                maxAttempts,
                delay.TotalSeconds);
            if (attempt < maxAttempts)
                Thread.Sleep(delay);
        }
    }

    throw new InvalidOperationException(
        $"Não foi possível ligar ao RabbitMQ em {o.HostName}:{o.Port} após {maxAttempts} tentativas (~{maxAttempts * delay.TotalSeconds:F0}s). " +
        "Confirme o serviço RabbitMQ, a rede Docker (hostname «rabbitmq») e as variáveis RabbitMQ__*.",
        lastFailure);
});

builder.Services.AddSingleton<IAccountExpiryNotificationSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
    if (smtp.UseSmtp)
        return ActivatorUtilities.CreateInstance<SmtpAccountExpiryNotificationSender>(sp);
    return ActivatorUtilities.CreateInstance<LoggingAccountExpiryNotificationSender>(sp);
});

builder.Services.AddSingleton<IOutboundMailSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
    if (smtp.UseSmtp)
        return ActivatorUtilities.CreateInstance<SmtpOutboundMailSender>(sp);
    return ActivatorUtilities.CreateInstance<LoggingOutboundMailSender>(sp);
});

builder.Services.AddSingleton<ISystemOutboundMailSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<MasterSmtpOptions>>().Value;
    if (smtp.UseSmtp)
        return ActivatorUtilities.CreateInstance<SmtpSystemOutboundMailSender>(sp);
    return ActivatorUtilities.CreateInstance<LoggingSystemOutboundMailSender>(sp);
});

builder.Services.AddHostedService<RegistrationExpiryScanHostedService>();
builder.Services.AddHostedService<RegistrationExpiryReminderConsumerHostedService>();
builder.Services.AddHostedService<EmailDispatchConsumerHostedService>();

var host = builder.Build();
host.Run();
