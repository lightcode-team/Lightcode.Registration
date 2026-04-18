using System.Text.Json;

namespace Lightcode.Registration.Application.SchemaConfig;

public static class AccountSchemaConfigParser
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    /// <summary>Valida e materializa config; <paramref name="error"/> preenchido em caso de JSON inválido ou regras de negócio violadas.</summary>
    public static bool TryParseAndValidate(string? configJson, out AccountSchemaConfig? config, out string? error)
    {
        config = null;
        error = null;

        if (string.IsNullOrWhiteSpace(configJson))
            return true;

        try
        {
            config = JsonSerializer.Deserialize<AccountSchemaConfig>(configJson, Options);
        }
        catch (JsonException ex)
        {
            error = $"config JSON inválido: {ex.Message}";
            return false;
        }

        if (config is null)
            return true;

        if (config.Expiry is null)
            return true;

        if (config.Expiry.ExpiryRegister && config.Expiry.DaysExpiry <= 0)
        {
            error = "Quando Expiry.expiryRegister é true, Expiry.daysExpiry deve ser maior que zero.";
            return false;
        }

        return true;
    }

    /// <summary>Interpreta config para regras de expiração no registo; só aplica se <c>expiryRegister</c> e dias válidos.</summary>
    public static bool TryGetRegistrationExpiry(string? configJson, out int daysExpiry)
    {
        daysExpiry = 0;
        if (!TryParseAndValidate(configJson, out var config, out _) || config?.Expiry is null)
            return false;

        if (!config.Expiry.ExpiryRegister || config.Expiry.DaysExpiry <= 0)
            return false;

        daysExpiry = config.Expiry.DaysExpiry;
        return true;
    }
}
