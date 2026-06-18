using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Frontend;
using Lightcode.Registration.Domain.Entities;

namespace Lightcode.Registration.Application.Services;

public sealed class FrontConfigAppService(IFrontConfigRepository frontConfigRepository) : IFrontConfigAppService
{
    public async Task<FrontConfigDto> ResolveAsync(string? tenantId, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tenantId))
            return FrontConfigDefaults.Create();

        var config = await frontConfigRepository.GetActiveAsync(tenantId.Trim(), cancellationToken);
        return config is null ? FrontConfigDefaults.Create() : MergeWithDefaults(config);
    }

    private static FrontConfigDto MergeWithDefaults(FrontConfig config)
    {
        var fallback = FrontConfigDefaults.Create();
        var messages = config.Messages;

        return new FrontConfigDto
        {
            Messages = new FrontConfigMessagesDto
            {
                PageTitle = ValueOrDefault(messages?.PageTitle, fallback.Messages.PageTitle),
                Heading = ValueOrDefault(messages?.Heading, fallback.Messages.Heading),
                Subtitle = ValueOrDefault(messages?.Subtitle, fallback.Messages.Subtitle),
                UsernameLabel = ValueOrDefault(messages?.UsernameLabel, fallback.Messages.UsernameLabel),
                UsernamePlaceholder = ValueOrDefault(messages?.UsernamePlaceholder, fallback.Messages.UsernamePlaceholder),
                UsernameRequired = ValueOrDefault(messages?.UsernameRequired, fallback.Messages.UsernameRequired),
                PasswordLabel = ValueOrDefault(messages?.PasswordLabel, fallback.Messages.PasswordLabel),
                PasswordPlaceholder = ValueOrDefault(messages?.PasswordPlaceholder, fallback.Messages.PasswordPlaceholder),
                PasswordRequired = ValueOrDefault(messages?.PasswordRequired, fallback.Messages.PasswordRequired),
                SubmitButton = ValueOrDefault(messages?.SubmitButton, fallback.Messages.SubmitButton),
                SubmittingButton = ValueOrDefault(messages?.SubmittingButton, fallback.Messages.SubmittingButton),
                AuthenticationNotIntegrated = ValueOrDefault(
                    messages?.AuthenticationNotIntegrated,
                    fallback.Messages.AuthenticationNotIntegrated)
            },
            Css = ValueOrDefault(config.Css, fallback.Css),
            LogoUrl = NullIfWhiteSpace(config.LogoUrl),
            BackgroundImageUrl = NullIfWhiteSpace(config.BackgroundImageUrl)
        };
    }

    private static string ValueOrDefault(string? value, string fallback) =>
        string.IsNullOrWhiteSpace(value) ? fallback : value;

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
