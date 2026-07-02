# Plano v1.1.0 - SSO Hospedado

## Objetivo

Documentar o fluxo real de SSO hospedado da Registration API:

- login centralizado em `/auth/login`;
- sessão SSO server-side reutilizável entre apps do mesmo tenant;
- Authorization Code com PKCE S256;
- troca de code em `/api/auth/token`;
- integração com 2FA de usuário final;
- discovery/JWKS por tenant para validação dos JWTs emitidos.

Esta versão corrige o plano v1.0.0 antigo, que descrevia uma arquitetura futura
com `/connect/*`, OpenIddict, `SsoClient` separado e `id_token`. Esses itens
seguem como evolução futura, mas não representam o comportamento atual da
codebase.

## Estado Atual da Codebase

- `AuthController` expõe as rotas hospedadas em `/auth/*`.
- `HostedAuthenticationAppService` orquestra authorization request, login, 2FA,
  sessão SSO, authorization code, logout e exchange.
- `AuthenticationAppService` valida credenciais, aplica política de 2FA,
  recarrega usuário antes de emitir token e preserva os grants legados.
- `OAuthClient` é o registro ativo de clientes. Ele continua suportando
  `client_credentials` e também carrega os campos do login hospedado.
- Tokens de usuário final são JWTs tenant-scoped assinados com a chave RSA do
  tenant.
- Discovery/JWKS ficam em `/tenants/{tenantId}/.well-known/openid-configuration`
  e `/tenants/{tenantId}/.well-known/jwks.json`.
- Não há `/connect/*`, OpenIddict, `SsoClient` separado ou `id_token`.

## Decisões Fechadas no v1.1.0

- `GET /auth/login` é o authorization endpoint atual.
- `POST /api/auth/token` concentra `authorization_code`, `password`,
  `refresh_token` e `client_credentials`.
- `authorization_code` é separado internamente: quando `grant_type` é
  `authorization_code`, o controller chama `HostedAuthenticationAppService`.
- PKCE S256 é obrigatório para todo login hospedado.
- `tenant_id` é obrigatório no início do fluxo hospedado.
- `X-Tenant-Id` é obrigatório no token exchange.
- `redirect_uri` e `post_logout_redirect_uri` precisam bater exatamente com o
  cadastro do `OAuthClient`.
- HTTP em redirect URI só é aceito para loopback; HTTPS e custom scheme são
  aceitos conforme o validador atual.
- `scope` é validado contra `AllowedScopes`, scopes básicos `openid`, `profile`,
  `email` e scopes do `TokenConfig`.
- `RequireConsent` não altera comportamento nesta versão.
- `nonce` é armazenado no code grant, mas ainda não é refletido em `id_token`
  porque não há `id_token`.
- O exchange emite `AuthTokenResponse` com `requires_2fa=false` e `token`.
- O logout central revoga somente `SsoSession` e remove `lc_sso`; os apps devem
  limpar seus próprios access/refresh tokens.

## Contratos Públicos

### `GET /auth/login`

Inicia o fluxo hospedado.

Parâmetros obrigatórios:

- `response_type=code`
- `tenant_id`
- `client_id`
- `redirect_uri`
- `state`
- `code_challenge`
- `code_challenge_method=S256`

Parâmetros opcionais:

- `scope`
- `nonce`
- `prompt`
- `max_age`
- `transactionId`, usado apenas para reabrir uma transação já criada.

Comportamento:

- valida prompt (`login`, `none` ou vazio);
- valida `max_age` como inteiro não negativo;
- valida client ativo, redirect URI exata, scope e PKCE;
- cria `HostedAuthTransaction` e `HostedAuthSession`;
- tenta reutilizar `lc_sso` quando `prompt` não é `login`;
- com `prompt=none`, falha de sessão redireciona ao callback validado com
  `error=login_required` e `state`;
- sem sessão válida, renderiza a tela de login.

### `POST /auth/login`

Confirma credenciais por formulário server-side com antiforgery.

Comportamento:

- aplica rate limit de login humano;
- chama `BeginHostedPasswordAuthenticationAsync`;
- se credenciais falham, retorna erro genérico;
- se 2FA não é exigido, cria/rotaciona `SsoSession`, emite code e redireciona;
- se 2FA é exigido, grava `HostedAuthSession.Stage=awaiting_two_factor` e
  redireciona para `/auth/2fa`.

### `GET|POST /auth/2fa`

Exibe e confirma o challenge de 2FA do login hospedado.

Comportamento:

- `GET` renderiza a etapa quando a sessão está em `awaiting_two_factor`;
- `POST` aplica rate limit por IP/tenant/challenge;
- confirma challenge `purpose=login`;
- valida que o subject confirmado bate com a sessão hospedada;
- cria/rotaciona `SsoSession`, emite code e redireciona ao callback.

### `POST /auth/2fa/resend`

Substitui o challenge ativo.

Regras:

- exige sessão em `awaiting_two_factor`;
- respeita cooldown de 30 segundos;
- cria novo challenge `email_code`;
- substitui `challengeId`, `verificationType`, `destinationHint` e
  `challengeCreatedAtUtc` na sessão hospedada.

### `POST /auth/2fa/cancel`

Cancela a etapa 2FA e volta ao login.

Regras:

- invalida challenge pendente do usuário para `purpose=login`;
- limpa subject, e-mail, username, challenge e método MFA da sessão hospedada;
- retorna para `/auth/login?transactionId=...`.

### `POST /api/auth/token` com `grant_type=authorization_code`

Troca o code por tokens.

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

- valida `code`, `client_id`, `redirect_uri` e `code_verifier`;
- valida client ativo e redirect URI exata;
- consome `AuthorizationCodeGrant` atomicamente por tenant, code hash, client,
  redirect URI e PKCE;
- recarrega usuário/status/roles antes de emitir token;
- emite `AuthTokenResponse` com access token e refresh token.

Conteúdo de `Data` na resposta de sucesso:

```json
{
  "requires_2fa": false,
  "token": {
    "access_token": "jwt",
    "token_type": "Bearer",
    "expires_in": 7200,
    "refresh_token": "refresh"
  },
  "challenge": null
}
```

### `GET|POST /auth/logout`

Encerra a sessão SSO central.

Parâmetros:

- `tenant_id`
- `post_logout_redirect_uri`

Comportamento:

- revoga a `SsoSession` identificada pelo cookie;
- remove o cookie `lc_sso`;
- redireciona apenas quando `post_logout_redirect_uri` está cadastrado em algum
  `OAuthClient` ativo do tenant;
- se não houver redirect válido, retorna texto simples `Logout concluido.`.

## Modelo de Estado

### `HostedAuthTransaction`

Coleção: `HostedAuthTransactions`, no banco master.

Campos principais:

- `TenantId`
- `ClientId`
- `RedirectUri`
- `State`
- `Nonce`
- `Scope`
- `Prompt`
- `MaxAgeSeconds`
- `CodeChallenge`
- `CodeChallengeMethod`
- `CorrelationId`
- `ExpiresAtUtc`

TTL atual: 15 minutos.

### `HostedAuthSession`

Coleção: `HostedAuthSessions`, no banco master.

Controla a jornada de tela:

- `login`
- `awaiting_two_factor`
- `completing`
- `completed`

Também guarda subject autenticado, challenge ativo, destino mascarado, método
MFA e expiração curta. Não guarda senha.

### `SsoSession`

Coleção: `SsoSessions`, no banco master.

Campos principais:

- `TenantId`
- `SubjectId`
- `SubjectEmail`
- `SubjectUsername`
- `AuthTimeUtc`
- `LastSeenAtUtc`
- `ExpiresAtUtc`
- `MfaMethod`
- `TwoFactorSatisfied`
- `RevokedAtUtc`
- `UserAgentHash`
- `IpHash`

Regras:

- o cookie `lc_sso` contém somente o id opaco da sessão;
- a sessão é isolada por tenant;
- TTL absoluto atual: 8 horas;
- idle timeout atual: 30 minutos;
- nova autenticação revoga a sessão anterior enviada no contexto;
- reuso exige sessão ativa, não revogada, sem idle timeout e usuário ainda ativo.

### `AuthorizationCodeGrant`

Coleção: `AuthorizationCodeGrants`, no banco master.

Campos principais:

- hash do code opaco;
- tenant, client e redirect URI;
- nonce e scope da transação;
- PKCE challenge e método;
- subject autenticado;
- método MFA usado;
- correlation id;
- expiração e consumo.

TTL atual: 60 segundos. O consumo é atômico e impede replay.

## Fluxo Ponta a Ponta

1. O app cliente cadastra ou atualiza `OAuthClient` com callback, callback de
   logout e scopes aceitos.
2. O app gera `state`, `nonce`, `code_verifier` e `code_challenge`.
3. O browser abre `/auth/login` com a Authorization Request.
4. O backend valida client, callback, scope e PKCE.
5. O backend cria transaction/session no master DB.
6. Se `lc_sso` é válido e o request permite reuso, o backend toca a sessão e
   emite novo authorization code.
7. Sem sessão válida, o usuário envia usuário/senha no formulário.
8. Se 2FA for exigido, o backend cria challenge e espera `/auth/2fa`.
9. Após login completo, o backend cria ou reutiliza `SsoSession` e emite code.
10. O browser volta ao app em `redirect_uri?code&state`.
11. O app valida `state`.
12. O app chama `/api/auth/token` com `grant_type=authorization_code`,
    `X-Tenant-Id`, code e `code_verifier`.
13. O backend consome o code, recarrega a identidade e emite access/refresh token.

## Tokens Emitidos

O access token emitido após exchange usa o mesmo perfil do password grant:

- `iss`: `{PublicApiBaseUrl ou Jwt:Issuer}/tenants/{tenantId}`;
- `aud`: `{Jwt:Audience}/{tenantId}`;
- `sub`: id do usuário;
- `tenantId`;
- `token_use=tenant_access`;
- `userId`;
- `email`, quando disponível;
- `username`, quando disponível;
- `role`, conforme documento atual do usuário;
- claims MFA quando houve 2FA: `amr=pwd`, `amr=mfa`, `auth_time`,
  `mfa_method`.

O fluxo atual não emite `id_token` e não reflete `nonce` em token.

## Discovery e JWKS

Discovery por tenant:

- `GET /tenants/{tenantId}/.well-known/openid-configuration`

JWKS por tenant:

- `GET /tenants/{tenantId}/.well-known/jwks.json`

O discovery atual é mínimo e informa:

- `issuer`;
- `jwks_uri`;
- `id_token_signing_alg_values_supported` com `RS256`.

Apesar do nome do campo, ele serve hoje para validação dos JWTs tenant-scoped.

## Bruno

Requests relevantes:

- `Auth / Open Hosted Login`
- `Auth / Authorization Code Exchange`
- `Auth / Logout Hosted SSO`
- `Discovery / OpenID Configuration`
- `Discovery / JWKS`
- `OAuth Clients / Create OAuth Client`
- `OAuth Clients / Update OAuth Client`

Variáveis relevantes:

- `tenantId`
- `oauthClientId`
- `hostedRedirectUri`
- `postLogoutRedirectUri`
- `hostedScope`
- `oauthState`
- `oauthNonce`
- `pkceVerifier`
- `pkceChallenge`
- `authorizationCode`

## Segurança

- PKCE S256 obrigatório.
- `state` é preservado e deve ser validado pelo app antes do exchange.
- Authorization code é opaco, armazenado como hash e de uso único.
- `code_verifier` nunca entra na URL.
- JWT e refresh token nunca passam por Razor view nem por query string.
- Login, 2FA, reenvio e recuperação usam rate limiting.
- Formulários hospedados usam antiforgery.
- `lc_sso` é `HttpOnly`, `Secure`, `SameSite=Lax` e guarda apenas id opaco.
- `prompt=login` força senha.
- `prompt=none` sem sessão válida retorna `login_required` ao callback validado.
- `max_age` bloqueia reuso de autenticação antiga.
- Logout central só redireciona para URI cadastrada.
- Auditoria não deve registrar senha, código 2FA, code puro, `code_verifier`,
  access token ou refresh token.

## Auditoria

Eventos usados pelo fluxo:

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
- `sso_session_created`
- `sso_session_reused`
- `sso_session_revoked`
- `sso_session_expired`
- `sso_prompt_login`
- `sso_prompt_none_failed`
- `logout_completed`

## Fora de Escopo

- OIDC completo.
- `/connect/*`.
- `id_token`.
- OpenIddict.
- `SsoClient` separado.
- Consentimento interativo.
- Tenant resolvido por custom domain no fluxo público.
- Revogação de access/refresh tokens pelo logout central.

## Testes Esperados

- Authorization request inválida não cria transaction/session.
- Redirect URI não cadastrada é rejeitada.
- Scope não permitido é rejeitado.
- Nonce e scope são preservados no code grant.
- Login sem 2FA emite code e exchange emite tokens.
- Login com 2FA só emite code após confirmação.
- Duas confirmações 2FA concorrentes completam apenas uma vez.
- Code é consumido apenas uma vez.
- Verifier PKCE errado não consome code.
- Sessão SSO é criada após login.
- Segundo app do mesmo tenant reutiliza `lc_sso`.
- `prompt=login` não reutiliza sessão SSO.
- `prompt=none` sem sessão falha com `login_required`.
- `max_age` expirado bloqueia reuso.
- Logout revoga `SsoSession` e só redireciona para URI autorizada.

## Próxima Versão Prevista

`v1.2.0`: hardening operacional, consentimento e preparação para OIDC completo.
