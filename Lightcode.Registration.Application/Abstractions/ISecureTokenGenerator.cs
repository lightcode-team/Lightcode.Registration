namespace Lightcode.Registration.Application.Abstractions;

public interface ISecureTokenGenerator
{
    string GenerateRefreshToken();

    string GenerateClientSecret();
}
