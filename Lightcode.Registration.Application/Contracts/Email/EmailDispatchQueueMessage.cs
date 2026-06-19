namespace Lightcode.Registration.Application.Contracts.Email;

/// <summary>Mensagem publicada na fila de envio de emails.</summary>
public sealed record EmailDispatchQueueMessage(
    string TenantId,
    string? TemplateId,
    string? TemplateKey,
    string To,
    Dictionary<string, string>? Parameters,
    bool SystemEmail = false,
    string? Subject = null,
    string? HtmlBody = null,
    string? TextBody = null);
