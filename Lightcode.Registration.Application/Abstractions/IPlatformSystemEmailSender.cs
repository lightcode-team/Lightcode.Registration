namespace Lightcode.Registration.Application.Abstractions;

public interface IPlatformSystemEmailSender
{
    Task SendTwoFactorCodeAsync(
        string to,
        string username,
        string code,
        string purpose,
        CancellationToken cancellationToken = default);
}
