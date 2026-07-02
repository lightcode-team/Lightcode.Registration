# Diagramas do Módulo OAuth

Diagramas Mermaid para o módulo OAuth atual.

## Arquivos

- `01-client-registry-e-provisionamento.mmd`: criação do tenant, client owner
  e gestão de clients por policy.
- `02-client-credentials-token.mmd`: emissão de access/refresh token para
  `client_credentials`.
- `03-authorization-code-pkce-exchange.mmd`: ponte entre login hospedado,
  authorization code e exchange em `/api/auth/token`.
- `04-refresh-token-e-validação-jwt.mmd`: renovação de access token e
  validação de bearer JWT tenant-scoped.
- `05-discovery-jwks-tenant.mmd`: discovery e JWKS público por tenant.

## Decisões Fixas

- `OAuthClient` é o registry atual de clients.
- Clients ficam em `tenant_{id}.OAuthClients`.
- `/api/auth/token` exige `X-Tenant-Id`.
- Access tokens tenant-scoped usam RS256.
- Refresh tokens são opacos e persistidos por hash.
- PKCE aceita somente `S256`.
- Authorization code é opaco, curto e single-use.
- Discovery/JWKS é público por tenant.
- `id_token` ainda não é emitido.
