using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Infrastructure;
using Lightcode.Registration.Infrastructure.Notifications;
using Lightcode.Registration.Infrastructure.Persistence.Mongo;
using Lightcode.Registration.Worker;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Driver;
using RabbitMQ.Client;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.Configure<MongoOptions>(builder.Configuration.GetSection(MongoOptions.SectionName));
builder.Services.Configure<MasterOptions>(builder.Configuration.GetSection(MasterOptions.SectionName));
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));
builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection(RabbitMqOptions.SectionName));
builder.Services.Configure<SmtpOptions>(builder.Configuration.GetSection(SmtpOptions.SectionName));

var mongoConn = builder.Configuration.GetSection(MongoOptions.SectionName)["ConnectionString"]
    ?? throw new InvalidOperationException("Mongo:ConnectionString em falta.");
builder.Services.AddSingleton<IMongoClient>(_ => new MongoClient(mongoConn));

MongoSerialization.EnsureRegistered();
builder.Services.AddInfrastructure();

builder.Services.AddSingleton<IConnection>(_ =>
{
    var o = _.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
    var f = new ConnectionFactory
    {
        HostName = o.HostName,
        Port = o.Port,
        UserName = o.UserName,
        Password = o.Password,
        VirtualHost = o.VirtualHost
    };
    return f.CreateConnection("lightcode-registration-worker");
});

builder.Services.AddSingleton<IAccountExpiryNotificationSender>(sp =>
{
    var smtp = sp.GetRequiredService<IOptions<SmtpOptions>>().Value;
    if (smtp.UseSmtp)
        return ActivatorUtilities.CreateInstance<SmtpAccountExpiryNotificationSender>(sp);
    return ActivatorUtilities.CreateInstance<LoggingAccountExpiryNotificationSender>(sp);
});

builder.Services.AddHostedService<RegistrationExpiryScanHostedService>();
builder.Services.AddHostedService<RegistrationExpiryReminderConsumerHostedService>();

var host = builder.Build();
host.Run();
