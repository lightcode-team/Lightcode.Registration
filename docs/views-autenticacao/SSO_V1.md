# SSO V1 com Sessao Central

## Resumo

O SSO V1 transforma o login hospedado em uma autenticacao reutilizavel entre apps do mesmo tenant.

O usuario autentica uma vez em `/auth/login`; o servidor de autenticacao cria uma sessao central `SsoSession` no banco master e grava no browser um cookie opaco `lc_sso`. Quando outro app do mesmo tenant abre novamente `/auth/login`, o backend valida a Authorization Request OAuth2, reutiliza a sessao SSO e emite um novo authorization code sem pedir senha ou 2FA outra vez.

Tokens continuam sendo emitidos somente em `/api/auth/token`. JWT, refresh token, senha, `code_verifier` e codigo 2FA nunca sao armazenados no cookie SSO nem trafegam pela view.

## Modelo de Estado

### Transacao OAuth

Representa uma tentativa especifica de autorizacao para um cliente.

Guarda dados do protocolo:

- `tenantId`
- `clientId`
- `redirectUri`
- `state`
- `nonce`
- `scope`
- `codeChallenge`
- `codeChallengeMethod`
- `prompt`
- `maxAgeSeconds`
- `correlationId`
- TTL de 15 minutos

### Sessao de Autenticacao Hospedada

Representa o estado curto da tela de login atual.

Controla:

- estagio atual: `login`, `awaiting_two_factor`, `completing`, `completed`
- usuario autenticado durante a jornada
- challenge 2FA ativo
- destino mascarado
- metodo MFA usado
- expiracao da jornada

### Sessao SSO Central

Representa o usuario autenticado no servidor `/auth`.

Colecao: `SsoSessions`, no banco master.

Campos principais:

- `tenantId`
- `subjectId`
- `subjectEmail`
- `subjectUsername`
- `createdAtUtc`
- `lastSeenAtUtc`
- `authTimeUtc`
- `expiresAtUtc`
- `mfaMethod`
- `twoFactorSatisfied`
- `revokedAtUtc`
- `correlationId`
- `userAgentHash`
- `ipHash`

Regras:

- O cookie `lc_sso` contem somente o `sessionId` opaco.
- A sessao SSO e por tenant.
- TTL absoluto padrao: 8 horas.
- Idle timeout padrao: 30 minutos.
- Uma nova autenticacao rotaciona a sessao anterior.
- Sessao expirada, revogada ou com usuario nao ativo nao pode emitir authorization code.

## Contratos Publicos

### `GET /auth/login`

Inicia uma Authorization Request OAuth2/OIDC hospedada.

Parametros:

- `response_type=code`
- `tenant_id`
- `client_id`
- `redirect_uri`
- `scope`
- `state`
- `nonce`
- `code_challenge`
- `code_challenge_method=S256`
- `prompt` opcional
- `max_age` opcional

Comportamento SSO:

- Sem `prompt`: tenta reutilizar `lc_sso`; se nao conseguir, renderiza login.
- `prompt=login`: ignora `lc_sso` e exige senha novamente.
- `prompt=none`: exige `lc_sso` valido; se nao houver, redireciona ao callback validado com `error=login_required` e o `state` original.
- `max_age`: se `authTimeUtc` da sessao SSO for antigo demais, exige login novamente.

### `POST /auth/login`

Valida credenciais por formulario server-side com antiforgery.

Se login for valido:

- sem 2FA: cria/rotaciona `SsoSession`, grava cookie `lc_sso`, emite authorization code e redireciona ao callback.
- com 2FA: vai para `/auth/2fa`, sem emitir tokens.

### `POST /auth/2fa`

Confirma 2FA.

Se o codigo for valido:

- cria/rotaciona `SsoSession`
- marca `twoFactorSatisfied=true`
- grava cookie `lc_sso`
- emite authorization code
- redireciona ao callback

### `POST /api/auth/token`

Troca authorization code por tokens.

Regras preservadas:

- `grant_type=authorization_code`
- `client_id`
- `redirect_uri`
- `code`
- `code_verifier`
- PKCE S256 obrigatorio
- code de uso unico e consumo atomico

### `GET|POST /auth/logout`

Encerra a sessao SSO central.

Parametros:

- `tenant_id`
- `post_logout_redirect_uri`

Comportamento:

- revoga a `SsoSession` atual
- remove o cookie `lc_sso`
- redireciona apenas se `post_logout_redirect_uri` estiver cadastrado no OAuthClient
- os apps clientes continuam responsaveis por limpar seus proprios access/refresh tokens

## OAuth Client

O cadastro de cliente passa a aceitar:

- `redirect_uris`: callbacks permitidos para authorization code.
- `post_logout_redirect_uris`: callbacks permitidos apos logout central.
- `allowed_scopes`: scopes que o cliente pode solicitar.
- `require_consent`: preparado para consentimento futuro, ainda sem tela nesta versao.

Exemplo:

```json
{
  "redirect_uris": [
    "https://localhost:4200/auth/callback"
  ],
  "post_logout_redirect_uris": [
    "https://localhost:4200/auth/logout-callback"
  ],
  "allowed_scopes": [
    "openid",
    "email"
  ],
  "require_consent": false
}
```

## Auditoria e Seguranca

Eventos novos em `AuthAuditLogs`:

- `sso_session_created`
- `sso_session_reused`
- `sso_session_revoked`
- `sso_session_expired`
- `sso_prompt_login`
- `sso_prompt_none_failed`
- `logout_completed`

Dados sensiveis que nao devem ser registrados:

- senha
- codigo 2FA
- authorization code puro
- `code_verifier`
- access token
- refresh token
- dados completos do usuario

Logs e auditoria podem usar:

- `tenantId`
- `clientId`
- `subjectId`
- `transactionId`
- `sessionId`
- `correlationId`
- hashes parciais para IP e user-agent

## Bruno

Requests atualizadas:

- `Auth / Open Hosted Login`
  - inclui `prompt` e `max_age`
- `Auth / Authorization Code Exchange`
  - continua usando o `pkceVerifier`
- `Auth / Logout Hosted SSO`
  - chama `/auth/logout`
- `OAuth Clients / Create OAuth Client`
  - inclui `post_logout_redirect_uris`
- `OAuth Clients / Update OAuth Client`
  - inclui `post_logout_redirect_uris`

Variaveis relevantes:

- `hostedRedirectUri`
- `postLogoutRedirectUri`
- `oauthClientId`
- `oauthState`
- `oauthNonce`
- `pkceVerifier`
- `pkceChallenge`
- `authorizationCode`

## Testes de Aceite

- Login inicial cria `SsoSession`, cookie `lc_sso` e authorization code.
- Segundo app do mesmo tenant reutiliza `lc_sso` e recebe novo code sem pedir senha.
- App de outro tenant nao reutiliza a sessao.
- `prompt=login` exige senha mesmo com cookie valido.
- `prompt=none` com sessao valida emite code.
- `prompt=none` sem sessao redireciona com `error=login_required`.
- `max_age` expirado bloqueia o reuso e exige login.
- Logout revoga a sessao e remove o cookie.
- Authorization code continua de uso unico.
- Tokens continuam saindo apenas pelo exchange com PKCE valido.

## Diagrama

Fluxo principal: [03-sso-sessao-central.mmd](diagrams/03-sso-sessao-central.mmd)

Passo a passo para integrar um app: [04-integracao-app-sso.mmd](diagrams/04-integracao-app-sso.mmd)
