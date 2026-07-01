# Plano v1.0.0 - Login Hospedado com Authorization Code

## Objetivo

Documentar a primeira entrega real do login hospedado da Registration API,
implementada no commit `4797481`.

Esta versão cobre:

- entrada por `/auth/login`;
- formulário Razor de login;
- 2FA de usuário final durante a jornada hospedada;
- Authorization Code com PKCE S256;
- troca de code em `/api/auth/token`;
- recuperação de senha dentro da jornada.

Esta versão ainda não possui sessão SSO central reutilizável. `SsoSession`,
cookie `lc_sso`, `prompt`, `max_age` e `/auth/logout` entram na v1.1.0.

## Estado da Codebase no v1.0.0

- `AuthController` expõe `/auth/login`, `/auth/2fa`, `/auth/2fa/resend`,
  `/auth/2fa/cancel`, `/auth/forgot-password` e `/api/auth/token`.
- `HostedAuthenticationAppService` cria transações, sessões curtas e
  authorization codes.
- `OAuthClient` passa a ter `RedirectUris`, `AllowedScopes` e `RequireConsent`.
- `TokenRequest` passa a aceitar `code`, `redirect_uri` e `code_verifier`.
- `TokenGrantTypes.AuthorizationCode` é suportado.
- `AuthenticationAppService` passa a oferecer os métodos usados pelo fluxo
  hospedado sem emitir JWT antes da conclusão da jornada.
- `HostedAuthTransaction`, `HostedAuthSession`, `AuthorizationCodeGrant` e
  `AuthAuditLog` ficam no banco master.

## Decisões Fechadas no v1.0.0

- O login hospedado usa `GET /auth/login`.
- A entrada exige formato de Authorization Request:
  `response_type=code`, `tenant_id`, `client_id`, `redirect_uri`, `state`,
  `code_challenge` e `code_challenge_method=S256`.
- `scope` e `nonce` são aceitos e preservados.
- PKCE S256 é obrigatório.
- `redirect_uri` precisa bater exatamente com `OAuthClient.RedirectUris`.
- `scope` precisa estar autorizado pelo `OAuthClient`.
- O code é opaco, armazenado apenas como hash e tem TTL de 60 segundos.
- O code é consumido atomicamente no exchange.
- O exchange ocorre em `/api/auth/token` com `grant_type=authorization_code`.
- O exchange exige `X-Tenant-Id`.
- O token só é emitido após recarregar usuário/status/roles.
- 2FA, quando exigido, bloqueia a emissão do code até confirmação.
- Não há `id_token`.
- Não há `/connect/*`.
- Não há OpenIddict.
- Não há `SsoClient` separado.

## Contratos Públicos

### `GET /auth/login`

Parâmetros:

- `response_type=code`
- `tenant_id`
- `client_id`
- `redirect_uri`
- `scope`
- `state`
- `nonce`
- `code_challenge`
- `code_challenge_method=S256`

Comportamento:

- valida client ativo no tenant informado;
- valida redirect URI exata;
- valida scope e PKCE;
- cria `HostedAuthTransaction` e `HostedAuthSession`;
- renderiza a view de login do tenant;
- se a request for inválida, mostra erro de configuração sem redirecionar para
  callback não validado.

### `POST /auth/login`

Comportamento:

- valida antiforgery;
- aplica rate limit de login humano;
- valida username/senha no tenant;
- cria challenge se 2FA for exigido;
- sem 2FA, emite authorization code e redireciona para `redirect_uri?code&state`;
- com 2FA, redireciona para `/auth/2fa`.

### `GET|POST /auth/2fa`

Comportamento:

- exibe destino mascarado e tipo de verificação;
- confirma challenge `purpose=login`;
- consome challenge atômico via domínio de 2FA;
- emite authorization code somente depois de MFA válido.

### `POST /auth/2fa/resend`

Comportamento:

- exige sessão em `awaiting_two_factor`;
- respeita cooldown de 30 segundos;
- cria novo challenge e substitui o challenge ativo.

### `POST /auth/2fa/cancel`

Comportamento:

- invalida challenge pendente;
- limpa subject e dados de 2FA da `HostedAuthSession`;
- retorna para `/auth/login?transactionId=...`.

### `GET|POST /auth/forgot-password`

Comportamento:

- exige transação hospedada ativa;
- aceita username ou e-mail;
- retorna mensagem genérica para evitar enumeração;
- envia link de reset quando a conta existe;
- preserva `transactionId` no link quando possível.

### `POST /api/auth/token` com `grant_type=authorization_code`

Headers:

- `X-Tenant-Id`

Body:

```json
{
  "grant_type": "authorization_code",
  "client_id": "client-id",
  "code": "authorization-code",
  "redirect_uri": "https://app.example/callback",
  "code_verifier": "pkce-verifier"
}
```

Comportamento:

- valida client, redirect URI e PKCE;
- consome `AuthorizationCodeGrant` uma única vez;
- recarrega usuário/status/roles;
- emite `AuthTokenResponse` dentro de `ApiEnvelope.Data`.

## Modelo de Estado

### `HostedAuthTransaction`

Coleção: `HostedAuthTransactions`, no banco master.

Guarda tenant, client, redirect URI, `state`, `nonce`, `scope`, PKCE,
`correlationId` e expiração.

TTL: 15 minutos.

### `HostedAuthSession`

Coleção: `HostedAuthSessions`, no banco master.

Guarda estágio da jornada:

- `login`
- `awaiting_two_factor`
- `completing`
- `completed`

Também guarda subject autenticado, challenge ativo, destino mascarado e
expiração. Não guarda senha.

### `AuthorizationCodeGrant`

Coleção: `AuthorizationCodeGrants`, no banco master.

Guarda code hash, tenant, client, redirect URI, `nonce`, `scope`, PKCE, subject,
método MFA e `correlationId`.

TTL: 60 segundos.

### `AuthAuditLog`

Coleção: `AuthAuditLogs`, no banco master.

Eventos cobertos:

- `hosted_authorization_started`
- `hosted_authorization_rejected`
- `login_attempted`
- `login_succeeded`
- `login_failed`
- `two_factor_sent`
- `two_factor_resent`
- `two_factor_confirmed`
- `two_factor_failed`
- `two_factor_cancelled`
- `authorization_code_issued`
- `authorization_code_consumed`
- `authorization_code_failed`
- `password_recovery_requested`
- `password_reset_completed`

## Fora de Escopo no v1.0.0

- Sessão SSO central.
- Cookie `lc_sso`.
- Reuso entre apps.
- `prompt=login`.
- `prompt=none`.
- `max_age`.
- `/auth/logout`.
- `post_logout_redirect_uris`.
- `id_token`.
- `/connect/*`.
- OpenIddict.
- `SsoClient` separado.

## Testes Esperados

- Request com redirect URI não cadastrada é rejeitada.
- Scope não permitido é rejeitado.
- Nonce e scope são preservados.
- Login sem 2FA emite code e exchange emite tokens.
- Login com 2FA só emite code após confirmação.
- Code é consumido uma única vez.
- Verifier PKCE errado bloqueia exchange sem consumir code.
- Duas confirmações 2FA concorrentes completam apenas uma vez.

## Próxima Versão Prevista

`v1.1.0`: sessão SSO central com `lc_sso`, reuso entre apps, `prompt`, `max_age`
e logout hospedado.
