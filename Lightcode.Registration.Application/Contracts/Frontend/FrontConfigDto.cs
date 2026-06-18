namespace Lightcode.Registration.Application.Contracts.Frontend;

public sealed class FrontConfigDto
{
    public FrontConfigMessagesDto Messages { get; init; } = new();

    public string Css { get; init; } = string.Empty;

    public string? LogoUrl { get; init; }

    public string? BackgroundImageUrl { get; init; }
}

public sealed class FrontConfigMessagesDto
{
    public string PageTitle { get; init; } = FrontConfigDefaults.PageTitle;

    public string Heading { get; init; } = FrontConfigDefaults.Heading;

    public string Subtitle { get; init; } = FrontConfigDefaults.Subtitle;

    public string UsernameLabel { get; init; } = FrontConfigDefaults.UsernameLabel;

    public string UsernamePlaceholder { get; init; } = FrontConfigDefaults.UsernamePlaceholder;

    public string UsernameRequired { get; init; } = FrontConfigDefaults.UsernameRequired;

    public string PasswordLabel { get; init; } = FrontConfigDefaults.PasswordLabel;

    public string PasswordPlaceholder { get; init; } = FrontConfigDefaults.PasswordPlaceholder;

    public string PasswordRequired { get; init; } = FrontConfigDefaults.PasswordRequired;

    public string SubmitButton { get; init; } = FrontConfigDefaults.SubmitButton;

    public string SubmittingButton { get; init; } = FrontConfigDefaults.SubmittingButton;

    public string AuthenticationNotIntegrated { get; init; } = FrontConfigDefaults.AuthenticationNotIntegrated;
}
