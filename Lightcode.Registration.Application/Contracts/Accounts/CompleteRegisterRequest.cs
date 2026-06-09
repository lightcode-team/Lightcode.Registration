using System.Text.Json.Serialization;

namespace Lightcode.Registration.Application.Contracts.Accounts;

public sealed record CompleteRegisterRequest(
    [property: JsonPropertyName("confirmationReturnUrl")] string? ConfirmationReturnUrl = null);
