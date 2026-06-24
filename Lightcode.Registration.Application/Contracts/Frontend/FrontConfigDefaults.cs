namespace Lightcode.Registration.Application.Contracts.Frontend;

public static class FrontConfigDefaults
{
    public const string PageTitle = "Login";
    public const string Heading = "Login";
    public const string Subtitle = "Entre com seu username e password.";

    public const string UsernameLabel = "Username";
    public const string UsernamePlaceholder = "Digite seu username";
    public const string UsernameRequired = "Informe o username.";

    public const string PasswordLabel = "Password";
    public const string PasswordPlaceholder = "Digite seu password";
    public const string PasswordRequired = "Informe a senha.";

    public const string SubmitButton = "Entrar";
    public const string SubmittingButton = "Entrando...";
    public const string AuthenticationNotIntegrated = "Login ainda nao integrado ao servico de autenticacao.";
    public const string TwoFactorHeading = "Verificação em duas etapas";
    public const string TwoFactorSubtitle = "Digite o código de seis dígitos enviado para {destination}.";
    public const string ForgotPasswordHeading = "Esqueci minha senha";
    public const string ForgotPasswordSubtitle = "Informe seu username ou e-mail. Se a conta existir, enviaremos as instruções.";
    public const string ResetPasswordHeading = "Redefinir senha";
    public const string ResetPasswordSubtitle = "Defina uma nova senha para sua conta.";

    public static FrontConfigDto Create()
    {
        return new FrontConfigDto
        {
            Messages = new FrontConfigMessagesDto(),
            Css = Css
        };
    }

    public const string Css = """
* {
    box-sizing: border-box;
}

body {
    margin: 0;
    min-height: 100vh;
    display: grid;
    place-items: center;
    padding: 1.5rem;
    font-family: system-ui, -apple-system, Segoe UI, Roboto, sans-serif;
    color: #172033;
    background:
        var(--login-background-image, linear-gradient(135deg, rgba(18, 100, 163, 0.12), transparent 34%)),
        linear-gradient(315deg, rgba(29, 126, 106, 0.14), transparent 38%),
        #f7f8fb;
    background-size: cover, auto, auto;
    background-position: center, center, center;
}

.login-card {
    width: 100%;
    max-width: 400px;
    padding: 2rem;
    background: #ffffff;
    border: 1px solid #e1e7ef;
    border-radius: 8px;
    box-shadow: 0 18px 45px rgba(23, 32, 51, 0.10);
}

.login-logo {
    display: block;
    max-width: 160px;
    max-height: 64px;
    margin: 0 0 1.25rem;
    object-fit: contain;
}

.login-title {
    margin: 0 0 0.4rem;
    font-size: 1.7rem;
    line-height: 1.2;
}

.login-subtitle {
    margin: 0 0 1.5rem;
    color: #64748b;
    font-size: 0.95rem;
}

.field {
    margin-bottom: 1rem;
}

.field label {
    display: block;
    margin-bottom: 0.4rem;
    font-size: 0.9rem;
    font-weight: 650;
    color: #263244;
}

.field input {
    width: 100%;
    min-height: 46px;
    padding: 0.75rem 0.85rem;
    border: 1px solid #cbd5e1;
    border-radius: 8px;
    color: #172033;
    background: #ffffff;
    font-size: 1rem;
}

.field input:focus {
    outline: none;
    border-color: #1264a3;
    box-shadow: 0 0 0 3px rgba(18, 100, 163, 0.16);
}

.field-error {
    display: block;
    min-height: 1rem;
    margin-top: 0.35rem;
    color: #b42318;
    font-size: 0.84rem;
}

.alert {
    margin-bottom: 1rem;
    padding: 0.85rem 1rem;
    border: 1px solid #fecaca;
    border-radius: 8px;
    color: #991b1b;
    background: #fff1f2;
    font-size: 0.92rem;
}

.login-submit {
    width: 100%;
    min-height: 46px;
    margin-top: 0.25rem;
    border: 0;
    border-radius: 8px;
    color: #ffffff;
    background: #1264a3;
    font-size: 1rem;
    font-weight: 700;
    cursor: pointer;
}

.login-submit:hover {
    background: #0f548a;
}

.login-submit:focus-visible {
    outline: 3px solid rgba(18, 100, 163, 0.28);
    outline-offset: 2px;
}

.login-submit:disabled {
    cursor: wait;
    background: #6f8fa8;
}

.password-field {
    position: relative;
}

.password-field input {
    padding-right: 5rem;
}

.password-toggle {
    position: absolute;
    top: 50%;
    right: 0.75rem;
    transform: translateY(-50%);
    border: 0;
    color: #1264a3;
    background: transparent;
    font-weight: 650;
    cursor: pointer;
}

.auth-link,
.auth-link-button {
    color: #1264a3;
    font: inherit;
    font-size: 0.9rem;
    font-weight: 650;
    text-decoration: none;
}

.auth-link:hover,
.auth-link-button:hover {
    text-decoration: underline;
}

.auth-link-right {
    display: block;
    margin: -0.35rem 0 1rem;
    text-align: right;
}

.auth-link-centered {
    display: block;
    margin-top: 1.25rem;
    text-align: center;
}

.auth-actions {
    display: flex;
    justify-content: space-between;
    gap: 1rem;
    margin-top: 1.25rem;
}

.auth-link-button {
    padding: 0;
    border: 0;
    background: transparent;
    cursor: pointer;
}

.auth-link-button:disabled {
    color: #64748b;
    cursor: wait;
    text-decoration: none;
}

.one-time-code {
    text-align: center;
    letter-spacing: 0.5rem;
    font-size: 1.35rem !important;
    font-variant-numeric: tabular-nums;
}

.alert-success {
    border-color: #a7f3d0;
    color: #065f46;
    background: #ecfdf5;
}
""";
}
