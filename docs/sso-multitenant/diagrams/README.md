# Fluxos de Integração SSO Multi-Tenant

Diagramas Mermaid da integração multi-tenant no estado atual.

## Arquivos

- `01-host-tenant-authorize-token.mmd`: mostra que `Host -> Tenant` ainda não é
  parte do código e que o fluxo atual usa `tenant_id` e `X-Tenant-Id`.

## Decisões Fixas

- O SSO atual usa `/auth/login`.
- O token exchange atual usa `/api/auth/token`.
- Tenant é informado explicitamente por `tenant_id` e `X-Tenant-Id`.
- Custom Domains não alimenta o SSO atual.
- `/connect/*` não existe na codebase atual.

## Planos Canônicos

- [SSO Hospedado](../../sso-oidc/diagrams/README.md)
