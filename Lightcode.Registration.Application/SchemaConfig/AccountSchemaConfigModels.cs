namespace Lightcode.Registration.Application.SchemaConfig;

public sealed class AccountSchemaConfig
{
    public ExpiryConfig? Expiry { get; set; }
}

public sealed class ExpiryConfig
{
    public bool ExpiryRegister { get; set; }

    public int DaysExpiry { get; set; }
}
