using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.Auth;

public sealed record AuthTokenResponse(
    [property: JsonPropertyName("requires_2fa")] bool RequiresTwoFactor,
    [property: JsonPropertyName("token")] IssueTokenResponse? Token,
    [property: JsonPropertyName("challenge")] TwoFactorChallengeDto? Challenge)
{
    public static AuthTokenResponse Issued(IssueTokenResponse token) =>
        new(false, token, null);

    public static AuthTokenResponse TwoFactorRequired(TwoFactorChallengeDto challenge) =>
        new(true, null, challenge);
}

public sealed record TwoFactorChallengeDto(
    [property: JsonPropertyName("challenge_id")] string ChallengeId,
    [property: JsonPropertyName("verification_type")] string VerificationType,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds,
    [property: JsonPropertyName("destination_hint")] string DestinationHint);

public sealed record ConfirmTwoFactorRequest(
    [property: JsonPropertyName("challenge_id")] string? ChallengeId,
    string? Code);

public sealed record TwoFactorBeginResponse(
    [property: JsonPropertyName("challenge_id")] string ChallengeId,
    [property: JsonPropertyName("verification_type")] string VerificationType,
    [property: JsonPropertyName("expires_in")] int ExpiresInSeconds,
    [property: JsonPropertyName("destination_hint")] string DestinationHint);
