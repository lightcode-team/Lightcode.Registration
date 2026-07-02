# SSO Hospedado

Documentação do fluxo atual de SSO da Registration API, incluindo login
hospedado em `/auth/login`, sessão central `lc_sso`, Authorization Code com
PKCE S256, troca de code em `/api/auth/token`, discovery/JWKS por tenant,
logout central e integração com 2FA.

Nota de escopo: esta versão ainda não é um provedor OIDC completo. Não existem
`/connect/*`, OpenIddict, entidade `SsoClient` separada nem emissão de
`id_token`. O diretório permanece como `sso-oidc` por histórico e porque o
fluxo usa formato de Authorization Request com `openid`, `nonce` e discovery.

## Navegação

- [Plano Atual](plan.md)
- [Diagramas](diagrams/README.md)
- [Histórico de Versões](versions/)
- [Views de Autenticação e SSO](../views-autenticacao/README.md)
- [Versionamento da Documentação](../versioning.md)

## Manutenção

Ao alterar este domínio:

1. Atualize o `plan.md`.
2. Crie uma nova versão em `versions/` quando houver mudança relevante.
3. Atualize os diagramas quando login/token/logout/discovery mudar.
4. Atualize coleções Bruno, testes e contratos quando houver impacto em API ou comportamento.
