using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.SchemaConfig;

public static class AccountSchemaConfigParser
{
    /// <summary>Valida e materializa config; <paramref name="error"/> preenchido em caso de JSON inválido ou regras de negócio violadas.</summary>
    public static bool TryParseAndValidate(string? configJson, out AccountJsonSchemaConfig? config, out string? error) =>
        AccountJsonSchemaConfig.TryParseAndValidate(configJson, out config, out error);

    /// <summary>Interpreta config para regras de expiração no registo; só aplica se <c>expiryRegister</c> e dias válidos.</summary>
    public static bool TryGetRegistrationExpiry(string? configJson, out int daysExpiry)
    {
        daysExpiry = 0;
        if (!TryParseAndValidate(configJson, out var config, out _))
            return false;

        return TryGetRegistrationExpiry(config, out daysExpiry);
    }

    public static bool TryGetRegistrationExpiry(AccountJsonSchemaConfig? config, out int daysExpiry)
    {
        daysExpiry = 0;
        if (config?.Expiry is null)
            return false;

        if (!config.Expiry.ExpiryRegister || config.Expiry.DaysExpiry <= 0)
            return false;

        daysExpiry = config.Expiry.DaysExpiry;
        return true;
    }

    /// <summary>Indica se o 2FA por email está ligado e devolve o modo.</summary>
    public static bool TryGetEmailTwoFactor(string? configJson, out EmailTwoFactorType? type)
    {
        type = null;
        if (!TryParseAndValidate(configJson, out var config, out _) || config?.TwoFactor is not { Active: true })
            return false;

        type = config.TwoFactor.Type;
        return type.HasValue;
    }
}
