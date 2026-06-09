using System.Text.Json.Nodes;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Accounts;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Configuration;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Microsoft.Extensions.Options;

namespace Lightcode.Registration.Application.Services;

public sealed class AuthenticationAppService(
    ITenantLookup tenantLookup,
    IUserCredentialValidator credentialValidator,
    IUserAccountWriter userAccountWriter,
    IOAuthClientRepository oauthClientRepository,
    IRefreshTokenRepository refreshTokenRepository,
    IAccessTokenIssuer accessTokenIssuer,
    IPasswordHasher passwordHasher,
    IOptions<JwtOptions> jwtOptions) : IAuthenticationAppService
{
    public async Task<ServiceResult<IssueTokenResponse>> IssueTokenAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return ServiceResult<IssueTokenResponse>.Fail(400, "O cabeçalho do tenant (X-Tenant-Id) é obrigatório.");

        var tenant = await tenantLookup.FindActiveByIdAsync(tenantId.Trim(), cancellationToken);
        if (tenant is null)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Tenant não encontrado ou inativo.");

        var grantType = ResolveGrantType(request);
        return grantType switch
        {
            TokenGrantTypes.Password => await IssuePasswordGrantAsync(request, tenant.Id, cancellationToken),
            TokenGrantTypes.RefreshToken => await IssueRefreshGrantAsync(request, tenant.Id, cancellationToken),
            TokenGrantTypes.ClientCredentials => await IssueClientCredentialsGrantAsync(request, tenant.Id, cancellationToken),
            _ => ServiceResult<IssueTokenResponse>.Fail(400, $"grant_type inválido ou não suportado: '{request.GrantType}'.")
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

    private async Task<ServiceResult<IssueTokenResponse>> IssuePasswordGrantAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Username) || string.IsNullOrWhiteSpace(request.Password))
            return ServiceResult<IssueTokenResponse>.Fail(400, "Username e password são obrigatórios para grant_type=password.");

        var normalizedUsername = request.Username.Trim().ToLowerInvariant();
        var outcome = await credentialValidator.ValidateAsync(
            tenantId,
            normalizedUsername,
            request.Password,
            cancellationToken);

        if (!outcome.IsSuccess)
        {
            return outcome.Failure switch
            {
                CredentialValidationFailure.EmailNotConfirmed =>
                    ServiceResult<IssueTokenResponse>.Fail(403, "Confirme o email antes de entrar."),
                _ => ServiceResult<IssueTokenResponse>.Fail(401, "Credenciais inválidas.")
            };
        }

        var credentials = outcome.Success!;
        var profile = TokenIssuanceProfile.ForPasswordGrant(
            jwtOptions.Value,
            tenantId,
            credentials.Roles,
            credentials.UserId,
            credentials.Email,
            credentials.Username);
        return await IssueTokensAsync(tenantId, profile, credentials.UserId, TokenSubjectTypes.User, cancellationToken);
    }

    private async Task<ServiceResult<IssueTokenResponse>> IssueRefreshGrantAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
            return ServiceResult<IssueTokenResponse>.Fail(400, "refresh_token é obrigatório para grant_type=refresh_token.");

        var stored = await refreshTokenRepository.FindActiveByPlainTokenAsync(
            tenantId,
            request.RefreshToken.Trim(),
            cancellationToken);

        if (stored is null)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Refresh token inválido, expirado ou esgotado.");

        var incremented = await refreshTokenRepository.TryIncrementUseCountAsync(tenantId, stored.Id, cancellationToken);
        if (!incremented)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Refresh token inválido, expirado ou esgotado.");

        if (string.Equals(stored.SubjectType, TokenSubjectTypes.User, StringComparison.Ordinal))
        {
            var status = await userAccountWriter.GetUserStatusAsync(tenantId, stored.SubjectId, cancellationToken);
            if (status is not AccountStatuses.Active and not AccountStatuses.Incomplete)
                return ServiceResult<IssueTokenResponse>.Fail(401, "Conta inativa ou email não confirmado.");
        }

        var profile = await ResolveRefreshProfileAsync(tenantId, stored, cancellationToken);
        if (profile is null)
            return ServiceResult<IssueTokenResponse>.Fail(401, "Refresh token inválido.");

        var response = accessTokenIssuer.CreateAccessToken(stored.SubjectId, tenantId, profile);
        return ServiceResult<IssueTokenResponse>.Ok(response with { RefreshToken = request.RefreshToken.Trim() });
    }

    private async Task<ServiceResult<IssueTokenResponse>> IssueClientCredentialsGrantAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ClientId) || string.IsNullOrWhiteSpace(request.ClientSecret))
            return ServiceResult<IssueTokenResponse>.Fail(400, "client_id e client_secret são obrigatórios para grant_type=client_credentials.");

        var client = await oauthClientRepository.FindByClientIdAsync(
            tenantId,
            request.ClientId.Trim(),
            cancellationToken);

        if (client is null || !passwordHasher.Verify(request.ClientSecret, client.ClientSecretHash))
            return ServiceResult<IssueTokenResponse>.Fail(401, "Credenciais de cliente inválidas.");

        var profile = TokenIssuanceProfile.FromOAuthClient(client);
        return await IssueTokensAsync(tenantId, profile, client.ClientId, TokenSubjectTypes.Client, cancellationToken);
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

        var identity = await TryResolveUserIdentityAsync(tenantId, stored.SubjectId, cancellationToken);
        return TokenIssuanceProfile.ForPasswordGrant(
            jwtOptions.Value,
            tenantId,
            stored.Roles,
            stored.SubjectId,
            identity?.Email,
            identity?.Username);
    }

    private async Task<(string Email, string Username)?> TryResolveUserIdentityAsync(
        string tenantId,
        string userId,
        CancellationToken cancellationToken)
    {
        var json = await userAccountWriter.GetUserDocumentJsonAsync(tenantId, userId, cancellationToken);
        if (json is null || JsonNode.Parse(json) is not JsonObject obj)
            return null;

        var email = obj["email"] is JsonValue e && e.TryGetValue<string>(out var ev) ? ev : string.Empty;
        var username = obj["username"] is JsonValue u && u.TryGetValue<string>(out var uv) ? uv : string.Empty;
        return (email, username);
    }

    private async Task<ServiceResult<IssueTokenResponse>> IssueTokensAsync(
        string tenantId,
        TokenIssuanceProfile profile,
        string subjectId,
        string subjectType,
        CancellationToken cancellationToken)
    {
        var access = accessTokenIssuer.CreateAccessToken(subjectId, tenantId, profile);

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
}
