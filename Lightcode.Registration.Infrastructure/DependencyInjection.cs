using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Services;
using Lightcode.Registration.Infrastructure.Email;
using Lightcode.Registration.Infrastructure.Hosting;
using Lightcode.Registration.Infrastructure.Persistence.Mongo;
using Lightcode.Registration.Infrastructure.Security;
using Microsoft.Extensions.DependencyInjection;

namespace Lightcode.Registration.Infrastructure;

public static class DependencyInjection
{
    /// <param name="registerRabbitMqEmailEnqueuePublisher">
    /// Quando falso, regista <see cref="DisabledEmailEnqueuePublisher"/> (necessário se não existir <c>IConnection</c> RabbitMQ).
    /// </param>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        bool registerRabbitMqEmailEnqueuePublisher = true)
    {
        services.AddSingleton<IRuntimeEnvironment, AspNetCoreRuntimeEnvironment>();

        services.AddScoped<ITenantLookup, MongoTenantLookup>();
        services.AddScoped<IAccountJsonSchemaRepository, MongoAccountJsonSchemaRepository>();
        services.AddScoped<IJsonSchemaValidationService, JsonSchemaDocumentValidator>();
        services.AddScoped<IJsonSchemaToMongoValidatorMapper, JsonSchemaDraftToMongoValidatorMapper>();
        services.AddScoped<IUsersCollectionSchemaApplier, MongoUsersCollectionSchemaApplier>();
        services.AddScoped<IUserAccountWriter, UserAccountMongoWriter>();
        services.AddScoped<IUserCredentialValidator, UserCredentialValidator>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();

        services.AddScoped<IOAuthClientRepository, MongoOAuthClientRepository>();
        services.AddScoped<IOAuthClientAppService, OAuthClientAppService>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();

        services.AddScoped<ITenantProvisioner, MongoTenantProvisioner>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped(typeof(MongoRepository<>));

        services.AddScoped<IWeatherForecastRepository, WeatherForecastRepositoryAdapter>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddScoped<IAuthenticationAppService, AuthenticationAppService>();
        services.AddScoped<ITenantOnboardingAppService, TenantOnboardingAppService>();
        services.AddScoped<IWeatherForecastAppService, WeatherForecastAppService>();
        services.AddScoped<IAccountJsonSchemaAppService, AccountJsonSchemaAppService>();
        services.AddScoped<IAccountRegistrationAppService, AccountRegistrationAppService>();
        services.AddScoped<IAccountAdminAppService, AccountAdminAppService>();
        services.AddScoped<IAccountUpdateAppService, AccountUpdateAppService>();

        services.AddScoped<ITenantSmtpSettingsRepository, MongoTenantSmtpSettingsRepository>();
        services.AddScoped<IEmailTemplateRepository, MongoEmailTemplateRepository>();
        if (registerRabbitMqEmailEnqueuePublisher)
            services.AddScoped<IEmailEnqueuePublisher, RabbitMqEmailEnqueuePublisher>();
        else
            services.AddScoped<IEmailEnqueuePublisher, DisabledEmailEnqueuePublisher>();

        return services;
    }
}
