using Lightcode.Registration.Domain.Entities;
using MongoDB.Bson.Serialization;

namespace Lightcode.Registration.Infrastructure.Persistence.Mongo;

public static class MongoSerialization
{
    private static readonly Lock Sync = new();
    private static bool _registered;

    public static void EnsureRegistered()
    {
        lock (Sync)
        {
            if (_registered)
                return;

            RegisterTenant();
            RegisterWeatherForecast();
            RegisterAccountJsonSchema();
            RegisterEmailTemplate();
            RegisterTenantSmtpSettings();
            RegisterPlatformSmtpSettings();
            RegisterOAuthClient();
            RegisterHostedAuthentication();
            RegisterRefreshToken();
            RegisterTwoFactorChallenge();
            RegisterFrontConfig();
            _registered = true;
        }
    }

    private static void RegisterTenant()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(Tenant)))
            return;

        BsonClassMap.RegisterClassMap<Tenant>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(t => t.Id);
        });
    }

    private static void RegisterWeatherForecast()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(WeatherForecast)))
            return;

        BsonClassMap.RegisterClassMap<WeatherForecast>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(w => w.Id);
            cm.UnmapProperty(w => w.TemperatureF);
        });
    }

    private static void RegisterAccountJsonSchema()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(AccountJsonSchema)))
            return;

        BsonClassMap.RegisterClassMap<AccountJsonSchema>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
            cm.MapProperty(x => x.SchemaJson).SetSerializer(new SchemaJsonBsonSerializer());
            cm.MapProperty(x => x.ConfigJson).SetSerializer(new SchemaJsonBsonSerializer());
        });
    }

    private static void RegisterEmailTemplate()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(EmailTemplate)))
            return;

        BsonClassMap.RegisterClassMap<EmailTemplate>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
        });
    }

    private static void RegisterTenantSmtpSettings()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(TenantSmtpSettingsRoot)))
            return;

        BsonClassMap.RegisterClassMap<TenantSmtpSettingsRoot>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
        });

        if (BsonClassMap.IsClassMapRegistered(typeof(TenantSmtpConfiguration)))
            return;

        BsonClassMap.RegisterClassMap<TenantSmtpConfiguration>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });
    }

    private static void RegisterPlatformSmtpSettings()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(PlatformSmtpSettingsRoot)))
            return;

        BsonClassMap.RegisterClassMap<PlatformSmtpSettingsRoot>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
        });
    }

    private static void RegisterOAuthClient()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(OAuthClient)))
            return;

        BsonClassMap.RegisterClassMap<OAuthClient>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
        });

        if (BsonClassMap.IsClassMapRegistered(typeof(OAuthClientTokenConfiguration)))
            return;

        BsonClassMap.RegisterClassMap<OAuthClientTokenConfiguration>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });

        if (BsonClassMap.IsClassMapRegistered(typeof(OAuthClientTokenClaimValue)))
            return;

        BsonClassMap.RegisterClassMap<OAuthClientTokenClaimValue>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
        });
    }

    private static void RegisterRefreshToken()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(RefreshToken)))
            return;

        BsonClassMap.RegisterClassMap<RefreshToken>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
        });
    }

    private static void RegisterHostedAuthentication()
    {
        if (!BsonClassMap.IsClassMapRegistered(typeof(HostedAuthTransaction)))
        {
            BsonClassMap.RegisterClassMap<HostedAuthTransaction>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(x => x.Id);
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(HostedAuthSession)))
        {
            BsonClassMap.RegisterClassMap<HostedAuthSession>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(x => x.Id);
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(AuthorizationCodeGrant)))
        {
            BsonClassMap.RegisterClassMap<AuthorizationCodeGrant>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(x => x.Id);
            });
        }

        if (!BsonClassMap.IsClassMapRegistered(typeof(AuthAuditLog)))
        {
            BsonClassMap.RegisterClassMap<AuthAuditLog>(cm =>
            {
                cm.AutoMap();
                cm.SetIgnoreExtraElements(true);
                cm.MapIdProperty(x => x.Id);
            });
        }
    }

    private static void RegisterFrontConfig()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(FrontConfig)))
            return;

        BsonClassMap.RegisterClassMap<FrontConfig>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
            cm.GetMemberMap(x => x.Active).SetElementName("active");
            cm.GetMemberMap(x => x.Messages).SetElementName("messages");
            cm.GetMemberMap(x => x.Css).SetElementName("css");
            cm.GetMemberMap(x => x.LogoUrl).SetElementName("logo_url");
            cm.GetMemberMap(x => x.BackgroundImageUrl).SetElementName("background_image_url");
            cm.GetMemberMap(x => x.CreatedAtUtc).SetElementName("created_at_utc");
            cm.GetMemberMap(x => x.UpdatedAtUtc).SetElementName("updated_at_utc");
        });

        if (BsonClassMap.IsClassMapRegistered(typeof(FrontConfigMessages)))
            return;

        BsonClassMap.RegisterClassMap<FrontConfigMessages>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.GetMemberMap(x => x.PageTitle).SetElementName("page_title");
            cm.GetMemberMap(x => x.Heading).SetElementName("heading");
            cm.GetMemberMap(x => x.Subtitle).SetElementName("subtitle");
            cm.GetMemberMap(x => x.UsernameLabel).SetElementName("username_label");
            cm.GetMemberMap(x => x.UsernamePlaceholder).SetElementName("username_placeholder");
            cm.GetMemberMap(x => x.UsernameRequired).SetElementName("username_required");
            cm.GetMemberMap(x => x.PasswordLabel).SetElementName("password_label");
            cm.GetMemberMap(x => x.PasswordPlaceholder).SetElementName("password_placeholder");
            cm.GetMemberMap(x => x.PasswordRequired).SetElementName("password_required");
            cm.GetMemberMap(x => x.SubmitButton).SetElementName("submit_button");
            cm.GetMemberMap(x => x.SubmittingButton).SetElementName("submitting_button");
            cm.GetMemberMap(x => x.AuthenticationNotIntegrated).SetElementName("authentication_not_integrated");
            cm.GetMemberMap(x => x.TwoFactorHeading).SetElementName("two_factor_heading");
            cm.GetMemberMap(x => x.TwoFactorSubtitle).SetElementName("two_factor_subtitle");
            cm.GetMemberMap(x => x.ForgotPasswordHeading).SetElementName("forgot_password_heading");
            cm.GetMemberMap(x => x.ForgotPasswordSubtitle).SetElementName("forgot_password_subtitle");
            cm.GetMemberMap(x => x.ResetPasswordHeading).SetElementName("reset_password_heading");
            cm.GetMemberMap(x => x.ResetPasswordSubtitle).SetElementName("reset_password_subtitle");
        });
    }

    private static void RegisterTwoFactorChallenge()
    {
        if (BsonClassMap.IsClassMapRegistered(typeof(TwoFactorChallenge)))
            return;

        BsonClassMap.RegisterClassMap<TwoFactorChallenge>(cm =>
        {
            cm.AutoMap();
            cm.SetIgnoreExtraElements(true);
            cm.MapIdProperty(x => x.Id);
        });
    }
}
