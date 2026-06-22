# Emails de Plataforma

Documentação do domínio de emails globais da plataforma, incluindo SMTP master,
templates master e separação entre emails administrativos e emails de tenant.

## Navegação

- [Plano Atual](plan.md)
- [Diagramas](diagrams/README.md)
- [Histórico de Versões](versions/)
- [Versionamento da Documentação](../versioning.md)

## Manutenção

Ao alterar este domínio:

1. Atualize o `plan.md`.
2. Crie uma nova versão em `versions/` quando houver mudança relevante.
3. Atualize os diagramas quando o fluxo API/worker/Mongo/SMTP mudar.
4. Atualize testes, seeds e coleções Bruno quando houver impacto em contratos ou comportamento.
