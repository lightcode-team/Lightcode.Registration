namespace Lightcode.Registration.Application.Contracts.Auth;

public sealed class CredentialValidationOutcome
{
    private CredentialValidationOutcome(CredentialValidationResult? success, CredentialValidationFailure? failure)
    {
        Success = success;
        Failure = failure;
    }

    public CredentialValidationResult? Success { get; }

    public CredentialValidationFailure? Failure { get; }

    public bool IsSuccess => Success is not null;

    public static CredentialValidationOutcome Succeeded(CredentialValidationResult result) =>
        new(result, null);

    public static CredentialValidationOutcome Failed(CredentialValidationFailure failure) =>
        new(null, failure);
}
