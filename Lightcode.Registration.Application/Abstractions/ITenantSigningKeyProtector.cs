namespace Lightcode.Registration.Application.Abstractions;

public interface ITenantSigningKeyProtector
{
    string Protect(string signingKey);

    string Unprotect(string encryptedSigningKey);
}
