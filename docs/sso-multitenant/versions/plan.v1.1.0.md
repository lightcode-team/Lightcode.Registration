# Plano v1.1.0 - Integração SSO Multi-Tenant

## Objetivo

Corrigir a documentação de integração SSO multi-tenant para refletir o estado
real da codebase após os commits de SSO.

O fluxo implementado hoje não resolve tenant por host público. Ele usa tenant
explícito na authorization request e no token exchange.

## Estado Atual da Codebase

- `/auth/login` recebe `tenant_id` por query.
- `/api/auth/token` recebe `X-Tenant-Id`.
- `/auth/logout` recebe `tenant_id`.
- `OAuthClient` fica no banco do tenant e é buscado por `tenant_id` + `client_id`.
- `SsoSession` é armazenada no banco master, mas sempre vinculada a `TenantId`.
- Discovery e JWKS ficam em `/tenants/{tenantId}/.well-known/*`.
- Não existe `TenantDomain`.
- Não existe `HostTenantContext`.
- Não existe `DefaultSsoClientId`.
- Não existe `/connect/*`.
- Não existe `id_token`.

## Commits Usados como Fonte

- `4797481`: login hospedado com Authorization Code + PKCE.
- `801691b`: sessão SSO central, cookie `lc_sso`, `prompt`, `max_age` e logout.
- `39f7d78`: requests Bruno de integração.

## Contrato Atual

### Início

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

### Exchange

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

### Logout

```text
GET /auth/logout?tenant_id={tenantId}&post_logout_redirect_uri={uri}
```

## O Que Não Deve Ser Documentado como Atual

- `Host -> TenantDomain -> Tenant`.
- `DefaultSsoClientId`.
- `/connect/authorize`.
- `/connect/token`.
- `/connect/logout`.
- issuer canônico único.
- `id_token`.
- discovery global em `/.well-known/openid-configuration`.
- JWKS global em `/jwks`.

## Fluxo Atual

1. App escolhe o tenant explicitamente.
2. App inicia `/auth/login` com `tenant_id`.
3. Registration valida client, redirect URI, scope e PKCE no banco do tenant.
4. Usuário autentica, com 2FA quando aplicável.
5. Registration cria ou reutiliza `SsoSession` para aquele tenant.
6. Registration emite authorization code.
7. App troca code em `/api/auth/token` com `X-Tenant-Id`.
8. Registration emite access token tenant-scoped e refresh token.

## Evolução Futura

Quando houver código de Custom Domains, esta documentação deve ser revisada para
mostrar o contrato real de resolução por host. Até lá, qualquer fluxo por host é
apenas direção futura.
