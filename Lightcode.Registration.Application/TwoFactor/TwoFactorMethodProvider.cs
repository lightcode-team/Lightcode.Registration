using Lightcode.Registration.Application.Abstractions;

namespace Lightcode.Registration.Application.TwoFactor;

public sealed class TwoFactorMethodProvider(IEnumerable<ITwoFactorMethod> methods) : ITwoFactorMethodProvider
{
    public ITwoFactorMethod GetRequired(string method)
    {
        var resolved = methods.FirstOrDefault(x => string.Equals(x.Method, method, StringComparison.OrdinalIgnoreCase));
        return resolved ?? throw new InvalidOperationException($"Método 2FA não registrado: {method}.");
    }
}
