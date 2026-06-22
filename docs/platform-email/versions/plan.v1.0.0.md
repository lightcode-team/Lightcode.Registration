# Plano v1.0.0 - Emails de Plataforma

## Objetivo

Organizar os emails globais da plataforma em torno do banco master
`SaasMasterDb`, mantendo a separação clara entre emails administrativos da
plataforma e emails pertencentes a tenants.

Esta refatoração cria a base para:

- templates globais em `SaasMasterDb.EmailTemplates`;
- settings globais em `SaasMasterDb.Settings`;
- envio de emails admin/plataforma via `MasterSmtp__*`;
- worker capaz de resolver templates master para mensagens `SystemEmail=true`.

## Estado Atual do Sistema

Hoje o backend já possui:

- `MasterSmtp__*` para configuração SMTP global;
- `ISystemOutboundMailSender` para envio de email de sistema;
- mensagens de fila com flag `SystemEmail`;
- `tenant_{id}.Settings` para SMTP de tenant;
- `tenant_{id}.EmailTemplates` para templates de tenant.

O ponto a corrigir e consolidar é que parte dos emails admin/plataforma ainda
depende de templates de tenant, enquanto o 2FA de platform admin usa envio de
sistema com corpo hardcoded.

## Decisões

- Emails admin/plataforma usam SMTP master.
- Templates de emails admin/plataforma ficam em `SaasMasterDb.EmailTemplates`.
- `SaasMasterDb.Settings` deve existir para organizar settings globais.
- `SaasMasterDb.Settings` terá documento `_id = "smtp"` com shape equivalente ao SMTP de tenant.
- Nesta etapa, `MasterSmtp__*` continua sendo a fonte oficial de envio.
- `SaasMasterDb.Settings/smtp` é um espelho organizacional atualizado a partir de `MasterSmtp__*`.
- Seeds de templates master inserem documentos faltantes, mas não sobrescrevem templates existentes.
- Emails de usuário final continuam isolados por tenant.
- Templates master devem ser acessados por contrato/repositório próprio, sem lookup de tenant.
- Mensagens `SystemEmail=true` sem template informado continuam suportando assunto/corpo inline como fallback de compatibilidade.

## Collections Master

### `SaasMasterDb.Settings`

Documento inicial:

```json
{
  "_id": "smtp",
  "smtp": {
    "host": "...",
    "port": 587,
    "usuario": "...",
    "senha": "...",
    "emailRemetente": "...",
    "nomeRemetente": "Lightcode",
    "usarSsl": true
  }
}
```

Fonte do seed:

- `MasterSmtp__Host`
- `MasterSmtp__Port`
- `MasterSmtp__Usuario`
- `MasterSmtp__Senha`
- `MasterSmtp__EmailRemetente`
- `MasterSmtp__NomeRemetente`
- `MasterSmtp__UsarSsl`

Regra de escrita:

- fazer upsert/update a cada startup da API e do worker;
- considerar `MasterSmtp__*` como fonte oficial;
- não tratar edições manuais em `Settings/smtp` como fonte de envio nesta versão.

### `SaasMasterDb.EmailTemplates`

Campos esperados seguem o modelo já usado por `EmailTemplate`:

- `Id`
- `TenantId`
- `Key`
- `DisplayName`
- `Subject`
- `HtmlBody`
- `TextBody`
- `CreatedAtUtc`
- `UpdatedAtUtc`

Para templates master, `TenantId` deve usar valor constante `platform`.

Esta é uma convenção de compatibilidade com o modelo `EmailTemplate` atual.
Templates master não devem ser resolvidos via `ITenantLookup` nem via
`MongoEmailTemplateRepository` de tenant.

Índice:

- único por `Key`.

Repositório:

- criar contrato específico, por exemplo `IPlatformEmailTemplateRepository`;
- a implementação Mongo deve acessar diretamente `MongoOptions.MasterDatabaseName`;
- manter o repositório de tenant atual dedicado a `tenant_{id}.EmailTemplates`.

## Templates Master Iniciais

### `platform-admin-invite`

Usado no convite de administrador da plataforma.

Placeholders mínimos:

- `tenantName`
- `activationToken`
- `activationUrl`
- `expiresAtUtc`

### `tenant-onboarding`

Usado após criação de tenant.

Placeholders mínimos:

- `tenantId`
- `tenantName`
- `clientId`
- `clientSecret`
- `activationUrl`
- `activationToken`
- `expiresAtUtc`

### `platform-admin-2fa-code`

Usado no 2FA de administrador da plataforma.

Placeholders mínimos:

- `username`
- `code`
- `purpose`

## Fluxo de Envio

1. API cria `EmailDispatchQueueMessage`.
2. Emails admin/plataforma devem publicar:
   - `TenantId = "platform"`;
   - `SystemEmail = true`;
   - `TemplateKey` preenchida com template master.
3. RabbitMQ entrega a mensagem ao worker.
4. Worker identifica `SystemEmail=true`.
5. Se `TemplateKey` ou `TemplateId` estiver preenchido, worker resolve template em `SaasMasterDb.EmailTemplates`.
6. Worker aplica placeholders no template resolvido.
7. Se não houver template informado, worker mantém o comportamento atual e usa `Subject`, `HtmlBody` e `TextBody` inline.
8. Worker chama `ISystemOutboundMailSender`.
9. `ISystemOutboundMailSender` envia usando `MasterSmtp__*` ou logging, conforme `MasterSmtp__UseSmtp`.

## Impactos Esperados

- `PlatformAdminAppService` deixa de depender de template do tenant para convite admin.
- `TenantOnboardingAppService` deixa de depender de template do tenant para onboarding.
- `QueuedPlatformSystemEmailSender` deixa de montar corpo hardcoded para 2FA admin e passa a publicar template master.
- `EmailDispatchConsumerHostedService` passa a suportar `SystemEmail=true` com `TemplateKey` ou `TemplateId`.
- `MongoTenantProvisioner` deixa de semear `platform-admin-invite` e `tenant-onboarding` em novos tenants.
- Templates admin já existentes em bancos de tenants antigos não são apagados.

## Fora de Escopo

- CRUD/API/frontend de templates master.
- Migrar templates de usuário final para `SaasMasterDb`.
- Alterar o fluxo de envio de emails de usuário final.
- Usar `SaasMasterDb.Settings` como fonte oficial de SMTP master.
- Criar versionamento formal de templates.
- Remover retroativamente templates admin já existentes em bancos de tenants.

## Testes Esperados

- Worker resolve template master quando `SystemEmail=true`.
- Worker envia via `ISystemOutboundMailSender` após merge de placeholders.
- Worker preserva fallback inline para `SystemEmail=true` sem template informado.
- Worker preserva fluxo de templates de tenant quando `SystemEmail=false`.
- Seed cria `SaasMasterDb.Settings/smtp`.
- Seed atualiza `SaasMasterDb.Settings/smtp` a partir de `MasterSmtp__*`.
- Seed cria templates master faltantes.
- Seed não sobrescreve template master existente.
- Serviços admin/plataforma publicam mensagens com `SystemEmail=true`.
- Novos tenants não recebem seeds `platform-admin-invite` e `tenant-onboarding`.

## Diagrama

Fluxo principal:
[diagrams/01-fluxo-email-plataforma-master.mmd](../diagrams/01-fluxo-email-plataforma-master.mmd)
