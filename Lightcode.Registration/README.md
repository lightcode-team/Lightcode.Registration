# Lightcode.Registration — API principal

API multi-tenant de registo de contas, autenticação OAuth/JWT, gestão de schemas JSON e provisionamento de tenants.

## Pré-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MongoDB (`mongodb://127.0.0.1:27017`)
- RabbitMQ (para enfileirar emails — credenciais OAuth, confirmação, reset de senha)

Subir dependências locais:

```bash
docker compose up -d mongo rabbitmq
```

## Executar

```bash
dotnet run --project Lightcode.Registration/Lightcode.Registration.csproj --launch-profile http
```

| Ambiente | URL |
|----------|-----|
| Development (HTTP) | http://localhost:5012 |
| Development (HTTPS) | https://localhost:7035 |
| Docker Compose | http://localhost:8080 |

OpenAPI (apenas Development): `GET /openapi/v1.json`

## Arquitetura de dados

| Banco | Conteúdo |
|-------|----------|
| `SaasMasterDb` | Coleção `Tenants` (metadados) |
| `tenant_{id}` | `Users`, `OAuthClients`, `RefreshTokens`, `Settings`, `EmailTemplates`, `AccountJsonSchemas` |

Cada tenant tem base MongoDB isolada. O isolamento é físico (banco dedicado), não apenas por campo `TenantId`.

## Autenticação e tenant

| Contexto | Como identificar o tenant |
|----------|---------------------------|
| Pedidos anónimos | Cabeçalho `X-Tenant-Id` |
| Pedidos autenticados | Claim JWT `tenantId` |

### Obter token

`POST /api/auth/token` com `X-Tenant-Id`

| `grant_type` | Uso |
|--------------|-----|
| `password` | Utilizador + senha |
| `refresh_token` | Renovar sessão |
| `client_credentials` | Cliente OAuth (`client_id` + `client_secret`) |

## Endpoints

### Tenants

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/tenants` | `X-Provisioning-Key` (se configurado) | Cria tenant, schema default, templates e cliente OAuth |

### Auth

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/auth/token` | `X-Tenant-Id` | Emite JWT + refresh token |

### Contas (`/api/accounts`)

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| POST | `/api/accounts` | `X-Tenant-Id` | Registo público (parcial; `status: Incomplete`) |
| POST | `/api/tenants/{tenantId}/accounts` | — | Registo (rota legada) |
| GET | `/api/accounts` | JWT (`admin` / `owner`) | Listar contas |
| GET | `/api/accounts/{userId}` | JWT (`admin` / `owner`) | Detalhe da conta |
| POST | `/api/accounts/admin` | JWT (`admin` / `owner`) | Registo por administrador |
| PUT | `/api/accounts/{userId}` | JWT | Atualização parcial (validação parcial do schema) |
| POST | `/api/accounts/{userId}/complete-register` | JWT | Concluir cadastro por steps (validação completa + ativação) |
| PUT | `/api/accounts/{userId}/roles` | JWT (`admin` / `owner`) | Atualizar roles |
| POST | `/api/accounts/confirm-email-code/{code}` | `X-Tenant-Id` | Confirmação 2FA (código) |
| GET | `/api/accounts/confirm-email/{token}` | Query `tenantId`, `email` | Confirmação 2FA (link) |
| POST | `/api/accounts/forgot-password` | `X-Tenant-Id` | Pedido de reset (email **ou** username) |

### JSON Schemas de conta

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/account-json-schemas` | JWT | Listar |
| GET | `/api/account-json-schemas/{id}` | JWT | Obter por id |
| POST | `/api/account-json-schemas` | JWT (`admin`) | Criar |
| PUT | `/api/account-json-schemas/{id}` | JWT (`admin`) | Atualizar |
| DELETE | `/api/account-json-schemas/{id}` | JWT (`admin`) | Apagar |

## JSON Schema de conta

Cada tenant pode ter vários schemas na coleção `AccountJsonSchemas`. Cada schema define **como validar o corpo de cadastro/atualização** (`schemaJson`) e **regras de negócio opcionais** (`config`).

O motor de validação usa [JsonSchema.Net](https://github.com/gregsdennis/json-everything) (draft). Quando um schema é marcado como `isDefault: true`, o validador Mongo da coleção `Users` é atualizado automaticamente (tipos e estrutura; `required` da raiz **não** é imposto no Mongo — ver abaixo).

### Campos do schema (API)

| Campo | Obrigatório | Descrição |
|-------|-------------|-----------|
| `key` | Sim | Identificador único por tenant (ex.: `default`, `premium-v1`) |
| `displayName` | Não | Nome amigável |
| `schemaJson` | Sim | Objeto JSON Schema (não string) |
| `config` | Não | Regras de negócio (ver secção abaixo) |
| `isDefault` | Não | Se `true`, torna-se o schema default e atualiza o validador Mongo de `Users` |

No registo, o cliente envia `schemaId` no corpo (valor da `key` ou do `id` do schema).

### Requisitos do `schemaJson`

Todo schema de conta **deve** declarar em `properties` e em `required` da raiz:

- `email` — `type: string`, recomendado `format: email`
- `username` — `type: string`
- `password` — `type: string`

Campos adicionais (`phone`, `document`, objetos aninhados, etc.) são livres. Recomenda-se `additionalProperties: true` para extensibilidade.

**Schema default** criado no provisionamento do tenant:

```json
{
  "type": "object",
  "required": ["email", "username", "password"],
  "additionalProperties": true,
  "properties": {
    "email": { "type": "string", "format": "email" },
    "username": { "type": "string", "minLength": 1 },
    "password": { "type": "string", "minLength": 8 }
  }
}
```

### Exemplo com objeto aninhado

Propriedades opcionais na raiz podem ser preenchidas em steps. Se um **objeto for enviado**, o `required` **interno** desse objeto é validado:

```json
{
  "type": "object",
  "required": ["email", "username", "password"],
  "additionalProperties": true,
  "properties": {
    "email": { "type": "string", "format": "email" },
    "username": { "type": "string", "minLength": 1 },
    "password": { "type": "string", "minLength": 8 },
    "address": {
      "type": "object",
      "required": ["bairro", "rua"],
      "properties": {
        "rua": { "type": "string", "minLength": 5 },
        "bairro": { "type": "string", "minLength": 5 },
        "numero": { "type": "string", "minLength": 5 }
      }
    }
  }
}
```

| Pedido | Resultado |
|--------|-----------|
| Sem `address` | Válido (cadastro parcial) |
| `address` com `rua` e `bairro` | Válido (`numero` é opcional) |
| `address` só com `rua` | Inválido — falta `bairro` |

### Modos de validação

| Endpoint | Modo | Comportamento |
|----------|------|---------------|
| `POST /api/accounts` | Parcial | Ignora `required` **da raiz**; valida tipos/formatos dos campos enviados; objetos aninhados enviados são validados por completo |
| `PUT /api/accounts/{userId}` | Parcial | Idem (merge sobre o documento existente) |
| `POST /api/accounts/{userId}/complete-register` | Completo | Exige todos os `required` da raiz; ativa a conta |
| `POST /api/accounts/admin` | Completo | Registo administrativo com validação total |

### Cadastro por steps (fluxo público)

1. **Registo** — `POST /api/accounts` com `email`, `username`, `password` (+ campos opcionais). Conta criada com `status: Incomplete`.
2. **Token** — `POST /api/auth/token` (login permitido em `Incomplete`).
3. **Atualizações** — `PUT /api/accounts/{userId}` para preencher campos adicionais step a step.
4. **Conclusão** — `POST /api/accounts/{userId}/complete-register` valida o documento completo contra o schema e define `status: Active` (ou `PendingConfirmation` se 2FA estiver ativo).

### `config` — configurações personalizadas

Objeto opcional com regras de negócio por schema. Propriedades reconhecidas:

#### `validateDuplicateEmail`

| Tipo | Default | Descrição |
|------|---------|-----------|
| `boolean` ou string `"true"` / `"false"` | `true` | Se `false`, o registo **não** verifica email duplicado (username continua único) |

#### `Expiry`

Controla expiração de cadastros **ativos** após a conclusão do registo.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `expiryRegister` | `boolean` | Se `true`, define `registrationExpiresAtUtc` na conclusão do registo |
| `daysExpiry` | `int` | Dias até expirar (obrigatório > 0 quando `expiryRegister` é `true`) |

Quando ativo, o **Worker** verifica periodicamente contas com `registrationExpiresAtUtc` vencido e define `status: Expired`. Também envia lembretes por email (30 e 15 dias antes). Contas `Incomplete` ou `PendingConfirmation` são ignoradas pelo scan.

Na **atualização** de conta já ativa, se o schema tiver `Expiry` ativo, a data de expiração é renovada.

#### `2FA`

Confirmação de email em duas etapas. Aplicada na **conclusão** do registo (`complete-register`) ou no registo por admin — não no registo público parcial.

| Campo | Tipo | Descrição |
|-------|------|-----------|
| `Active` | `boolean` | Liga/desliga confirmação por email |
| `Type` | `"Code"` ou `"Link"` | Obrigatório quando `Active` é `true` |

| `Type` | Comportamento | Endpoint de confirmação |
|--------|---------------|-------------------------|
| `Code` | Código numérico no email; opcional `confirmationReturnUrl` no `complete-register` para URL do frontend | `POST /api/accounts/confirm-email-code/{code}` |
| `Link` | Link com token no email | `GET /api/accounts/confirm-email/{token}?tenantId=...&email=...` |

Templates de email seed no tenant: `email-confirmation-code` e `email-confirmation-link`. O código/link expira em **30 minutos**.

### Exemplo completo de criação

`POST /api/account-json-schemas` (JWT com role `admin`):

```json
{
  "key": "custom-v1",
  "displayName": "Schema personalizado",
  "config": {
    "validateDuplicateEmail": "false",
    "Expiry": {
      "expiryRegister": false,
      "daysExpiry": 360
    },
    "2FA": {
      "Active": true,
      "Type": "Code"
    }
  },
  "schemaJson": {
    "type": "object",
    "required": ["email", "username", "password"],
    "additionalProperties": true,
    "properties": {
      "email": { "type": "string", "format": "email" },
      "username": { "type": "string", "minLength": 1 },
      "password": { "type": "string", "minLength": 8 }
    }
  },
  "isDefault": false
}
```

> `validateDuplicateEmail` aceita boolean ou string (`"false"`). As chaves `Expiry` e `2FA` são case-insensitive na desserialização.

### Status da conta (`Users.status`)

| Valor | Significado |
|-------|-------------|
| `Incomplete` | Cadastro iniciado; faltam campos ou conclusão |
| `PendingConfirmation` | Aguardando confirmação de email (2FA) |
| `Active` | Conta ativa |
| `Expired` | Cadastro expirado (`Expiry` ativo) |

### Validador Mongo (`Users`)

O schema default é convertido para `$jsonSchema` do MongoDB (tipos em `properties`). O array `required` da raiz **não** é aplicado no Mongo, para permitir documentos parciais durante o cadastro por steps. A validação de campos obrigatórios fica na aplicação (`complete-register`).

Tenants já provisionados precisam **reaplicar** o schema default (atualizar o schema com `isDefault: true`) para alinhar o validador Mongo após esta mudança.

### Clientes OAuth

| Método | Rota | Auth | Descrição |
|--------|------|------|-----------|
| GET | `/api/oauth-clients` | JWT (`clients-read` / `owner`) | Listar |
| GET | `/api/oauth-clients/me` | JWT | Cliente do token (`client_id`) |
| POST | `/api/oauth-clients` | JWT (`clients-write` / `owner`) | Criar |
| PUT | `/api/oauth-clients/me` | JWT (`clients-write` / `owner`) | Atualizar cliente autenticado |
| DELETE | `/api/oauth-clients/me` | JWT (`clients-write` / `owner`) | Desativar cliente autenticado |

### Página web — reset de senha

| Método | Rota | Descrição |
|--------|------|-----------|
| GET | `/reset-password?token=...&tenantId=...&email=...` | Formulário (nova senha + confirmar) |
| POST | `/reset-password` | Submissão do formulário |

O link é enviado por email após `forgot-password` (processado pelo **Worker**).

## Configuração (`appsettings.json`)

| Secção | Descrição |
|--------|-----------|
| `Mongo:ConnectionString` | Ligação MongoDB |
| `Mongo:MasterDatabaseName` | Banco master (default: `SaasMasterDb`) |
| `Jwt:SigningKey` | Chave HMAC (mín. 32 caracteres) |
| `Master:ProvisioningApiKey` | Chave para `POST /api/tenants` |
| `Registration:PublicApiBaseUrl` | URL pública para links de email (confirmação, reset) |
| `RabbitMQ:*` | Publicação de mensagens de email |
| `TenantDefaultSmtp` | SMTP seed no provisionamento de tenant |

Variáveis de ambiente usam `__` como separador (ex.: `Mongo__ConnectionString`).

## Testes com Bruno

Coleção: [`bruno/Lightcode.Registration`](../bruno/Lightcode.Registration)

Ambiente local: `environments/LOCAL.bru` (`baseUrl: http://localhost:5012`)

Fluxo típico:

1. **Create Tenant** → guardar `tenantId` e credenciais OAuth do email
2. **Client Credentials** ou **Issue Token** → obter `jwt`
3. **Register** → **Issue Token** → **Update** → **Complete Register** (cadastro por steps) / **AccountJsonSchemas** / **Forgot Password**

## Projetos relacionados

| Projeto | Função |
|---------|--------|
| [Lightcode.Registration.EmailApi](../Lightcode.Registration.EmailApi/README.md) | CRUD de templates e envio de emails |
| [Lightcode.Registration.Worker](../Lightcode.Registration.Worker/README.md) | Consumo RabbitMQ e envio SMTP |
