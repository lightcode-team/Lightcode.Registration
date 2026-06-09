namespace Lightcode.Registration.Application.Contracts.Auth;

public enum CredentialValidationFailure
{
    InvalidCredentials,
    Expired,
    EmailNotConfirmed
}
