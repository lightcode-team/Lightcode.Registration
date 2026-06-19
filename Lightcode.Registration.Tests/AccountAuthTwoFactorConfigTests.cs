using FluentAssertions;
using Lightcode.Registration.Domain.Entities;
using Xunit;

namespace Lightcode.Registration.Tests;

public sealed class AccountAuthTwoFactorConfigTests
{
    [Fact]
    public void TryParseAndValidate_accepts_required_email_code_policy()
    {
        const string json = """
        {
          "auth": {
            "twoFactor": {
              "mode": "required",
              "allowedMethods": ["email_code"],
              "defaultMethod": "email_code"
            }
          }
        }
        """;

        var valid = AccountJsonSchemaConfig.TryParseAndValidate(json, out var config, out var error);

        valid.Should().BeTrue(error);
        config!.Auth!.TwoFactor!.Mode.Should().Be(AccountAuthTwoFactorModes.Required);
    }

    [Fact]
    public void TryParseAndValidate_rejects_unknown_mode()
    {
        const string json = """
        {
          "auth": {
            "twoFactor": {
              "mode": "always",
              "allowedMethods": ["email_code"],
              "defaultMethod": "email_code"
            }
          }
        }
        """;

        var valid = AccountJsonSchemaConfig.TryParseAndValidate(json, out _, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("auth.twoFactor.mode");
    }

    [Fact]
    public void TryParseAndValidate_rejects_default_method_outside_allowed_methods()
    {
        const string json = """
        {
          "auth": {
            "twoFactor": {
              "mode": "optional",
              "allowedMethods": ["email_code"],
              "defaultMethod": "totp"
            }
          }
        }
        """;

        var valid = AccountJsonSchemaConfig.TryParseAndValidate(json, out _, out var error);

        valid.Should().BeFalse();
        error.Should().Contain("defaultMethod");
    }
}
