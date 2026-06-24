namespace Lightcode.Registration.Domain.Entities;

public sealed class FrontConfig
{
    public const string DefaultId = "default";

    public string Id { get; set; } = DefaultId;

    public bool Active { get; set; } = true;

    public FrontConfigMessages? Messages { get; set; }

    public string? Css { get; set; }

    public string? LogoUrl { get; set; }

    public string? BackgroundImageUrl { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

    public DateTime? UpdatedAtUtc { get; set; }
}

public sealed class FrontConfigMessages
{
    public string? PageTitle { get; set; }

    public string? Heading { get; set; }

    public string? Subtitle { get; set; }

    public string? UsernameLabel { get; set; }

    public string? UsernamePlaceholder { get; set; }

    public string? UsernameRequired { get; set; }

    public string? PasswordLabel { get; set; }

    public string? PasswordPlaceholder { get; set; }

    public string? PasswordRequired { get; set; }

    public string? SubmitButton { get; set; }

    public string? SubmittingButton { get; set; }

    public string? AuthenticationNotIntegrated { get; set; }
    public string? TwoFactorHeading { get; set; }
    public string? TwoFactorSubtitle { get; set; }
    public string? ForgotPasswordHeading { get; set; }
    public string? ForgotPasswordSubtitle { get; set; }
    public string? ResetPasswordHeading { get; set; }
    public string? ResetPasswordSubtitle { get; set; }
}
