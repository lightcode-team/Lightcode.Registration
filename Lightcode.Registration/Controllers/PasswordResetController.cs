using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Lightcode.Registration.Controllers;

[AllowAnonymous]
public sealed class PasswordResetController(
    IAccountPasswordResetAppService passwordResetAppService,
    IFrontConfigAppService frontConfigAppService) : Controller
{
    [HttpGet("/reset-password")]
    public async Task<IActionResult> ResetPassword(
        [FromQuery] string? token,
        [FromQuery] string? tenantId,
        [FromQuery] string? email,
        [FromQuery] string? transactionId,
        CancellationToken cancellationToken)
    {
        var frontConfig = await frontConfigAppService.ResolveAsync(tenantId, cancellationToken);
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(email))
        {
            return View(new ResetPasswordViewModel
            {
                FrontConfig = frontConfig,
                TransactionId = transactionId,
                ErrorMessage = "Link inválido ou incompleto."
            });
        }

        return View(new ResetPasswordViewModel
        {
            FrontConfig = frontConfig,
            TransactionId = transactionId,
            Token = token.Trim(),
            TenantId = tenantId.Trim(),
            Email = email.Trim()
        });
    }

    [HttpPost("/reset-password")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        model.FrontConfig = await frontConfigAppService.ResolveAsync(model.TenantId, cancellationToken);
        if (!ModelState.IsValid)
            return View(model);

        var result = await passwordResetAppService.ResetPasswordAsync(
            model.TenantId,
            model.Email,
            model.Token,
            model.NewPassword,
            cancellationToken);

        if (!result.IsSuccess)
        {
            model.ErrorMessage = string.Join(' ', result.Errors);
            model.NewPassword = string.Empty;
            model.ConfirmPassword = string.Empty;
            return View(model);
        }

        return View(new ResetPasswordViewModel
        {
            FrontConfig = model.FrontConfig,
            TransactionId = model.TransactionId,
            SuccessMessage = result.Message ?? "Senha redefinida com sucesso."
        });
    }
}
