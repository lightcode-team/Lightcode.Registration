namespace Lightcode.Registration.Application.Registration;

/// <summary>Schema inicial de cadastro (JSON Schema draft, validado com JsonSchema.Net).</summary>
public static class DefaultAccountRegistrationSchema
{
    public const string Json =
        """
        {
          "type": "object",
          "required": ["email", "username", "password"],
          "additionalProperties": true,
          "properties": {
            "email": { "type": "string", "format": "email" },
            "username": { "type": "string", "minLength": 1 },
            "password": { "type": "string", "minLength": 8 }
          }
        }
        """;
}
