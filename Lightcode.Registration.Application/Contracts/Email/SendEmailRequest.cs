namespace Lightcode.Registration.Application.Contracts.Email;

public sealed record SendEmailRequest(
    string To,
    string? TemplateId,
    string? TemplateKey,
    Dictionary<string, string>? Parameters);
