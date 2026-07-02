# Fluxos de SSO Hospedado

Diagramas Mermaid para o domínio de SSO hospedado atual.

## Arquivos

- `01-authorization-code-pkce.mmd`: fluxo hosted authorization code com PKCE S256.
- `02-login-com-2fa-e-sessao-sso.mmd`: login humano, 2FA e sessão `lc_sso`.
- `03-token-discovery-jwks.mmd`: exchange em `/api/auth/token` e validação por discovery/JWKS do tenant.
- `04-rp-initiated-logout.mmd`: logout central hospedado em `/auth/logout`.
- `05-controles-seguranca-oidc.mmd`: controles contra replay, open redirect, CSRF e vazamento de tokens.

## Decisões Fixas

- SSO atual usa `/auth/login`, não `/connect/authorize`.
- Token exchange atual usa `/api/auth/token`, não `/connect/token`.
- Logout central atual usa `/auth/logout`, não `/connect/logout`.
- `OAuthClient` é o client registry atual.
- PKCE aceita somente S256.
- `state` deve ser preservado.
- `nonce` é preservado no code grant, mas ainda não há `id_token`.
- Authorization code é opaco, curto e de uso único.
- 2FA bloqueia code/token até confirmação.
- Discovery, JWKS e `iss` usam o issuer por tenant.
