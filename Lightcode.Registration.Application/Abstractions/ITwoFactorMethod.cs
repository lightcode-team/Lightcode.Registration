using Lightcode.Registration.Application.TwoFactor;

namespace Lightcode.Registration.Application.Abstractions;

public interface ITwoFactorMethod
{
    string Method { get; }

    Task SendAsync(
        TwoFactorChallengeSubject subject,
        string code,
        string purpose,
        CancellationToken cancellationToken = default);
}
