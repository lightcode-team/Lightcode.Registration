using Lightcode.Registration.Api;
using Lightcode.Registration.Application.Abstractions;
using Lightcode.Registration.Application.Contracts.Auth;
using Lightcode.Registration.Application.Security;
using Lightcode.Registration.Http;
using Lightcode.Registration.AspNetCore.Security;
using Lightcode.Registration.Models;
using Lightcode.Registration.Domain.Entities;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Lightcode.Registration.Controllers;

public sealed class AuthController(
    IAuthenticationAppService authenticationAppService,
    IHostedAuthenticationAppService hostedAuthenticationAppService,
    IAccountPasswordResetAppService passwordResetAppService,
    IFrontConfigAppService frontConfigAppService,
    HumanAuthRateLimiter rateLimiter) : BaseController( )
{
    [HttpGet("/auth/login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login(
        [FromQuery(Name = "response_type")] string? responseType,
        [FromQuery(Name = "tenant_id")] string? tenantId,
        [FromQuery(Name = "client_id")] string? clientId,
        [FromQuery(Name = "redirect_uri")] string? redirectUri,
        [FromQuery] string? scope,
        [FromQuery] string? state,
        [FromQuery] string? nonce,
        [FromQuery(Name = "code_challenge")] string? codeChallenge,
        [FromQuery(Name = "code_challenge_method")] string? codeChallengeMethod,
        [FromQuery] string? transactionId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(transactionId))
        {
            var existing = await hostedAuthenticationAppService.GetActiveAsync(transactionId, cancellationToken);
            var session = await hostedAuthenticationAppService.GetActiveSessionAsync(transactionId, cancellationToken);
            if (existing is not null && session?.Stage == HostedAuthStages.Login)
                return View("~/Views/Auth/Login.cshtml", await CreateLoginModelAsync(existing, cancellationToken));
        }

        var started = await hostedAuthenticationAppService.StartAsync(
            new HostedAuthorizationRequest(
                responseType,
                tenantId,
                clientId,
                redirectUri,
                state,
                nonce,
                scope,
                codeChallenge,
                codeChallengeMethod),
            cancellationToken);
        if (!started.IsSuccess)
        {
            return View("~/Views/Auth/Login.cshtml", new LoginViewModel
            {
                TenantId = tenantId?.Trim(),
                FrontConfig = await frontConfigAppService.ResolveAsync(tenantId, cancellationToken),
                ErrorMessage = string.Join(' ', started.Errors)
            });
        }

        return View("~/Views/Auth/Login.cshtml", await CreateLoginModelAsync(started.Value!, cancellationToken));
    }

    [HttpPost("/auth/login")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Login(LoginViewModel model, CancellationToken cancellationToken)
    {
        var transaction = await hostedAuthenticationAppService.GetActiveAsync(model.TransactionId ?? string.Empty, cancellationToken);
        if (transaction is null)
            return View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken));

        model.TenantId = transaction.TenantId;
        model.FrontConfig = await frontConfigAppService.ResolveAsync(transaction.TenantId, cancellationToken);

        if (!ModelState.IsValid)
            return View("~/Views/Auth/Login.cshtml", model);

        if (rateLimiter.LimitPasswordGrant(HttpContext, transaction.TenantId, model.Username) is not null)
        {
            model.Password = string.Empty;
            model.ErrorMessage = "Muitas tentativas. Tente novamente mais tarde.";
            return View("~/Views/Auth/Login.cshtml", model);
        }

        var result = await hostedAuthenticationAppService.LoginAsync(
            transaction.Id,
            model.Username,
            model.Password,
            cancellationToken);
        model.Password = string.Empty;
        if (!result.IsSuccess)
        {
            model.ErrorMessage = string.Join(' ', result.Errors);
            return View("~/Views/Auth/Login.cshtml", model);
        }

        if (result.Value!.Completed)
            return Redirect(result.Value.RedirectUrl!);

        return RedirectToAction(nameof(TwoFactor), new { transactionId = transaction.Id });
    }

    [HttpGet("/auth/2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> TwoFactor([FromQuery] string? transactionId, CancellationToken cancellationToken)
    {
        var model = await CreateTwoFactorModelAsync(transactionId, cancellationToken);
        return model is null
            ? View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken))
            : View("~/Views/Auth/TwoFactor.cshtml", model);
    }

    [HttpPost("/auth/2fa")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> TwoFactor(TwoFactorViewModel model, CancellationToken cancellationToken)
    {
        var transaction = await hostedAuthenticationAppService.GetActiveAsync(model.TransactionId, cancellationToken);
        var session = await hostedAuthenticationAppService.GetActiveSessionAsync(model.TransactionId, cancellationToken);
        if (transaction is null || session is null)
            return View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken));

        model.FrontConfig = await frontConfigAppService.ResolveAsync(transaction.TenantId, cancellationToken);
        model.DestinationHint = session.DestinationHint ?? string.Empty;
        model.VerificationType = session.VerificationType ?? "email_code";
        if (!ModelState.IsValid)
            return View("~/Views/Auth/TwoFactor.cshtml", model);

        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "hosted_auth_confirm_2fa",
                transaction.TenantId,
                session.ChallengeId) is not null)
        {
            model.Code = string.Empty;
            model.ErrorMessage = "Muitas tentativas. Tente novamente mais tarde.";
            return View("~/Views/Auth/TwoFactor.cshtml", model);
        }

        var result = await hostedAuthenticationAppService.ConfirmTwoFactorAsync(
            transaction.Id,
            model.Code,
            cancellationToken);
        model.Code = string.Empty;
        if (!result.IsSuccess)
        {
            model.ErrorMessage = string.Join(' ', result.Errors);
            return View("~/Views/Auth/TwoFactor.cshtml", model);
        }

        return Redirect(result.Value!.RedirectUrl!);
    }

    [HttpPost("/auth/2fa/resend")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResendTwoFactor([FromForm] string? transactionId, CancellationToken cancellationToken)
    {
        var transaction = await hostedAuthenticationAppService.GetActiveAsync(transactionId ?? string.Empty, cancellationToken);
        var session = await hostedAuthenticationAppService.GetActiveSessionAsync(transactionId ?? string.Empty, cancellationToken);
        var model = await CreateTwoFactorModelAsync(transactionId, cancellationToken);
        if (transaction is null || session is null || model is null)
            return View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken));

        if (rateLimiter.LimitTwoFactorResend(HttpContext, transaction.TenantId, session.SubjectId) is not null)
        {
            model.ErrorMessage = "Aguarde antes de solicitar um novo código.";
            return View("~/Views/Auth/TwoFactor.cshtml", model);
        }

        var result = await hostedAuthenticationAppService.ResendTwoFactorAsync(transaction.Id, cancellationToken);
        if (!result.IsSuccess)
            model.ErrorMessage = string.Join(' ', result.Errors);
        else
        {
            model.DestinationHint = result.Value!.DestinationHint;
            model.SuccessMessage = "Enviamos um novo código. O código anterior não é mais válido.";
        }

        return View("~/Views/Auth/TwoFactor.cshtml", model);
    }

    [HttpPost("/auth/2fa/cancel")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTwoFactor([FromForm] string? transactionId, CancellationToken cancellationToken)
    {
        var result = await hostedAuthenticationAppService.CancelTwoFactorAsync(transactionId ?? string.Empty, cancellationToken);
        return result.IsSuccess
            ? RedirectToAction(nameof(Login), new { transactionId = result.Value!.Id })
            : View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken));
    }

    [HttpGet("/auth/forgot-password")]
    [AllowAnonymous]
    public async Task<IActionResult> ForgotPassword([FromQuery] string? transactionId, CancellationToken cancellationToken)
    {
        var transaction = await hostedAuthenticationAppService.GetActiveAsync(transactionId ?? string.Empty, cancellationToken);
        if (transaction is null)
            return View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken));

        return View("~/Views/Auth/ForgotPassword.cshtml", new ForgotPasswordViewModel
        {
            TransactionId = transaction.Id,
            FrontConfig = await frontConfigAppService.ResolveAsync(transaction.TenantId, cancellationToken)
        });
    }

    [HttpPost("/auth/forgot-password")]
    [AllowAnonymous]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ForgotPassword(ForgotPasswordViewModel model, CancellationToken cancellationToken)
    {
        var transaction = await hostedAuthenticationAppService.GetActiveAsync(model.TransactionId, cancellationToken);
        if (transaction is null)
            return View("~/Views/Auth/Login.cshtml", await CreateExpiredLoginModelAsync(cancellationToken));

        model.FrontConfig = await frontConfigAppService.ResolveAsync(transaction.TenantId, cancellationToken);
        if (!ModelState.IsValid)
            return View("~/Views/Auth/ForgotPassword.cshtml", model);

        if (rateLimiter.LimitAccountRecovery(HttpContext, transaction.TenantId, model.Identifier) is not null)
        {
            model.SuccessMessage = "Se a conta existir, você receberá um link para redefinir a senha.";
            return View("~/Views/Auth/ForgotPassword.cshtml", model);
        }

        var isEmail = model.Identifier.Contains('@', StringComparison.Ordinal);
        await passwordResetAppService.ForgotPasswordAsync(
            transaction.TenantId,
            isEmail ? model.Identifier : null,
            isEmail ? null : model.Identifier,
            transaction.Id,
            cancellationToken);
        model.SuccessMessage = "Se a conta existir, você receberá um link para redefinir a senha.";
        return View("~/Views/Auth/ForgotPassword.cshtml", model);
    }

    /// <summary>
    /// Emite JWT e refresh token. Suporta <c>grant_type</c>: <c>password</c>, <c>refresh_token</c>, <c>client_credentials</c>.
    /// Envie o tenant no cabeçalho <see cref="TenantHttpHeaders.TenantId"/> (pedido anónimo, sem JWT).
    /// </summary>
    [HttpPost("/api/auth/token")]
    [AllowAnonymous]
    public async Task<IActionResult> IssueToken([FromBody] TokenRequest body, CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Cabeçalho obrigatório: {TenantHttpHeaders.TenantId}.");

        if (IsPasswordGrant(body)
            && rateLimiter.LimitPasswordGrant(HttpContext, tenantId, body.Username) is { } limited)
            return limited;

        if (string.Equals(body.GrantType?.Trim(), TokenGrantTypes.AuthorizationCode, StringComparison.OrdinalIgnoreCase))
        {
            var exchange = await hostedAuthenticationAppService.ExchangeAuthorizationCodeAsync(body, tenantId, cancellationToken);
            return exchange.ToApiResponse();
        }

        var result = await authenticationAppService.IssueTokenAsync(body, tenantId, cancellationToken);
        return result.ToApiResponse();
    }

    [HttpPost("/api/auth/confirm-2fa")]
    [AllowAnonymous]
    public async Task<IActionResult> ConfirmTwoFactor([FromBody] ConfirmTwoFactorRequest body, CancellationToken cancellationToken)
    {
        var tenantId = TenantHttpHeaders.TryGetTenantId(Request);
        if (tenantId is null)
            return ApiResponse.Error(400, $"Cabeçalho obrigatório: {TenantHttpHeaders.TenantId}.");

        if (rateLimiter.LimitTwoFactorConfirmation(
                HttpContext,
                "auth_confirm_2fa",
                tenantId,
                body.ChallengeId) is { } limited)
            return limited;

        var result = await authenticationAppService.ConfirmTwoFactorAsync(body, tenantId, cancellationToken);
        return result.ToApiResponse();
    }

    private static bool IsPasswordGrant(TokenRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.GrantType))
            return string.Equals(request.GrantType.Trim(), TokenGrantTypes.Password, StringComparison.OrdinalIgnoreCase);

        return !string.IsNullOrWhiteSpace(request.Username) || !string.IsNullOrWhiteSpace(request.Password);
    }

    private async Task<LoginViewModel> CreateLoginModelAsync(
        HostedAuthTransaction transaction,
        CancellationToken cancellationToken) =>
        new()
        {
            TransactionId = transaction.Id,
            TenantId = transaction.TenantId,
            FrontConfig = await frontConfigAppService.ResolveAsync(transaction.TenantId, cancellationToken)
        };

    private async Task<LoginViewModel> CreateExpiredLoginModelAsync(CancellationToken cancellationToken) =>
        new()
        {
            FrontConfig = await frontConfigAppService.ResolveAsync(null, cancellationToken),
            ErrorMessage = "O fluxo de login é inválido ou expirou. Inicie novamente pela aplicação."
        };

    private async Task<TwoFactorViewModel?> CreateTwoFactorModelAsync(
        string? transactionId,
        CancellationToken cancellationToken)
    {
        var session = await hostedAuthenticationAppService.GetActiveSessionAsync(transactionId ?? string.Empty, cancellationToken);
        if (session?.Stage != HostedAuthStages.AwaitingTwoFactor)
            return null;

        return new TwoFactorViewModel
        {
            TransactionId = session.TransactionId,
            DestinationHint = session.DestinationHint ?? string.Empty,
            VerificationType = session.VerificationType ?? "email_code",
            FrontConfig = await frontConfigAppService.ResolveAsync(session.TenantId, cancellationToken)
        };
    }
}
