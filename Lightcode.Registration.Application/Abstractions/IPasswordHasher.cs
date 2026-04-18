namespace Lightcode.Registration.Application.Abstractions;

public interface IPasswordHasher
{
    string Hash(string plainTextPassword);
}
