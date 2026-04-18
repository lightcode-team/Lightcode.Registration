namespace Lightcode.Registration.Application.Contracts.Auth;

/// <summary>Resultado positivo da validação de credenciais no login.</summary>
public sealed record CredentialValidationResult(string UserId, IReadOnlyList<string> Roles);
