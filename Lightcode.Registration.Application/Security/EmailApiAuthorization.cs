namespace Lightcode.Registration.Application.Security;

/// <summary>Roles para a Email API (claims <c>role</c> no JWT).</summary>
public static class EmailApiRoles
{
    public const string TemplateRead = "template-read";
    public const string TemplateWrite = "template-write";
    public const string SendEmail = "send-email";
}

/// <summary>Scopes para a Email API (claims <c>scope</c> no JWT).</summary>
public static class EmailApiScopes
{
    /// <summary>Concede todas as operações da Email API.</summary>
    public const string EmailAdmin = "email-admin";
}

public enum EmailApiPermission
{
    TemplateRead,
    TemplateWrite,
    SendEmail
}

public static class EmailApiPolicyNames
{
    public const string TemplateRead = "EmailTemplateRead";
    public const string TemplateWrite = "EmailTemplateWrite";
    public const string SendEmail = "EmailSend";
}
