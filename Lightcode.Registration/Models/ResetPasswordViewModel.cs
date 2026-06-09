using System.ComponentModel.DataAnnotations;

namespace Lightcode.Registration.Models;

public sealed class ResetPasswordViewModel
{
    public string Token { get; set; } = default!;

    public string TenantId { get; set; } = default!;

    public string Email { get; set; } = default!;

    [Required(ErrorMessage = "Nova senha é obrigatória.")]
    [MinLength(8, ErrorMessage = "A senha deve ter pelo menos 8 caracteres.")]
    [DataType(DataType.Password)]
    [Display(Name = "Nova senha")]
    public string NewPassword { get; set; } = default!;

    [Required(ErrorMessage = "Confirmação de senha é obrigatória.")]
    [Compare(nameof(NewPassword), ErrorMessage = "As senhas não coincidem.")]
    [DataType(DataType.Password)]
    [Display(Name = "Confirmar nova senha")]
    public string ConfirmPassword { get; set; } = default!;

    public string? SuccessMessage { get; set; }

    public string? ErrorMessage { get; set; }

    public bool IsCompleted => !string.IsNullOrWhiteSpace(SuccessMessage);
}
