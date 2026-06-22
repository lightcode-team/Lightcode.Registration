# Fluxos de Emails de Plataforma

Diagramas Mermaid para o domínio de emails globais da plataforma.

## Arquivos

- `01-fluxo-email-plataforma-master.mmd`: fluxo API, RabbitMQ, worker, Mongo master e SMTP master para emails admin/plataforma.

## Decisões Fixas

- Emails admin/plataforma usam `SystemEmail=true`.
- Templates master ficam em `SaasMasterDb.EmailTemplates`.
- Settings globais ficam em `SaasMasterDb.Settings`.
- `MasterSmtp__*` continua sendo a fonte oficial de envio nesta etapa.
- Emails de tenant continuam usando `tenant_{id}.Settings` e `tenant_{id}.EmailTemplates`.
