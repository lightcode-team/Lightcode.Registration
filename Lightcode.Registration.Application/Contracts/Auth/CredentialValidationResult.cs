namespace Lightcode.Registration.Application.Contracts.Auth;

/// <summary>Resultado positivo da validação de credenciais no login.</summary>
public sealed record CredentialValidationResult(
    string UserId,
    string Email,
    string Username,
    IReadOnlyList<string> Roles);
