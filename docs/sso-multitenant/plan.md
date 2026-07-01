# Plano Vivo de Integração SSO Multi-Tenant

## Estado Atual

Integração por host público ainda não implementada.

Snapshot completo:
[versions/plan.v1.1.0.md](versions/plan.v1.1.0.md)

## Decisões Ativas

- O SSO ativo hoje é o [SSO Hospedado](../sso-oidc/plan.md).
- `GET /auth/login` exige `tenant_id` na query.
- `POST /api/auth/token` exige `X-Tenant-Id`.
- Discovery/JWKS são por tenant em `/tenants/{tenantId}/.well-known/*`.
- Não existe entidade `TenantDomain` no código atual.
- Não existe resolução pública `Host -> TenantDomain -> Tenant`.
- Não existe `HostTenantContext`.
- Não existe `DefaultSsoClientId`.
- Não existe issuer canônico único para SSO/OIDC.
- Não existem `/connect/authorize`, `/connect/token` ou `/connect/logout`.
- Custom Domains não alimenta o fluxo SSO atual.

## Contrato Atual

O contrato ativo de tenant para SSO é explícito:

- início hospedado: `tenant_id` em `/auth/login`;
- exchange: `X-Tenant-Id` em `/api/auth/token`;
- logout central: `tenant_id` em `/auth/logout`;
- validação de tokens: issuer/JWKS por `/tenants/{tenantId}`.

Esse contrato é compatível com os commits recentes de SSO:

- `4797481`: adicionou login hospedado com Authorization Code + PKCE.
- `801691b`: adicionou `SsoSession`, cookie `lc_sso`, `prompt`, `max_age` e logout.
- `39f7d78`: ajustou requests Bruno de integração.

## Fluxo Multi-Tenant Atual

1. App cliente conhece o `tenantId`.
2. App redireciona o browser para `/auth/login` com `tenant_id`.
3. Registration valida `OAuthClient` dentro do tenant informado.
4. Usuário autentica e confirma 2FA quando necessário.
5. Registration emite authorization code vinculado ao tenant, client, redirect URI e PKCE.
6. App troca code em `/api/auth/token` enviando `X-Tenant-Id`.
7. Token emitido usa issuer por tenant: `{PublicApiBaseUrl ou Jwt:Issuer}/tenants/{tenantId}`.

## Fora de Escopo Atual

- Resolver tenant por `Host`.
- Usar custom domain como entrada pública do SSO.
- Remover `tenant_id` da authorization request.
- Remover `X-Tenant-Id` do token exchange.
- Publicar discovery/JWKS canônicos globais.
- Emitir `id_token`.
- Usar `/connect/*`.

## Integração Futura Esperada

Quando Custom Domains existir no código, a integração poderá evoluir para:

- resolver tenant server-side a partir de host validado;
- validar que o `client_id` pertence ao tenant resolvido pelo host;
- impedir troca de tenant por query/header no fluxo público;
- definir se `tenant_id` continuará existindo apenas para ambientes internos ou legado;
- manter isolamento de cookie SSO por domínio/tenant;
- ajustar discovery/JWKS conforme a decisão futura de issuer.

Esses itens são direcionamento futuro, não comportamento atual.

## Pendências

- Implementar modelo e persistência de domínios públicos.
- Implementar middleware/contexto de resolução por host.
- Definir contrato entre resolução de host e `/auth/login`.
- Criar testes ponta a ponta para host, tenant, client e redirect URI.
- Atualizar Bruno quando existir endpoint real para entrada por host.

## Próxima Versão Prevista

`v1.2.0`: documentar integração real se Custom Domains for implementado.
