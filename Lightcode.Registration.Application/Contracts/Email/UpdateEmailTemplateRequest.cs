namespace Lightcode.Registration.Application.Contracts.Email;

public sealed record UpdateEmailTemplateRequest(
    string? DisplayName,
    string? Subject,
    string? HtmlBody,
    string? TextBody);
