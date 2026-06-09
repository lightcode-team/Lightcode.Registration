using System.Text;
using System.Text.Json;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Expiry;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Worker.RabbitMq;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using MongoDB.Bson;
using MongoDB.Driver;
using RabbitMQ.Client;

namespace Lightcode.Registration.Worker;

public sealed class RegistrationExpiryScanHostedService(
    IConnection rabbitConnection,
    IMongoClient mongoClient,
    IServiceScopeFactory scopeFactory,
    IOptions<RabbitMqOptions> rabbitOptions,
    ILogger<RegistrationExpiryScanHostedService> logger) : BackgroundService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        AccountRegistrationRabbitTopology.Ensure(rabbitConnection);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunScanAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falha no scan de expiração de cadastros.");
            }

            var delay = TimeSpan.FromMinutes(Math.Max(1, rabbitOptions.Value.ScanIntervalMinutes));
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (TaskCanceledException)
            {
                break;
            }
        }
    }

    private async Task RunScanAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var tenantLookup = scope.ServiceProvider.GetRequiredService<ITenantLookup>();
        var schemaRepository = scope.ServiceProvider.GetRequiredService<IAccountJsonSchemaRepository>();
        var userAccountWriter = scope.ServiceProvider.GetRequiredService<IUserAccountWriter>();

        var tenants = await tenantLookup.ListActiveAsync(cancellationToken);
        using var publishChannel = rabbitConnection.CreateModel();
        var props = publishChannel.CreateBasicProperties();
        props.Persistent = true;

        foreach (var tenant in tenants)
        {
            var schema = await schemaRepository.GetDefaultAsync(tenant.Id, cancellationToken);
            if (schema is null || !AccountSchemaConfigParser.TryGetRegistrationExpiry(schema.ConfigJson, out _))
                continue;

            var coll = mongoClient.GetDatabase(tenant.DatabaseName).GetCollection<BsonDocument>("Users");
            using var cursor = await coll.FindAsync(FilterDefinition<BsonDocument>.Empty, cancellationToken: cancellationToken);
            var list = await cursor.ToListAsync(cancellationToken);

            foreach (var doc in list)
            {
                if (!doc.Contains("registrationExpiresAtUtc") || !doc["registrationExpiresAtUtc"].IsValidDateTime)
                    continue;

                var status = doc.Contains("status") && doc["status"].IsString
                    ? doc["status"].AsString
                    : AccountStatuses.Active;
                if (status is AccountStatuses.Expired or AccountStatuses.PendingConfirmation)
                    continue;

                var expiresUtc = doc["registrationExpiresAtUtc"].ToUniversalTime();
                var daysRemaining = (expiresUtc.Date - DateTime.UtcNow.Date).Days;
                var userId = doc["_id"].ToString()!;

                if (daysRemaining < 0)
                {
                    await userAccountWriter.TryMarkRegistrationExpiredAsync(tenant.Id, userId, cancellationToken);
                    continue;
                }

                var email = doc.Contains("email") && doc["email"].IsString ? doc["email"].AsString : null;
                if (string.IsNullOrWhiteSpace(email))
                    continue;

                var sent30 = doc.Contains("expiryReminder30SentUtc") && doc["expiryReminder30SentUtc"].IsValidDateTime;
                var sent15 = doc.Contains("expiryReminder15SentUtc") && doc["expiryReminder15SentUtc"].IsValidDateTime;

                if (daysRemaining <= 30 && daysRemaining > 15 && !sent30)
                    PublishReminder(publishChannel, props, tenant.Id, userId, email, 30, expiresUtc);

                if (daysRemaining <= 15 && daysRemaining > 0 && !sent15)
                    PublishReminder(publishChannel, props, tenant.Id, userId, email, 15, expiresUtc);
            }
        }
    }

    private void PublishReminder(
        IModel channel,
        IBasicProperties props,
        string tenantId,
        string userId,
        string email,
        int reminderKind,
        DateTime registrationExpiresAtUtc)
    {
        var msg = new RegistrationExpiryReminderMessage(tenantId, userId, email, reminderKind, registrationExpiresAtUtc);
        var body = JsonSerializer.SerializeToUtf8Bytes(msg, JsonOptions);
        channel.BasicPublish(
            AccountRegistrationRabbitTopology.ExchangeName,
            AccountRegistrationRabbitTopology.ReminderRoutingKey(reminderKind),
            props,
            body);
        logger.LogDebug("Publicado lembrete {Kind}d para user {UserId} (tenant {TenantId})", reminderKind, userId, tenantId);
    }
}
