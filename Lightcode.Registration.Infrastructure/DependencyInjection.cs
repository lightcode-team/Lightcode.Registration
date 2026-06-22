using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Emails;
using Lightcode.Registration.Application.Services;
using Lightcode.Registration.Application.TwoFactor;
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
        services.AddScoped<IPlatformAdminRepository, MongoPlatformAdminRepository>();
        services.AddScoped<IAccountJsonSchemaRepository, MongoAccountJsonSchemaRepository>();
        services.AddScoped<IFrontConfigRepository, MongoFrontConfigRepository>();
        services.AddScoped<IJsonSchemaValidationService, JsonSchemaDocumentValidator>();
        services.AddScoped<IJsonSchemaToMongoValidatorMapper, JsonSchemaDraftToMongoValidatorMapper>();
        services.AddScoped<IUsersCollectionSchemaApplier, MongoUsersCollectionSchemaApplier>();
        services.AddScoped<IUserAccountWriter, UserAccountMongoWriter>();
        services.AddScoped<IUserCredentialValidator, UserCredentialValidator>();
        services.AddScoped<IPasswordHasher, Pbkdf2PasswordHasher>();
        services.AddSingleton<ISecureTokenGenerator, SecureTokenGenerator>();
        services.AddSingleton<ITenantSigningKeyProtector, AesGcmTenantSigningKeyProtector>();
        services.AddScoped<ITenantSigningKeyResolver, MongoTenantSigningKeyResolver>();

        services.AddScoped<IOAuthClientRepository, MongoOAuthClientRepository>();
        services.AddScoped<IOAuthClientAppService, OAuthClientAppService>();
        services.AddScoped<IRefreshTokenRepository, MongoRefreshTokenRepository>();
        services.AddScoped<ITwoFactorChallengeRepository, MongoTwoFactorChallengeRepository>();
        services.AddScoped<ITwoFactorSettingsService, MongoUserTwoFactorSettingsService>();
        services.AddScoped<ITwoFactorChallengeService, TwoFactorChallengeService>();
        services.AddScoped<ITwoFactorMethod, EmailCodeTwoFactorMethod>();
        services.AddScoped<ITwoFactorMethod, TotpTwoFactorMethod>();
        services.AddScoped<ITwoFactorMethodProvider, TwoFactorMethodProvider>();
        services.AddScoped<IPlatformSystemEmailSender, QueuedPlatformSystemEmailSender>();

        services.AddScoped<ITenantProvisioner, MongoTenantProvisioner>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();
        services.AddScoped(typeof(MongoRepository<>));

        services.AddScoped<IWeatherForecastRepository, WeatherForecastRepositoryAdapter>();
        services.AddScoped<IAccessTokenIssuer, JwtAccessTokenIssuer>();

        services.AddScoped<IAuthenticationAppService, AuthenticationAppService>();
        services.AddScoped<IPlatformAdminAppService, PlatformAdminAppService>();
        services.AddScoped<ITenantOnboardingAppService, TenantOnboardingAppService>();
        services.AddScoped<IWeatherForecastAppService, WeatherForecastAppService>();
        services.AddScoped<IAccountJsonSchemaAppService, AccountJsonSchemaAppService>();
        services.AddScoped<AccountRegistrationTwoFactorSupport>();
        services.AddScoped<IAccountRegistrationAppService, AccountRegistrationAppService>();
        services.AddScoped<IAccountAdminAppService, AccountAdminAppService>();
        services.AddScoped<IAccountUpdateAppService, AccountUpdateAppService>();
        services.AddScoped<IAccountCompleteRegistrationAppService, AccountCompleteRegistrationAppService>();
        services.AddScoped<IAccountEmailConfirmationAppService, AccountEmailConfirmationAppService>();
        services.AddScoped<IAccountPasswordResetAppService, AccountPasswordResetAppService>();
        services.AddScoped<IFrontConfigAppService, FrontConfigAppService>();
        services.AddScoped<IAccountTwoFactorAppService, AccountTwoFactorAppService>();

        services.AddScoped<ITenantSmtpSettingsRepository, MongoTenantSmtpSettingsRepository>();
        services.AddScoped<IEmailTemplateRepository, MongoEmailTemplateRepository>();
        services.AddScoped<IPlatformEmailTemplateRepository, MongoPlatformEmailTemplateRepository>();
        services.AddHostedService<PlatformEmailMasterSeedHostedService>();
        if (registerRabbitMqEmailEnqueuePublisher)
            services.AddScoped<IEmailEnqueuePublisher, RabbitMqEmailEnqueuePublisher>();
        else
            services.AddScoped<IEmailEnqueuePublisher, DisabledEmailEnqueuePublisher>();

        return services;
    }
}
