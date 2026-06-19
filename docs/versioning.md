# Versionamento da Documentação

Este documento define como versionar planos e fluxos em `docs/`.

## Estrutura

```text
docs/
  nome-do-dominio/
    README.md
    plan.md
    versions/
      plan.vX.Y.Z.md
    diagrams/
```

## Versionamento

Utilize o padrão `MAJOR.MINOR.PATCH`.

- **MAJOR**: mudanças incompatíveis que afetam contratos, integrações ou fluxos.
- **MINOR**: novas funcionalidades, regras, diagramas ou comportamentos compatíveis.
- **PATCH**: correções de texto, exemplos ou documentação sem alteração de comportamento.

## Plano e Histórico

- `plan.md` representa a versão atual do domínio.
- `versions/plan.vX.Y.Z.md` armazena versões anteriores.
- Não altere snapshots históricos. Para mudanças relevantes, crie uma nova versão.

## Quando Criar uma Nova Versão

Crie uma nova versão sempre que houver alteração em:

- contratos públicos;
- payloads de request ou response;
- status HTTP;
- fluxos entre sistemas;
- regras de autenticação, autorização ou segurança;
- comportamentos esperados por integradores.

## Checklist

Antes de publicar uma nova versão:

- atualizar `plan.md`;
- criar o snapshot em `versions/`;
- atualizar diagramas relacionados;
- atualizar coleções Bruno quando necessário;
- revisar exemplos e remover dados sensíveis.
