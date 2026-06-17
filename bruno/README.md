# Colecoes Bruno - Lightcode.Registration

Existem **duas colecoes** (uma por host HTTP), cada uma com o seu `bruno.json`:

| Pasta | Projeto / host | Pedidos |
|--------|----------------|---------|
| `bruno/Lightcode.Registration/` | `Lightcode.Registration` | tenants, auth, discovery/JWKS, accounts, account-json-schemas |
| `bruno/Lightcode.Registration.EmailApi/` | `Lightcode.Registration.EmailApi` | emails (`/api/emails/send`), templates (`/api/email-templates`) |

## Requisitos

- [Bruno](https://www.usebruno.com/) instalado (desktop ou CLI).

## Abrir no Bruno

1. **Open Collection** -> escolha **`bruno/Lightcode.Registration`** (API principal) **ou** **`bruno/Lightcode.Registration.EmailApi`** (email).
2. Selecione o ambiente **LOCAL** ou **DOCKER** dentro dessa colecao (cada colecao tem os seus `environments/`).

Para testar a Email API precisa de um JWT valido: use primeiro a colecao da API principal (**Create Tenant**, **Issue Token** ou **Client Credentials**) e copie o `access_token` para a variavel `jwt` na colecao da Email API. Tokens tenant-scoped sao RS256 e podem ser validados pelo JWKS publico do tenant.

## Ambiente DOCKER

Use quando **api**, **email-api**, **mongo** e **rabbitmq** estiverem a correr via `docker compose` na raiz do projeto.

**Colecao API principal** (`Lightcode.Registration/environments/DOCKER.bru`):

| Variavel | Valor predefinido | Notas |
|----------|-------------------|--------|
| `baseUrl` | `http://localhost:8082` | API principal |
| `rabbitManagementUrl` | `http://localhost:15672` | UI RabbitMQ (guest/guest) - referencia |
| `mongoUrl` | `mongodb://localhost:27017` | Referencia; pedidos HTTP vao a API |
| `provisioningKey` | *(vazio)* | Alinhar com `Master__ProvisioningApiKey` |
| `tenantId` / `jwt` / `schemaId` | *(exemplo ou vazio)* | Preencher apos criar tenant e emitir token contra este `baseUrl` |
| `tenantAuthority` / `openIdConfigurationUrl` / `jwksUrl` | derivados de `baseUrl` + `tenantId` | Usados para validar tokens RS256 em APIs cliente |
| `tokenAudience` | `api-do-cliente` | Audience esperado por uma API cliente externa |

**Colecao Email API** (`Lightcode.Registration.EmailApi/environments/DOCKER.bru`):

| Variavel | Valor predefinido | Notas |
|----------|-------------------|--------|
| `baseUrl` | `http://localhost:8081` | Servico `email-api` |
| `registrationApiUrl` | `http://localhost:8080` | Referencia a API principal |
| `jwt` | *(exemplo)* | Emitir de novo com **Issue Token** ou **Client Credentials** na API principal |
| `emailTemplateId` | *(vazio)* | Copiar de **List Email Templates** |

## Ambiente LOCAL

**API principal** - `baseUrl` predefinido `http://localhost:5012` (`Lightcode.Registration/Properties/launchSettings.json`).

**Email API** - `baseUrl` predefinido `http://localhost:5013` (`Lightcode.Registration.EmailApi/Properties/launchSettings.json`); `registrationApiUrl` aponta para `http://localhost:5012`.

Variaveis comuns: `tenantId`, `jwt`, `loginUsername` / `loginPassword`, `provisioningKey`, `schemaId` (so na colecao principal). A colecao principal tambem inclui `tenantAuthority`, `openIdConfigurationUrl`, `jwksUrl` e `tokenAudience` para testes de discovery/JWKS.

## Roles (`admin` / `user`)

- No MongoDB, o utilizador tem **`roles`** (array), ex.: `["user"]` ou `["admin","user"]`.
- Registo publico define **`roles: ["user"]`**.
- Em **Development**, o bootstrap cria **`roles: ["admin"]`** para o utilizador inicial.
- No JWT, cada role aparece como claim **`role`** repetida.
- **Account JSON Schemas**: criar/atualizar/apagar exige **admin**; listar/obter por id bastam utilizador do tenant.
- **Email API**: autorizacao por roles (`template-read`, `template-write`, `send-email`) ou scope `email-admin` (acesso total).

## Ordem sugerida (API principal)

1. **Create Tenant** -> copiar `id` para `tenantId`.
2. **Issue Token** (admin de bootstrap) ou **Client Credentials** -> copiar `access_token` para `jwt`.
3. **Discovery / OpenID Configuration** e **Discovery / JWKS** -> validar metadados publicos do tenant.
4. Opcional: **Register Account** + novo **Issue Token** para token so `user`.
5. Pedidos autenticados em schemas conforme roles acima.

## Ordem sugerida (Email API)

1. Obter `jwt` na colecao **Lightcode.Registration** (mesmo ambiente LOCAL/DOCKER).
2. **List Email Templates** / **Create Email Template** (admin).
3. **Send Email** com `templateKey` ou `templateId` (admin).

## Docker (resumo de portas)

- API principal **8082**, Email API **8081**, RabbitMQ management **15672**, AMQP **5672**, Mongo **27017** (conforme `docker-compose.yml` deste repositorio).
