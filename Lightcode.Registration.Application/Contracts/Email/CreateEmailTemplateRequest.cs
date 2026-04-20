namespace Lightcode.Registration.Application.Contracts.Email;

public sealed record CreateEmailTemplateRequest(
    string Key,
    string? DisplayName,
    string Subject,
    string HtmlBody,
    string? TextBody);
