using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Security;

public static class OAuthClientTokenConfigurationValidator
{
    public static IReadOnlyList<string> Validate(OAuthClientTokenConfiguration config)
    {
        var errors = new List<string>();

        if (config.AccessTokenExpirationMinutes <= 0)
            errors.Add("AccessTokenExpirationMinutes deve ser maior que zero.");

        if (config.RefreshTokenExpirationDays <= 0)
            errors.Add("RefreshTokenExpirationDays deve ser maior que zero.");

        if (config.MaxRefreshTokenUses <= 0)
            errors.Add("MaxRefreshTokenUses deve ser maior que zero.");

        if (config.Values is null || config.Values.Count == 0)
        {
            errors.Add("Values deve conter pelo menos uma entrada.");
            return errors;
        }

        var hasIssuer = false;
        for (var i = 0; i < config.Values.Count; i++)
        {
            var entry = config.Values[i];
            var type = entry.Type?.Trim() ?? "";
            var value = entry.Value?.Trim() ?? "";

            if (string.IsNullOrWhiteSpace(type))
                errors.Add($"Values[{i}].type é obrigatório.");
            else if (!TokenClaimTypes.Supported.Contains(type))
                errors.Add($"Values[{i}].type '{type}' não é suportado.");

            if (string.IsNullOrWhiteSpace(value))
                errors.Add($"Values[{i}].value é obrigatório.");

            if (string.Equals(type, TokenClaimTypes.Issuer, StringComparison.OrdinalIgnoreCase))
                hasIssuer = true;
        }

        if (!hasIssuer)
            errors.Add("Values deve conter pelo menos uma entrada com type=iss.");

        return errors;
    }
}
