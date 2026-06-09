namespace Lightcode.Registration.Application.Security;

/// <summary>Roles para gestão de clientes OAuth (claims <c>role</c> no JWT).</summary>
public static class OAuthClientsRoles
{
    public const string ClientsRead = "clients-read";
    public const string ClientsWrite = "clients-write";
}

/// <summary>Scopes para gestão de clientes OAuth (claims <c>scope</c> no JWT).</summary>
public static class OAuthClientsScopes
{
    /// <summary>Concede todas as operações de gestão de clientes OAuth (inclui o cliente owner criado no provisionamento).</summary>
    public const string Owner = "owner";
}

public enum OAuthClientsPermission
{
    ClientsRead,
    ClientsWrite
}

public static class OAuthClientsPolicyNames
{
    public const string ClientsRead = "OAuthClientsRead";
    public const string ClientsWrite = "OAuthClientsWrite";
}
