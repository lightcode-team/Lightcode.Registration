using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.TwoFactor;

public sealed class TotpTwoFactorMethod : ITwoFactorMethod
{
    public string Method => TwoFactorMethods.Totp;

    public Task SendAsync(
        TwoFactorChallengeSubject subject,
        string code,
        string purpose,
        CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("TOTP está reservado para uma próxima fase e ainda não envia challenge.");
}
