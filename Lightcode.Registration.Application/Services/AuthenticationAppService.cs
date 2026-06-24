using System.Text.Json.Nodes;
using System.Security.Claims;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.SchemaConfig;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Application.TwoFactor;
using Lightcode.Registration.Domain.Entities;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class AuthenticationAppService(
    ITenantLookup tenantLookup,
    IUserCredentialValidator credentialValidator,
    IUserAccountWriter userAccountWriter,
    IAccountJsonSchemaRepository schemaRepository,
    IOAuthClientRepository oauthClientRepository,
    IRefreshTokenRepository refreshTokenRepository,
    ITwoFactorSettingsService twoFactorSettingsService,
    ITwoFactorChallengeService twoFactorChallengeService,
    IAccessTokenIssuer accessTokenIssuer,
    ITenantSigningKeyResolver tenantSigningKeyResolver,
    IPasswordHasher passwordHasher,
    IOptions<JwtOptions> jwtOptions,
    IOptions<RegistrationOptions> registrationOptions) : IAuthenticationAppService
{
    public async Task<ServiceResult<AuthTokenResponse>> IssueTokenAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<AuthTokenResponse>.Fail(400, "O cabeçalho do tenant (X-Tenant-Id) é obrigatório.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<AuthTokenResponse>.Fail(401, "Tenant não encontrado ou inativo.");

        var grantType = ResolveGrantType(request);
        return grantType switch
        {
            TokenGrantTypes.Password => await IssuePasswordGrantAsync(request, tenant.Id, cancellationToken),
            TokenGrantTypes.RefreshToken => await IssueRefreshGrantAsync(request, tenant.Id, cancellationToken),
            TokenGrantTypes.ClientCredentials => await IssueClientCredentialsGrantAsync(request, tenant.Id, cancellationToken),
            _ => ServiceResult<AuthTokenResponse>.Fail(400, $"grant_type inválido ou não suportado: '{request.GrantType}'.")
        };
    }

    private static string ResolveGrantType(TokenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.GrantType))
            return request.GrantType.Trim().ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(request.Username) || !string.IsNullOrWhiteSpace(request.Password))
            return TokenGrantTypes.Password;

        if (!string.IsNullOrWhiteSpace(request.RefreshToken))
            return TokenGrantTypes.RefreshToken;

        if (!string.IsNullOrWhiteSpace(request.ClientId) || !string.IsNullOrWhiteSpace(request.ClientSecret))
            return TokenGrantTypes.ClientCredentials;

        return "";
    }

    public async Task<ServiceResult<AuthTokenResponse>> ConfirmTwoFactorAsync(
        ConfirmTwoFactorRequest request,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var authenticated = await ConfirmHostedTwoFactorAsync(request, tenantId, cancellationToken);
        if (!authenticated.IsSuccess)
            return ServiceResult<AuthTokenResponse>.Fail(authenticated.StatusCode, authenticated.Errors);

        return await IssueValidatedIdentityTokenAsync(authenticated.Value!, tenantId, cancellationToken);
    }

    private async Task<ServiceResult<AuthTokenResponse>> IssuePasswordGrantAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        var authenticated = await BeginHostedPasswordAuthenticationAsync(
            request.Username,
            request.Password,
            tenantId,
            cancellationToken);

        if (!authenticated.IsSuccess)
            return ServiceResult<AuthTokenResponse>.Fail(authenticated.StatusCode, authenticated.Errors);

        if (authenticated.Value!.Challenge is { } challenge)
            return ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.TwoFactorRequired(challenge));

        return await IssueValidatedIdentityTokenAsync(authenticated.Value, tenantId, cancellationToken);
    }

    public async Task<ServiceResult<HostedPasswordAuthenticationResult>> BeginHostedPasswordAuthenticationAsync(
        string? username,
        string? password,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            return ServiceResult<HostedPasswordAuthenticationResult>.Fail(400, "Username e password são obrigatórios.");

        var outcome = await credentialValidator.ValidateAsync(
            tenantId,
            username.Trim().ToLowerInvariant(),
            password,
            cancellationToken);

        if (!outcome.IsSuccess)
        {
            return outcome.Failure switch
            {
                CredentialValidationFailure.EmailNotConfirmed =>
                    ServiceResult<HostedPasswordAuthenticationResult>.Fail(403, "Confirme o email antes de entrar."),
                _ => ServiceResult<HostedPasswordAuthenticationResult>.Fail(401, "Credenciais inválidas.")
            };
        }

        var credentials = outcome.Success!;
        TwoFactorChallengeDto? challenge = null;
        if (await RequiresTwoFactorAsync(tenantId, credentials, cancellationToken))
        {
            challenge = await twoFactorChallengeService.CreateEmailChallengeAsync(
                new TwoFactorChallengeSubject(
                    TwoFactorSubjectTypes.TenantUser,
                    credentials.UserId,
                    tenantId,
                    credentials.Email,
                    credentials.Username),
                TwoFactorChallengePurposes.Login,
                cancellationToken);
        }

        return ServiceResult<HostedPasswordAuthenticationResult>.Ok(
            new HostedPasswordAuthenticationResult(
                credentials.UserId,
                credentials.Email,
                credentials.Username,
                challenge,
                Roles: credentials.Roles));
    }

    public async Task<ServiceResult<HostedPasswordAuthenticationResult>> ConfirmHostedTwoFactorAsync(
        ConfirmTwoFactorRequest request,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var verify = await twoFactorChallengeService.VerifyAsync(
            request.ChallengeId ?? string.Empty,
            request.Code ?? string.Empty,
            tenantId.Trim(),
            TwoFactorSubjectTypes.TenantUser,
            TwoFactorChallengePurposes.Login,
            cancellationToken);

        if (!verify.IsSuccess)
            return ServiceResult<HostedPasswordAuthenticationResult>.Fail(verify.StatusCode, verify.Errors);

        var challenge = verify.Value!;
        var identity = await ResolveUserIdentityAsync(tenantId.Trim(), challenge.SubjectId, cancellationToken);
        if (identity is null)
            return ServiceResult<HostedPasswordAuthenticationResult>.Fail(401, "Conta inválida.");

        return ServiceResult<HostedPasswordAuthenticationResult>.Ok(
            new HostedPasswordAuthenticationResult(
                challenge.SubjectId,
                identity.Value.Email,
                identity.Value.Username,
                Challenge: null,
                MfaMethod: challenge.Method,
                Roles: identity.Value.Roles));
    }

    public async Task<ServiceResult<AuthTokenResponse>> IssueHostedIdentityTokenAsync(
        string subjectId,
        string? mfaMethod,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var identity = await ResolveUserIdentityAsync(tenantId.Trim(), subjectId, cancellationToken);
        if (identity is null)
            return ServiceResult<AuthTokenResponse>.Fail(401, "Conta inválida.");

        return await IssueValidatedIdentityTokenAsync(
            new HostedPasswordAuthenticationResult(
                subjectId,
                identity.Value.Email,
                identity.Value.Username,
                Challenge: null,
                MfaMethod: mfaMethod,
                Roles: identity.Value.Roles),
            tenantId,
            cancellationToken);
    }

    private async Task<ServiceResult<AuthTokenResponse>> IssueValidatedIdentityTokenAsync(
        HostedPasswordAuthenticationResult identity,
        string tenantId,
        CancellationToken cancellationToken)
    {

        var profile = TokenIssuanceProfile.ForPasswordGrant(
            jwtOptions.Value,
            registrationOptions.Value,
            tenantId.Trim(),
            identity.Roles ?? [],
            identity.SubjectId,
            identity.Email,
            identity.Username);

        var claims = string.IsNullOrWhiteSpace(identity.MfaMethod) ? null : CreateMfaClaims(identity.MfaMethod);
        var issued = await IssueTokensAsync(
            tenantId.Trim(),
            profile,
            identity.SubjectId,
            TokenSubjectTypes.User,
            cancellationToken,
            claims);

        return issued.IsSuccess
            ? ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.Issued(issued.Value!), issued.StatusCode, issued.Message)
            : ServiceResult<AuthTokenResponse>.Fail(issued.StatusCode, issued.Errors);
    }

    private async Task<ServiceResult<AuthTokenResponse>> IssueRefreshGrantAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ServiceResult<AuthTokenResponse>.Fail(400, "refresh_token é obrigatório para grant_type=refresh_token.");

        var stored = await refreshTokenRepository.FindActiveByPlainTokenAsync(
            tenantId,
            request.RefreshToken.Trim(),
            cancellationToken);

        if (stored is null)
            return ServiceResult<AuthTokenResponse>.Fail(401, "Refresh token inválido, expirado ou esgotado.");

        var incremented = await refreshTokenRepository.TryIncrementUseCountAsync(tenantId, stored.Id, cancellationToken);
        if (!incremented)
            return ServiceResult<AuthTokenResponse>.Fail(401, "Refresh token inválido, expirado ou esgotado.");

        if (string.Equals(stored.SubjectType, TokenSubjectTypes.User, StringComparison.Ordinal))
        {
            var status = await userAccountWriter.GetUserStatusAsync(tenantId, stored.SubjectId, cancellationToken);
            if (status is not AccountStatuses.Active and not AccountStatuses.Incomplete)
                return ServiceResult<AuthTokenResponse>.Fail(401, "Conta inativa ou email não confirmado.");
        }

        var profile = await ResolveRefreshProfileAsync(tenantId, stored, cancellationToken);
        if (profile is null)
            return ServiceResult<AuthTokenResponse>.Fail(401, "Refresh token inválido.");

        var signingKey = await tenantSigningKeyResolver.ResolveSigningKeyAsync(tenantId, cancellationToken);
        var response = accessTokenIssuer.CreateAccessToken(stored.SubjectId, tenantId, profile, signingKey);
        return ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.Issued(response with { RefreshToken = request.RefreshToken.Trim() }));
    }

    private async Task<ServiceResult<AuthTokenResponse>> IssueClientCredentialsGrantAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            return ServiceResult<AuthTokenResponse>.Fail(400, "client_id e client_secret são obrigatórios para grant_type=client_credentials.");

        var client = await oauthClientRepository.FindByClientIdAsync(
            tenantId,
            request.ClientId.Trim(),
            cancellationToken);

        if (client is null || !passwordHasher.Verify(request.ClientSecret, client.ClientSecretHash))
            return ServiceResult<AuthTokenResponse>.Fail(401, "Credenciais de cliente inválidas.");

        var profile = TokenIssuanceProfile.FromOAuthClient(client);
        var issued = await IssueTokensAsync(tenantId, profile, client.ClientId, TokenSubjectTypes.Client, cancellationToken);
        return issued.IsSuccess
            ? ServiceResult<AuthTokenResponse>.Ok(AuthTokenResponse.Issued(issued.Value!), issued.StatusCode, issued.Message)
            : ServiceResult<AuthTokenResponse>.Fail(issued.StatusCode, issued.Errors);
    }

    private async Task<TokenIssuanceProfile?> ResolveRefreshProfileAsync(
        string tenantId,
        Domain.Entities.RefreshToken stored,
        CancellationToken cancellationToken)
    {
        if (string.Equals(stored.SubjectType, TokenSubjectTypes.Client, StringComparison.Ordinal))
        {
            var client = await oauthClientRepository.FindByClientIdAsync(tenantId, stored.SubjectId, cancellationToken);
            return client is null ? null : TokenIssuanceProfile.FromOAuthClient(client);
        }

        var identity = await ResolveUserIdentityAsync(tenantId, stored.SubjectId, cancellationToken);
        return TokenIssuanceProfile.ForPasswordGrant(
            jwtOptions.Value,
            registrationOptions.Value,
            tenantId,
            stored.Roles,
            stored.SubjectId,
            identity?.Email,
            identity?.Username);
    }

    private async Task<(string Email, string Username, IReadOnlyList<string> Roles)?> ResolveUserIdentityAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        var json = await userAccountWriter.GetUserDocumentJsonAsync(tenantId, userId, cancellationToken);
        if (json is null || JsonNode.Parse(json) is not JsonObject obj)
            return null;

        var email = obj["email"] is JsonValue e && e.TryGetValue<string>(out var ev) ? ev : string.Empty;
        var username = obj["username"] is JsonValue u && u.TryGetValue<string>(out var uv) ? uv : string.Empty;
        var status = obj["status"] is JsonValue s && s.TryGetValue<string>(out var sv) ? sv : AccountStatuses.Active;
        if (status is not AccountStatuses.Active and not AccountStatuses.Incomplete)
            return null;

        var roles = ReadRoles(obj);
        return (email, username, roles);
    }

    private async Task<ServiceResult<IssueTokenResponse>> IssueTokensAsync(
        string tenantId,
        TokenIssuanceProfile profile,
        string subjectId,
        string subjectType,
        CancellationToken cancellationToken,
        IEnumerable<Claim>? additionalClaims = null)
    {
        var signingKey = await tenantSigningKeyResolver.ResolveSigningKeyAsync(tenantId, cancellationToken);
        var access = accessTokenIssuer.CreateAccessToken(subjectId, tenantId, profile, signingKey, additionalClaims);

        var refreshDays = profile.RefreshTokenExpirationDays > 0 ? profile.RefreshTokenExpirationDays : 30;
        var maxUses = profile.MaxRefreshTokenUses > 0 ? profile.MaxRefreshTokenUses : 1;

        var (plainRefresh, _) = await refreshTokenRepository.CreateAsync(
            tenantId,
            subjectId,
            subjectType,
            profile.Roles,
            profile.Scopes,
            DateTime.UtcNow.AddDays(refreshDays),
            maxUses,
            cancellationToken);

        return ServiceResult<IssueTokenResponse>.Ok(access with { RefreshToken = plainRefresh });
    }

    private async Task<bool> RequiresTwoFactorAsync(
        string tenantId,
        CredentialValidationResult credentials,
        CancellationToken cancellationToken)
    {
        var config = await ResolveUserAuthTwoFactorConfigAsync(tenantId, credentials.SchemaId, cancellationToken);
        var mode = config?.Mode?.Trim().ToLowerInvariant() ?? AccountAuthTwoFactorModes.Disabled;

        if (mode == AccountAuthTwoFactorModes.Disabled)
            return false;

        var defaultMethod = config?.DefaultMethod?.Trim().ToLowerInvariant() ?? AccountAuthTwoFactorMethods.EmailCode;
        if (defaultMethod != AccountAuthTwoFactorMethods.EmailCode)
            return false;

        if (mode == AccountAuthTwoFactorModes.Required)
            return true;

        var settings = await twoFactorSettingsService.GetUserSettingsAsync(tenantId, credentials.UserId, cancellationToken);
        return settings.Enabled && settings.EmailEnabled;
    }

    private async Task<AccountAuthTwoFactorConfig?> ResolveUserAuthTwoFactorConfigAsync(
        string tenantId,
        string? schemaId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(schemaId))
            return null;

        var schema = await schemaRepository.GetByIdAsync(tenantId, schemaId.Trim(), cancellationToken)
            ?? await schemaRepository.GetByKeyAsync(tenantId, schemaId.Trim(), cancellationToken);

        return schema?.GetConfig().Auth?.TwoFactor;
    }

    private static IReadOnlyList<string> ReadRoles(JsonObject obj)
    {
        if (obj["roles"] is JsonArray arr)
        {
            var raw = arr
                .OfType<JsonValue>()
                .Select(v => v.TryGetValue<string>(out var s) ? s : null);
            return UserRoles.NormalizeAccountRoles(raw);
        }

        if (obj["role"] is JsonValue role && role.TryGetValue<string>(out var value))
            return UserRoles.NormalizeAccountRoles([value]);

        return UserRoles.NormalizeAccountRoles(null);
    }

    private static IReadOnlyList<Claim> CreateMfaClaims(string method) =>
    [
        new("amr", "pwd"),
        new("amr", "mfa"),
        new("auth_time", DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString()),
        new("mfa_method", method)
    ];
}
