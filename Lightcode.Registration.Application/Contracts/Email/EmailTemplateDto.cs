namespace Lightcode.Registration.Application.Contracts.Email;

public sealed record EmailTemplateDto(
    string Id,
    string TenantId,
    string Key,
    string? DisplayName,
    string Subject,
    string HtmlBody,
    string? TextBody,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
