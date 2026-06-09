namespace Lightcode.Registration.Application.Security;

public static class TokenGrantTypes
{
    public const string Password = "password";
    public const string RefreshToken = "refresh_token";
    public const string ClientCredentials = "client_credentials";
}

public static class TokenSubjectTypes
{
    public const string User = "user";
    public const string Client = "client";
}
