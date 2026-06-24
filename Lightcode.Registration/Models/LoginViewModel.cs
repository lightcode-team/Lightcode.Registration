using System.ComponentModel.DataAnnotations;
using Lightcode.Registration.Application.Contracts.Frontend;

namespace Lightcode.Registration.Models;

public sealed class LoginViewModel
{
    public string? TransactionId { get; set; }

    public string? TenantId { get; set; }

    public FrontConfigDto FrontConfig { get; set; } = FrontConfigDefaults.Create();

    [Required(ErrorMessage = FrontConfigDefaults.UsernameRequired)]
    [Display(Name = FrontConfigDefaults.UsernameLabel)]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = FrontConfigDefaults.PasswordRequired)]
    [DataType(DataType.Password)]
    [Display(Name = FrontConfigDefaults.PasswordLabel)]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}
