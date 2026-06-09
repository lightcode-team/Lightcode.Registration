namespace Lightcode.Registration.Application.Contracts.Accounts;

public sealed record ForgotPasswordRequest(string? Email, string? Username);
