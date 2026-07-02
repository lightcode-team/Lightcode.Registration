using System.Net.Mail;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Email;
using Lightcode.Registration.Application.Contracts.OAuthClients;
using Lightcode.Registration.Application.OAuthClients;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Services;

public sealed class OAuthClientAppService(
    IOAuthClientRepository repository,
    IPasswordHasher passwordHasher,
    ISecureTokenGenerator tokenGenerator,
    IEmailEnqueuePublisher emailEnqueuePublisher) : IOAuthClientAppService
{
    private const string ClientCredentialsTemplateKey = "client-credentials-secret";

    public async Task<ServiceResult<IReadOnlyList<OAuthClientDto>>> ListAsync(
        string tenantId,
        CancellationToken cancellationToken = default)
    {
        var clients = await repository.ListAsync(tenantId, cancellationToken);
        return ServiceResult<IReadOnlyList<OAuthClientDto>>.Ok(clients.Select(OAuthClientMapping.ToDto).ToList());
    }

    public async Task<ServiceResult<OAuthClientDto>> GetByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await repository.FindByClientIdAsync(tenantId, clientId, cancellationToken);
        return client is null
            ? ServiceResult<OAuthClientDto>.Fail(404, "Cliente OAuth não encontrado.")
            : ServiceResult<OAuthClientDto>.Ok(OAuthClientMapping.ToDto(client));
    }

    public async Task<ServiceResult<OAuthClientDto>> GetByIdAsync(
        string tenantId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var client = await repository.GetByIdAsync(tenantId, id, cancellationToken);
        return client is null
            ? ServiceResult<OAuthClientDto>.Fail(404, "Cliente OAuth não encontrado.")
            : ServiceResult<OAuthClientDto>.Ok(OAuthClientMapping.ToDto(client));
    }

    public async Task<ServiceResult<OAuthClientCreatedDto>> CreateAsync(
        string tenantId,
        CreateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var config = OAuthClientMapping.ToEntity(request.TokenConfig);
        var errors = OAuthClientTokenConfigurationValidator.Validate(config).ToList();
        var redirectUris = OAuthRedirectUriValidator.Normalize(request.RedirectUris);
        var postLogoutRedirectUris = OAuthRedirectUriValidator.Normalize(request.PostLogoutRedirectUris);
        var allowedScopes = OAuthScopeValidator.Normalize(request.AllowedScopes);
        errors.AddRange(redirectUris.Errors);
        errors.AddRange(postLogoutRedirectUris.Errors);
        errors.AddRange(allowedScopes.Errors);
        if (errors.Count > 0)
            return ServiceResult<OAuthClientCreatedDto>.Fail(400, errors);

        var notifyEmail = request.NotifyEmail?.Trim();
        if (!string.IsNullOrWhiteSpace(notifyEmail) && !IsValidEmail(notifyEmail))
            return ServiceResult<OAuthClientCreatedDto>.Fail(400, "notifyEmail inválido.");

        var now = DateTime.UtcNow;
        var clientId = $"client_{Guid.NewGuid():N}";
        var plainSecret = tokenGenerator.GenerateClientSecret();

        var entity = new OAuthClient
        {
            Id = Guid.NewGuid().ToString("N"),
            ClientId = clientId,
            ClientSecretHash = passwordHasher.Hash(plainSecret),
            DisplayName = request.DisplayName?.Trim(),
            NotifyEmail = notifyEmail,
            TokenConfig = config,
            RedirectUris = redirectUris.Values.ToList(),
            PostLogoutRedirectUris = postLogoutRedirectUris.Values.ToList(),
            AllowedScopes = allowedScopes.Values.ToList(),
            RequireConsent = request.RequireConsent,
            Active = true,
            CreatedAtUtc = now,
            UpdatedAtUtc = now
        };

        await repository.InsertAsync(tenantId, entity, cancellationToken);

        if (!string.IsNullOrWhiteSpace(notifyEmail))
        {
            await emailEnqueuePublisher.PublishSendAsync(
                new EmailDispatchQueueMessage(
                    tenantId,
                    TemplateId: null,
                    TemplateKey: ClientCredentialsTemplateKey,
                    To: notifyEmail,
                    Parameters: new Dictionary<string, string>(StringComparer.Ordinal)
                    {
                        ["tenantName"] = tenantId,
                        ["tenantId"] = tenantId,
                        ["clientId"] = clientId,
                        ["clientSecret"] = plainSecret
                    }),
                cancellationToken);
        }

        return ServiceResult<OAuthClientCreatedDto>.Ok(
            new OAuthClientCreatedDto(
                entity.Id,
                entity.ClientId,
                plainSecret,
                entity.DisplayName,
                entity.NotifyEmail,
                OAuthClientMapping.ToConfigDto(entity.TokenConfig),
                entity.RedirectUris,
                entity.PostLogoutRedirectUris,
                entity.AllowedScopes,
                entity.RequireConsent,
                entity.Active,
                entity.CreatedAtUtc,
                entity.UpdatedAtUtc));
    }

    public async Task<ServiceResult<OAuthClientDto>> UpdateByClientIdAsync(
        string tenantId,
        string clientId,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await repository.FindByClientIdAsync(tenantId, clientId, cancellationToken);
        if (client is null)
            return ServiceResult<OAuthClientDto>.Fail(404, "Cliente OAuth não encontrado.");

        return await UpdateAsync(tenantId, client, request, cancellationToken);
    }

    public async Task<ServiceResult<OAuthClientDto>> UpdateByIdAsync(
        string tenantId,
        string id,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken = default)
    {
        var client = await repository.GetByIdAsync(tenantId, id, cancellationToken);
        if (client is null)
            return ServiceResult<OAuthClientDto>.Fail(404, "Cliente OAuth não encontrado.");

        return await UpdateAsync(tenantId, client, request, cancellationToken);
    }

    public async Task<ServiceResult<bool>> DeactivateByClientIdAsync(
        string tenantId,
        string clientId,
        CancellationToken cancellationToken = default)
    {
        var client = await repository.FindByClientIdAsync(tenantId, clientId, cancellationToken);
        if (client is null)
            return ServiceResult<bool>.Fail(404, "Cliente OAuth não encontrado.");

        var deactivated = await repository.DeactivateAsync(tenantId, client.Id, cancellationToken);
        return deactivated
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Fail(404, "Cliente OAuth não encontrado.");
    }

    public async Task<ServiceResult<bool>> DeactivateByIdAsync(
        string tenantId,
        string id,
        CancellationToken cancellationToken = default)
    {
        var deactivated = await repository.DeactivateAsync(tenantId, id, cancellationToken);
        return deactivated
            ? ServiceResult<bool>.Ok(true)
            : ServiceResult<bool>.Fail(404, "Cliente OAuth não encontrado.");
    }

    private async Task<ServiceResult<OAuthClientDto>> UpdateAsync(
        string tenantId,
        OAuthClient client,
        UpdateOAuthClientRequest request,
        CancellationToken cancellationToken)
    {
        var config = OAuthClientMapping.ToEntity(request.TokenConfig);
        var errors = OAuthClientTokenConfigurationValidator.Validate(config).ToList();
        var redirectUris = OAuthRedirectUriValidator.Normalize(request.RedirectUris ?? client.RedirectUris);
        var postLogoutRedirectUris = OAuthRedirectUriValidator.Normalize(request.PostLogoutRedirectUris ?? client.PostLogoutRedirectUris);
        var allowedScopes = OAuthScopeValidator.Normalize(request.AllowedScopes ?? client.AllowedScopes);
        errors.AddRange(redirectUris.Errors);
        errors.AddRange(postLogoutRedirectUris.Errors);
        errors.AddRange(allowedScopes.Errors);
        if (errors.Count > 0)
            return ServiceResult<OAuthClientDto>.Fail(400, errors);

        var notifyEmail = request.NotifyEmail?.Trim();
        if (!string.IsNullOrWhiteSpace(notifyEmail) && !IsValidEmail(notifyEmail))
            return ServiceResult<OAuthClientDto>.Fail(400, "notifyEmail inválido.");

        client.DisplayName = request.DisplayName?.Trim();
        client.NotifyEmail = notifyEmail;
        client.TokenConfig = config;
        client.RedirectUris = redirectUris.Values.ToList();
        client.PostLogoutRedirectUris = postLogoutRedirectUris.Values.ToList();
        client.AllowedScopes = allowedScopes.Values.ToList();
        client.RequireConsent = request.RequireConsent ?? client.RequireConsent;
        client.UpdatedAtUtc = DateTime.UtcNow;

        await repository.ReplaceAsync(tenantId, client, cancellationToken);
        return ServiceResult<OAuthClientDto>.Ok(OAuthClientMapping.ToDto(client));
    }

    private static bool IsValidEmail(string email)
    {
        try
        {
            _ = new MailAddress(email);
            return email.Contains('@', StringComparison.Ordinal);
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
