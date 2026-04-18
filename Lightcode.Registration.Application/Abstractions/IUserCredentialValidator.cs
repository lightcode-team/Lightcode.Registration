using Lightcode.Registration.Application.Contracts.Auth;

namespace Lightcode.Registration.Application.Abstractions;

/// <summary>Valida username e password contra a coleção Users do tenant.</summary>
public interface IUserCredentialValidator
{
    Task<CredentialValidationResult?> ValidateAsync(
        string tenantId,
        string username,
        string password,
        CancellationToken cancellationToken = default);
}
