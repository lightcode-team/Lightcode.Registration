# Coleções Bruno — Lightcode.Registration

Existem **duas coleções** (uma por host HTTP), cada uma com o seu `bruno.json`:

| Pasta | Projeto / host | Pedidos |
|--------|----------------|---------|
| `bruno/Lightcode.Registration/` | `Lightcode.Registration` | tenants, auth, accounts, account-json-schemas |
| `bruno/Lightcode.Registration.EmailApi/` | `Lightcode.Registration.EmailApi` | emails (`/api/emails/send`), templates (`/api/email-templates`) |

## Requisitos

- [Bruno](https://www.usebruno.com/) instalado (desktop ou CLI).

## Abrir no Bruno

1. **Open Collection** → escolha **`bruno/Lightcode.Registration`** (API principal) **ou** **`bruno/Lightcode.Registration.EmailApi`** (email).
2. Selecione o ambiente **LOCAL** ou **DOCKER** dentro dessa coleção (cada coleção tem os seus `environments/`).

Para testar a Email API precisa de um JWT válido: use primeiro a coleção da API principal (**Create Tenant**, **Issue Token**) e copie o `access_token` para a variável `jwt` na coleção da Email API (o `issuer`, `audience` e chave de assinatura têm de ser os mesmos nos dois serviços).

## Ambiente DOCKER

Use quando **api**, **email-api**, **mongo** e **rabbitmq** estiverem a correr via `docker compose` na raiz do projeto.

**Coleção API principal** (`Lightcode.Registration/environments/DOCKER.bru`):

| Variável | Valor predefinido | Notas |
|----------|-------------------|--------|
| `baseUrl` | `http://localhost:8080` | Serviço `api` |
| `rabbitManagementUrl` | `http://localhost:15672` | UI RabbitMQ (guest/guest) — referência |
| `mongoUrl` | `mongodb://localhost:27017` | Referência; pedidos HTTP vão à API |
| `provisioningKey` | *(vazio)* | Alinhar com `Master__ProvisioningApiKey` / `MASTER_PROVISIONING_KEY` |
| `tenantId` / `jwt` / `schemaId` | *(exemplo ou vazio)* | Preencher após criar tenant e emitir token contra este `baseUrl` |

**Coleção Email API** (`Lightcode.Registration.EmailApi/environments/DOCKER.bru`):

| Variável | Valor predefinido | Notas |
|----------|-------------------|--------|
| `baseUrl` | `http://localhost:8081` | Serviço `email-api` |
| `registrationApiUrl` | `http://localhost:8080` | Referência à API principal |
| `jwt` | *(exemplo)* | Emitir de novo com **Issue Token** na API principal se a chave JWT do compose for diferente da local |
| `emailTemplateId` | *(vazio)* | Copiar de **List Email Templates** |

## Ambiente LOCAL

**API principal** — `baseUrl` predefinido `http://localhost:5012` (`Lightcode.Registration/Properties/launchSettings.json`).

**Email API** — `baseUrl` predefinido `http://localhost:5013` (`Lightcode.Registration.EmailApi/Properties/launchSettings.json`); `registrationApiUrl` aponta para `http://localhost:5012`.

Variáveis comuns: `tenantId`, `jwt`, `loginUsername` / `loginPassword`, `provisioningKey`, `schemaId` (só na coleção principal).

## Roles (`admin` / `user`)

- No MongoDB, o utilizador tem **`roles`** (array), ex.: `["user"]` ou `["admin","user"]`.
- Registo público define **`roles: ["user"]`**.
- Em **Development**, o bootstrap cria **`roles: ["admin"]`** para o utilizador inicial.
- No JWT, cada role aparece como claim **`role`** repetida.
- **Account JSON Schemas**: criar/atualizar/apagar exige **admin**; listar/obter por id bastam utilizador do tenant.
- **Email API**: autorização por roles (`template-read`, `template-write`, `send-email`) ou scope `email-admin` (acesso total).

## Ordem sugerida (API principal)

1. **Create Tenant** → copiar `id` para `tenantId`.
2. **Issue Token** (admin de bootstrap) → copiar `access_token` para `jwt`.
3. Opcional: **Register Account** + novo **Issue Token** para token só `user`.
4. Pedidos autenticados em schemas conforme roles acima.

## Ordem sugerida (Email API)

1. Obter `jwt` na coleção **Lightcode.Registration** (mesmo ambiente LOCAL/DOCKER).
2. **List Email Templates** / **Create Email Template** (admin).
3. **Send Email** com `templateKey` ou `templateId` (admin).

## Docker (resumo de portas)

- API principal **8080**, Email API **8081**, RabbitMQ management **15672**, AMQP **5672**, Mongo **27017** (conforme `docker-compose.yml` deste repositório).
