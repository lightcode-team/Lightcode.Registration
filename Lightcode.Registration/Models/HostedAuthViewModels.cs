using System.ComponentModel.DataAnnotations;
using Lightcode.Registration.Application.Contracts.Frontend;

namespace Lightcode.Registration.Models;

public sealed class TwoFactorViewModel
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o código de verificação.")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "Informe os seis dígitos do código.")]
    public string Code { get; set; } = string.Empty;

    public string DestinationHint { get; set; } = string.Empty;
    public string VerificationType { get; set; } = "email_code";
    public FrontConfigDto FrontConfig { get; set; } = FrontConfigDefaults.Create();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}

public sealed class ForgotPasswordViewModel
{
    [Required]
    public string TransactionId { get; set; } = string.Empty;

    [Required(ErrorMessage = "Informe o username ou e-mail.")]
    public string Identifier { get; set; } = string.Empty;

    public FrontConfigDto FrontConfig { get; set; } = FrontConfigDefaults.Create();
    public string? ErrorMessage { get; set; }
    public string? SuccessMessage { get; set; }
}
