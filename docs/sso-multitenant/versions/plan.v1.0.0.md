# Plano v1.0.0 - SSO Multi-Tenant Explícito

## Objetivo

Documentar o estado multi-tenant do login hospedado na primeira entrega real do
SSO, implementada no commit `4797481`.

Nesta versão, multi-tenant significa tenant explícito por parâmetro/header. Não
há resolução pública por host.

## Estado da Codebase no v1.0.0

- `/auth/login` recebe `tenant_id` por query.
- `/api/auth/token` recebe `X-Tenant-Id`.
- `OAuthClient` é buscado no banco do tenant informado.
- `HostedAuthTransaction`, `HostedAuthSession`, `AuthorizationCodeGrant` e
  `AuthAuditLog` ficam no banco master.
- Discovery/JWKS seguem por tenant em `/tenants/{tenantId}/.well-known/*`.
- Não existe `TenantDomain`.
- Não existe `HostTenantContext`.
- Não existe `DefaultSsoClientId`.
- Não existe cookie `lc_sso`.
- Não existe `/auth/logout`.
- Não existe `/connect/*`.
- Não existe `id_token`.

## Contrato Atual no v1.0.0

Início:

```text
GET /auth/login?response_type=code
  &tenant_id={tenantId}
  &client_id={clientId}
  &redirect_uri={redirectUri}
  &scope={scope}
  &state={state}
  &nonce={nonce}
  &code_challenge={challenge}
  &code_challenge_method=S256
```

Exchange:

```http
POST /api/auth/token
X-Tenant-Id: {tenantId}
Content-Type: application/json
```

```json
{
  "grant_type": "authorization_code",
  "client_id": "client-id",
  "code": "authorization-code",
  "redirect_uri": "https://app.example/callback",
  "code_verifier": "pkce-verifier"
}
```

## Fluxo

1. App conhece o `tenantId`.
2. App inicia `/auth/login` com `tenant_id`.
3. Registration valida `OAuthClient` dentro do tenant informado.
4. Registration valida redirect URI, scope e PKCE.
5. Usuário autentica e confirma 2FA quando necessário.
6. Registration emite authorization code vinculado ao tenant.
7. App troca code em `/api/auth/token` com `X-Tenant-Id`.
8. Registration emite access token tenant-scoped e refresh token.

## O Que Não Deve Ser Documentado como Atual

- `Host -> TenantDomain -> Tenant`.
- Custom domain como entrada do SSO.
- `DefaultSsoClientId`.
- `/connect/authorize`.
- `/connect/token`.
- `/connect/logout`.
- issuer canônico único.
- discovery global.
- JWKS global.
- `id_token`.

## Próxima Versão Prevista

`v1.1.0`: sessão SSO central e logout hospedado, ainda com tenant explícito.
