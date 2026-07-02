# OAuth2 e Tokens

Documentação do módulo OAuth da Registration API, incluindo registry de
clientes por tenant, grants suportados em `/api/auth/token`, refresh tokens,
emissão de JWT RS256 tenant-scoped, discovery/JWKS público por tenant e
fronteiras com SSO hospedado e 2FA.

Nota de escopo: esta versão não é um provedor OIDC completo. Não há
`id_token`, `/connect/*`, introspection endpoint ou consentimento interativo.
O fluxo hospedado de login e sessão central continua documentado em
[SSO Hospedado](../sso-oidc/README.md).

## Navegação

- [Plano Atual](plan.md)
- [Snapshot v1.0.1](versions/plan.v1.0.1.md)
- [Diagramas](diagrams/README.md)
- [Histórico de Versões](versions/)
- [SSO Hospedado](../sso-oidc/README.md)
- [2FA e MFA](../two-factor-auth/README.md)
- [Versionamento da Documentação](../versioning.md)

## Manutenção

Ao alterar este domínio:

1. Atualize o `plan.md`.
2. Crie uma nova versão em `versions/` quando houver mudança relevante.
3. Atualize os diagramas quando grants, tokens, policies, discovery ou
   persistência mudarem.
4. Atualize coleções Bruno, testes e contratos quando houver impacto em API
   ou comportamento.
