using Lightcode.Registration.Application.Common;
using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

public interface IAuthenticationAppService
{
    /// <param name="tenantId">Tenant obtido do cabeçalho HTTP quando o pedido não traz JWT.</param>
    Task<ServiceResult<AuthTokenResponse>> IssueTokenAsync(
        TokenRequest request,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthTokenResponse>> ConfirmTwoFactorAsync(
        ConfirmTwoFactorRequest request,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<HostedPasswordAuthenticationResult>> BeginHostedPasswordAuthenticationAsync(
        string? username,
        string? password,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<HostedPasswordAuthenticationResult>> ConfirmHostedTwoFactorAsync(
        ConfirmTwoFactorRequest request,
        string tenantId,
        CancellationToken cancellationToken = default);

    Task<ServiceResult<AuthTokenResponse>> IssueHostedIdentityTokenAsync(
        string subjectId,
        string? mfaMethod,
        string tenantId,
        CancellationToken cancellationToken = default);
}
