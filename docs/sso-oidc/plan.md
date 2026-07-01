# Plano Vivo de SSO Hospedado

## Estado Atual

Core de SSO hospedado implementado conforme baseline v1.1.0.

Snapshot completo:
[versions/plan.v1.1.0.md](versions/plan.v1.1.0.md)

## Decisões Ativas

- O endpoint público de início do SSO é `GET /auth/login`.
- A troca do authorization code ocorre em `POST /api/auth/token` com `grant_type=authorization_code`.
- Não existem `/connect/authorize`, `/connect/token` ou `/connect/logout` nesta versão.
- Não há emissão de `id_token`; o exchange retorna `AuthTokenResponse` com `access_token` e `refresh_token`.
- O client registry ativo é `OAuthClient` do tenant. Não há `SsoClient` separado.
- `RedirectUris` vazio mantém o cliente sem login hospedado.
- `PostLogoutRedirectUris` controla os destinos permitidos após `/auth/logout`.
- `AllowedScopes` limita os scopes aceitos no início do fluxo.
- `RequireConsent` existe no contrato, mas ainda não há tela de consentimento.
- PKCE é obrigatório e o fluxo aceita somente `code_challenge_method=S256`.
- O tenant entra por `tenant_id` na authorization request e por `X-Tenant-Id` no token exchange.
- A sessão SSO central usa cookie opaco `lc_sso` com `HttpOnly`, `Secure` e `SameSite=Lax`.
- O cookie guarda somente o id da `SsoSession`; dados de usuário, MFA e expiração ficam no banco master.
- `prompt=login`, `prompt=none` e `max_age` são suportados.
- 2FA de usuário final bloqueia emissão de code até confirmação.
- Discovery/JWKS continuam por tenant em `/tenants/{tenantId}/.well-known/*`.

## Contratos Ativos

- `GET /auth/login`
- `POST /auth/login`
- `GET /auth/2fa`
- `POST /auth/2fa`
- `POST /auth/2fa/resend`
- `POST /auth/2fa/cancel`
- `GET|POST /auth/forgot-password`
- `GET|POST /auth/logout`
- `/api/auth/token`
- `/api/auth/confirm-2fa`
- `/tenants/{tenantId}/.well-known/*`

## Fluxo Atual Desejado

- App cliente configura `OAuthClient` com `redirect_uris`, `post_logout_redirect_uris` e `allowed_scopes`.
- App gera `state`, `nonce`, `code_verifier` e `code_challenge=S256(code_verifier)`.
- Browser abre `/auth/login` com `response_type=code`, `tenant_id`, `client_id`, `redirect_uri`, `scope`, `state`, `nonce`, `code_challenge` e `code_challenge_method=S256`.
- Backend valida tenant, `OAuthClient`, callback exato, scopes e PKCE.
- Backend cria `HostedAuthTransaction` e `HostedAuthSession` no banco master.
- Se houver `lc_sso` válido e o request permitir reuso, backend emite novo authorization code sem senha.
- Sem sessão SSO, usuário autentica por Razor form com antiforgery.
- Se 2FA for exigido, `/auth/2fa` confirma o challenge antes de concluir.
- Backend cria ou rotaciona `SsoSession`, grava `lc_sso` e redireciona para `redirect_uri?code&state`.
- App valida `state` e troca `code` em `/api/auth/token` com `X-Tenant-Id` e `code_verifier`.
- Backend consome o code uma única vez, recarrega usuário/status/roles e emite access/refresh token tenant-scoped.

## Persistência Ativa

No banco master:

- `HostedAuthTransactions`: dados da authorization request, TTL de 15 minutos.
- `HostedAuthSessions`: estado curto da tela, TTL de 15 minutos.
- `AuthorizationCodeGrants`: code hash, PKCE, tenant, client, callback e subject, TTL de 60 segundos.
- `SsoSessions`: sessão central por tenant, TTL absoluto de 8 horas e idle timeout de 30 minutos.
- `AuthAuditLogs`: eventos de login, 2FA, code, SSO e logout.

No banco do tenant:

- `OAuthClients`: credenciais técnicas e configuração do login hospedado.
- `Users`: identidade recarregada antes da emissão de token.
- `RefreshTokens`: refresh tokens emitidos após exchange.

## Separação de Responsabilidades

- Login hospedado e sessão SSO ficam em `/auth/*`.
- Emissão e renovação de tokens continuam em `/api/auth/token`.
- `password`, `refresh_token` e `client_credentials` continuam suportados como fluxos legados/técnicos.
- Discovery/JWKS por tenant servem para validar JWTs tenant-scoped.
- 2FA continua sendo o domínio responsável por challenge, verificação e claims MFA.
- Views de autenticação documentam UX e formulários; este domínio documenta o contrato de SSO.

## Fora de Escopo

- OIDC completo com `id_token`.
- `/connect/*`.
- OpenIddict.
- `SsoClient` separado de `OAuthClient`.
- Consentimento interativo.
- Custom Domains como fonte server-side de tenant para SSO público.
- Revogação automática de access/refresh tokens no logout central.

## Pendências

- Propagar `scope` solicitado para claims de access token quando esse contrato for formalizado.
- Emitir `id_token` e refletir `nonce` quando o fluxo virar OIDC completo.
- Criar consentimento quando `RequireConsent=true`.
- Integrar Custom Domains para remover `tenant_id` público do fluxo hospedado.
- Definir runbook de rotação de signing keys tenant e Data Protection.
- Expandir testes end-to-end com navegador real para cookie `lc_sso`, antiforgery e logout.

## Próxima Versão Prevista

`v1.2.0`: hardening operacional, consentimento e preparação para OIDC completo.
