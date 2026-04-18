namespace Lightcode.Registration.Application.Registration;

/// <summary>Schema inicial de cadastro (JSON Schema draft, validado com JsonSchema.Net).</summary>
public static class DefaultAccountRegistrationSchema
{
    public const string Json =
        """
        {
          "type": "object",
          "required": ["email", "password"],
          "additionalProperties": true,
          "properties": {
            "email": { "type": "string", "format": "email" },
            "password": { "type": "string", "minLength": 8 }
          }
        }
        """;
}
