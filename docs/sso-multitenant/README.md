# SSO Multi-Tenant - Integração Futura

Documentação do ponto de integração futuro entre resolução pública por host e o
SSO hospedado atual.

Estado importante: esta integração não está implementada na codebase atual. O
fluxo SSO implementado nos commits recentes usa `tenant_id` em `/auth/login` e
`X-Tenant-Id` no exchange de `/api/auth/token`. Não existe `TenantDomain`,
`HostTenantContext`, `DefaultSsoClientId`, issuer canônico único ou `/connect/*`.

## Navegação

- [Plano Atual](plan.md)
- [Diagramas](diagrams/README.md)
- [Histórico de Versões](versions/)
- [SSO Hospedado](../sso-oidc/README.md)
- [Versionamento da Documentação](../versioning.md)

## Manutenção

Ao alterar esta integração:

1. Atualize o `plan.md`.
2. Não documente `Host -> Tenant` como fluxo ativo enquanto não existir código correspondente.
3. Crie nova versão em `versions/` quando o contrato entre host público e SSO mudar.
4. Atualize os diagramas quando o fluxo ponta a ponta real mudar.
