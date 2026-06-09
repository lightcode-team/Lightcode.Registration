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
| POST | `/api/accounts` | `X-Tenant-Id` | Registo público |
| POST | `/api/tenants/{tenantId}/accounts` | — | Registo (rota legada) |
| GET | `/api/accounts` | JWT (`admin` / `owner`) | Listar contas |
| GET | `/api/accounts/{userId}` | JWT (`admin` / `owner`) | Detalhe da conta |
| POST | `/api/accounts/admin` | JWT (`admin` / `owner`) | Registo por administrador |
| PUT | `/api/accounts/{userId}` | JWT | Atualização parcial |
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
3. **Register** / **AccountJsonSchemas** / **Forgot Password**

## Projetos relacionados

| Projeto | Função |
|---------|--------|
| [Lightcode.Registration.EmailApi](../Lightcode.Registration.EmailApi/README.md) | CRUD de templates e envio de emails |
| [Lightcode.Registration.Worker](../Lightcode.Registration.Worker/README.md) | Consumo RabbitMQ e envio SMTP |
