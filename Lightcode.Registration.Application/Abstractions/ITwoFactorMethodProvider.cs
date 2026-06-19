namespace Lightcode.Registration.Application.Abstractions;

public interface ITwoFactorMethodProvider
{
    ITwoFactorMethod GetRequired(string method);
}
