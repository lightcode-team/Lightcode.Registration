# Plano Vivo de Emails de Plataforma

## Estado Atual

Refatoração planejada conforme baseline v1.0.0.

Snapshot completo:
[versions/plan.v1.0.0.md](versions/plan.v1.0.0.md)

## Decisões Ativas

- Emails admin/plataforma usam SMTP master.
- Templates master ficam em `SaasMasterDb.EmailTemplates`.
- Settings globais ficam organizados em `SaasMasterDb.Settings`.
- O documento SMTP global usa `_id = "smtp"`.
- `MasterSmtp__*` continua sendo a fonte oficial de envio nesta etapa.
- `SaasMasterDb.Settings/smtp` é um espelho organizacional atualizado a partir de `MasterSmtp__*`.
- Seeds de templates master não sobrescrevem documentos existentes.
- Templates master devem ser acessados por um repositório/contrato próprio, sem passar por lookup de tenant.
- Mensagens `SystemEmail=true` sem `TemplateKey` ou `TemplateId` continuam suportando assunto/corpo inline como fallback de compatibilidade.

## Templates Master Iniciais

- `platform-admin-invite`: convite de administrador da plataforma.
- `tenant-onboarding`: onboarding de tenant e credenciais iniciais.
- `platform-admin-2fa-code`: código 2FA de administrador da plataforma.

## Fluxo Atual Desejado

- API publica `EmailDispatchQueueMessage` com `SystemEmail=true`.
- RabbitMQ entrega a mensagem ao worker.
- Worker busca template em `SaasMasterDb.EmailTemplates` quando `TemplateKey` ou `TemplateId` estiver preenchido.
- Worker aplica placeholders com `EmailTemplatePlaceholderMerger`.
- Worker usa `Subject`/`HtmlBody`/`TextBody` inline apenas quando não houver template informado.
- Worker envia via `ISystemOutboundMailSender`.
- `ISystemOutboundMailSender` usa `MasterSmtp__*`.

## Implementação Esperada

- Criar contrato específico para templates master, por exemplo `IPlatformEmailTemplateRepository`.
- Implementação Mongo deve acessar diretamente `MongoOptions.MasterDatabaseName`, collection `EmailTemplates`.
- Não reutilizar `MongoEmailTemplateRepository` para templates master, pois ele resolve banco por `ITenantLookup`.
- Para templates master, manter `TenantId = "platform"` apenas como convenção de compatibilidade com o modelo atual.
- Seed de `Settings/smtp` deve ser upsert/update a cada startup a partir de `MasterSmtp__*`, pois o env continua sendo fonte oficial.
- Seeds master de templates devem inserir somente chaves faltantes.
- Novos tenants não precisam mais receber seeds `platform-admin-invite` e `tenant-onboarding`; dados já existentes em tenants antigos não devem ser apagados.

## Separação de Responsabilidades

- Emails de plataforma usam banco master e SMTP master.
- Emails de usuário final continuam usando `tenant_{id}.EmailTemplates`.
- SMTP de usuário final continua usando `tenant_{id}.Settings`.
- Templates de tenant não devem depender de `SaasMasterDb.EmailTemplates`.

## Fora de Escopo

- CRUD/API/frontend para templates master.
- Migração de templates de usuário final para o banco master.
- Usar Mongo como fonte oficial do SMTP master.
- Versionamento formal de templates editados por operador.
- Remoção retroativa de templates admin já existentes nos bancos de tenants.

## Próxima Versão Prevista

`v1.1.0`: gerenciamento operacional de templates master, se necessário.
