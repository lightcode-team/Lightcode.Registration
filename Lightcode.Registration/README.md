# Lightcode.Registration - API principal

API multi-tenant para cadastro de contas, autenticacao OAuth/JWT, gestao de JSON Schemas e provisionamento de tenants.

O sistema possui dois niveis de identidade:

- **ADM central da plataforma**: fica no banco master e pode controlar um ou mais tenants pelo mesmo email.
- **Usuario do tenant**: fica dentro do banco isolado do tenant e usa os endpoints de conta/cadastro daquele tenant.

## Pre-requisitos

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- MongoDB (`mongodb://127.0.0.1:27017`)
- RabbitMQ para enfileirar emails de convite, confirmacao, reset e notificacoes

Subir dependencias locais:

```bash
docker compose up -d mongo rabbitmq
```

Executar a API:

```bash
dotnet run --project Lightcode.Registration/Lightcode.Registration.csproj --launch-profile http
```

| Ambiente | URL |
|----------|-----|
| Development HTTP | `http://localhost:5012` |
| Development HTTPS | `https://localhost:7035` |
| Docker Compose | `http://localhost:8080` |

OpenAPI em Development: `GET /openapi/v1.json`.

## Arquitetura de dados

| Banco | Conteudo |
|-------|----------|
| `SaasMasterDb` | `Tenants`, `PlatformAdmins`, `PlatformAdminTenantLinks`, `PlatformAdminInvites` |
| `tenant_{id}` | `Users`, `OAuthClients`, `RefreshTokens`, `Settings`, `EmailTemplates`, `AccountJsonSchemas` |

Cada tenant tem banco MongoDB dedicado. O isolamento e fisico por database, nao apenas por campo `TenantId`.

## Conceitos de credenciamento

### ADM central

O ADM central representa uma pessoa ou operador do painel administrativo. Exemplo:

```text
frederick.aquino@gmail.com controla:
- FinAi
- AFSSolutions
- SacaPay
```

Esse email existe uma vez no master em `PlatformAdmins`. O vinculo com cada tenant fica em `PlatformAdminTenantLinks`.

O ADM central nao precisa receber nem guardar `client_secret` de cada tenant. Ele faz login uma vez no painel central, lista os tenants vinculados e solicita um token especifico para o tenant selecionado.

### Usuario do tenant

O usuario do tenant fica dentro de `tenant_{id}.Users`. Ele pertence a apenas um tenant por documento e usa `grant_type=password` em `POST /api/auth/token` com `X-Tenant-Id`.

Usuarios podem ter roles como:

- `user`
- `admin`
- roles customizadas usadas pelas politicas da API

Um usuario com role `admin` dentro do tenant pode gerenciar contas e schemas daquele tenant, mas isso nao o torna ADM central da plataforma.

### Cliente OAuth

Cada tenant ainda possui `OAuthClients` para integracoes maquina-a-maquina. O fluxo `client_credentials` continua existindo e usa `client_id` + `client_secret`.

Use OAuth client para integracoes tecnicas. Use ADM central para painel humano multi-tenant.

## Fluxo completo: criar tenant e credenciar ADM central

### 1. Criar tenant

```http
POST /api/tenants
X-Provisioning-Key: {provisioningKey}
Content-Type: application/json
```

```json
{
  "name": "FinAi",
  "adminEmail": "frederick.aquino@gmail.com"
}
```

Ao criar o tenant, a API:

- cria registro em `SaasMasterDb.Tenants`;
- cria banco `tenant_{id}`;
- semeia schema default de conta;
- semeia templates de email;
- semeia SMTP do tenant em `tenant_{id}.Settings`;
- cria um OAuth client principal para uso tecnico;
- cria ou reutiliza o ADM central pelo `adminEmail`;
- vincula o ADM central ao tenant como `owner`;
- envia convite de ativacao para o ADM central se ele ainda nao estiver ativo.

Se o email ja existir como ADM central ativo, a API apenas adiciona o vinculo ao novo tenant. Nao cria outra senha e nao duplica o operador.

### 2. Ativar o ADM central

O convite gera um `inviteToken`. Ele pode vir por email ou ser copiado da resposta de desenvolvimento/Bruno.

```http
POST /api/platform-admins/activate
Content-Type: application/json
```

```json
{
  "token": "{inviteToken}",
  "password": "ChangeThisPlatform!12345678"
}
```

Depois disso, o ADM fica ativo em `PlatformAdmins`.

### 3. Login central do painel

```http
POST /api/platform-auth/token
Content-Type: application/json
```

```json
{
  "email": "frederick.aquino@gmail.com",
  "password": "ChangeThisPlatform!12345678"
}
```

Resposta:

```json
{
  "access_token": "jwt-central",
  "token_type": "Bearer",
  "expires_in": 7200
}
```

Esse JWT central possui `token_use=platform_admin` e `platformAdminId`. Ele nao possui `tenantId` e serve apenas para endpoints `/api/platform/*`.

### 4. Listar tenants do ADM central

```http
GET /api/platform/tenants
Authorization: Bearer {platformJwt}
```

Resposta:

```json
[
  {
    "id": "tenant-id-finai",
    "name": "FinAi",
    "role": "owner"
  },
  {
    "id": "tenant-id-afs",
    "name": "AFSSolutions",
    "role": "owner"
  }
]
```

O painel usa essa resposta para mostrar o seletor de tenants.

### 5. Selecionar tenant e emitir JWT tenant-scoped

```http
POST /api/platform/tenants/{tenantId}/token
Authorization: Bearer {platformJwt}
```

Resposta:

```json
{
  "tenant_id": "tenant-id-finai",
  "token": {
    "access_token": "jwt-do-tenant",
    "token_type": "Bearer",
    "expires_in": 7200
  }
}
```

Esse JWT possui:

- `tenantId`
- `sub` com o `PlatformAdminId`
- `email`
- `role=admin`
- `scope=owner`
- issuer/audience do tenant

Use esse `access_token` nos endpoints existentes do tenant, como:

- `GET /api/accounts`
- `POST /api/accounts/admin`
- `GET /api/account-json-schemas`
- `POST /api/account-json-schemas`
- `GET /api/oauth-clients`

Se o ADM tentar emitir token para um tenant sem vinculo ativo, a API retorna `403`.

## Fluxo completo: usuario do tenant

### 1. Cadastro publico parcial

Pedidos anonimos identificam o tenant por header `X-Tenant-Id`.

```http
POST /api/accounts
X-Tenant-Id: {tenantId}
Content-Type: application/json
```

```json
{
  "schemaId": "default",
  "email": "usuario@example.com",
  "username": "usuario",
  "password": "ChangeThis!12345678"
}
```

A conta nasce como `Incomplete`. O cadastro publico e parcial: campos obrigatorios da raiz do schema podem ser completados depois.

### 2. Login do usuario

```http
POST /api/auth/token
X-Tenant-Id: {tenantId}
Content-Type: application/json
```

```json
{
  "grant_type": "password",
  "username": "usuario",
  "password": "ChangeThis!12345678"
}
```

O token de usuario possui `tenantId` e roles salvas em `Users.roles`.

### 3. Atualizar cadastro por etapas

```http
PUT /api/accounts/{userId}
Authorization: Bearer {jwt}
Content-Type: application/json
```

```json
{
  "phone": "85999999999",
  "document": "00000000000"
}
```

O proprio usuario pode atualizar a propria conta. Admins do tenant tambem podem atualizar contas.

### 4. Concluir cadastro

```http
POST /api/accounts/{userId}/complete-register
Authorization: Bearer {jwt}
Content-Type: application/json
```

```json
{
  "confirmationReturnUrl": "https://app.example.com/confirmado"
}
```

A conclusao valida todos os `required` da raiz do JSON Schema. Se o schema tiver `2FA.Active=true`, a conta fica `PendingConfirmation`; caso contrario, fica `Active`.

### 5. Confirmar email quando houver 2FA

Modo codigo:

```http
POST /api/accounts/confirm-email-code/{code}
X-Tenant-Id: {tenantId}
Content-Type: application/json
```

```json
{
  "email": "usuario@example.com"
}
```

Modo link:

```http
GET /api/accounts/confirm-email/{token}?tenantId={tenantId}&email={email}
```

## Criacao administrativa de usuario

Um ADM central que ja emitiu token do tenant, ou um usuario admin do tenant, pode criar usuarios diretamente:

```http
POST /api/accounts/admin
Authorization: Bearer {tenantJwt}
Content-Type: application/json
```

```json
{
  "schemaId": "default",
  "email": "novo.usuario@example.com",
  "username": "novo.usuario",
  "password": "ChangeThis!12345678",
  "roles": ["user"]
}
```

Esse endpoint usa validacao completa do schema e permite definir roles.

Para transformar um usuario comum em admin do tenant:

```http
PUT /api/accounts/{userId}/roles
Authorization: Bearer {tenantJwt}
Content-Type: application/json
```

```json
{
  "roles": ["admin"]
}
```

## Autenticacao e tenant

| Contexto | Como identificar o tenant |
|----------|---------------------------|
| Pedido anonimo de usuario | Header `X-Tenant-Id` |
| Pedido autenticado de usuario/admin tenant | Claim JWT `tenantId` |
| Painel central | JWT central com `token_use=platform_admin` |

### Tokens disponiveis

| Endpoint | Token retornado | Uso |
|----------|-----------------|-----|
| `POST /api/platform-auth/token` | JWT central | Listar tenants e emitir token de tenant |
| `POST /api/platform/tenants/{tenantId}/token` | JWT tenant-scoped para ADM central | Operar endpoints do tenant como admin/owner |
| `POST /api/auth/token` com `password` | JWT tenant-scoped de usuario | Operar como usuario ou admin local do tenant |
| `POST /api/auth/token` com `client_credentials` | JWT tenant-scoped de cliente OAuth | Integracoes maquina-a-maquina |
| `POST /api/auth/token` com `refresh_token` | Novo JWT tenant-scoped | Renovar sessao existente |

## Endpoints

### Platform Admins

| Metodo | Rota | Auth | Descricao |
|--------|------|------|-----------|
| POST | `/api/platform-admins/invite` | `X-Provisioning-Key` | Cria convite de ADM central e vincula tenants |
| POST | `/api/platform-admins/activate` | Publico com token | Ativa convite e define senha |
| POST | `/api/platform-auth/token` | Publico | Login central do ADM |
| GET | `/api/platform/tenants` | JWT central | Lista tenants vinculados ao ADM |
| POST | `/api/platform/tenants/{tenantId}/token` | JWT central | Emite JWT tenant-scoped para operar o tenant |

### Tenants

| Metodo | Rota | Auth | Descricao |
|--------|------|------|-----------|
| POST | `/api/tenants` | `X-Provisioning-Key` | Cria tenant, schema default, templates, OAuth client e vinculo com ADM central |

### Auth tenant-scoped

| Metodo | Rota | Auth | Descricao |
|--------|------|------|-----------|
| POST | `/api/auth/token` | `X-Tenant-Id` | Emite JWT + refresh token para `password`, `refresh_token` ou `client_credentials` |

### Contas

| Metodo | Rota | Auth | Descricao |
|--------|------|------|-----------|
| POST | `/api/accounts` | `X-Tenant-Id` | Cadastro publico parcial |
| POST | `/api/tenants/{tenantId}/accounts` | Publico | Rota legada de cadastro |
| GET | `/api/accounts` | JWT `admin` ou `owner` | Lista contas do tenant |
| GET | `/api/accounts/{userId}` | JWT `admin` ou `owner` | Detalhe da conta |
| POST | `/api/accounts/admin` | JWT `admin` ou `owner` | Cria usuario como administrador |
| PUT | `/api/accounts/{userId}` | JWT | Atualizacao parcial |
| POST | `/api/accounts/{userId}/complete-register` | JWT | Conclui cadastro |
| PUT | `/api/accounts/{userId}/roles` | JWT `admin` ou `owner` | Atualiza roles |
| POST | `/api/accounts/confirm-email-code/{code}` | `X-Tenant-Id` | Confirmacao por codigo |
| GET | `/api/accounts/confirm-email/{token}` | Query `tenantId`, `email` | Confirmacao por link |
| POST | `/api/accounts/forgot-password` | `X-Tenant-Id` | Solicita reset por email ou username |

### JSON Schemas de conta

| Metodo | Rota | Auth | Descricao |
|--------|------|------|-----------|
| GET | `/api/account-json-schemas` | JWT | Lista schemas |
| GET | `/api/account-json-schemas/{id}` | JWT | Obtem schema |
| POST | `/api/account-json-schemas` | JWT `admin` | Cria schema |
| PUT | `/api/account-json-schemas/{id}` | JWT `admin` | Atualiza schema |
| DELETE | `/api/account-json-schemas/{id}` | JWT `admin` | Remove schema |

### OAuth Clients

| Metodo | Rota | Auth | Descricao |
|--------|------|------|-----------|
| GET | `/api/oauth-clients` | JWT `clients-read` ou `owner` | Lista clientes |
| GET | `/api/oauth-clients/me` | JWT | Cliente do token atual |
| POST | `/api/oauth-clients` | JWT `clients-write` ou `owner` | Cria cliente |
| PUT | `/api/oauth-clients/me` | JWT `clients-write` ou `owner` | Atualiza cliente autenticado |
| DELETE | `/api/oauth-clients/me` | JWT `clients-write` ou `owner` | Desativa cliente autenticado |

## JSON Schema de conta

Cada tenant pode ter varios schemas em `AccountJsonSchemas`.

Campos principais da API:

| Campo | Obrigatorio | Descricao |
|-------|-------------|-----------|
| `key` | Sim | Identificador unico por tenant, como `default` ou `premium-v1` |
| `displayName` | Nao | Nome amigavel |
| `schemaJson` | Sim | Objeto JSON Schema |
| `config` | Nao | Regras de negocio opcionais |
| `isDefault` | Nao | Se `true`, vira schema default e atualiza validador Mongo de `Users` |

Todo schema de conta deve declarar:

- `email`
- `username`
- `password`

Schema default criado no provisionamento:

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

### Modos de validacao

| Endpoint | Modo | Comportamento |
|----------|------|---------------|
| `POST /api/accounts` | Parcial | Ignora `required` da raiz; valida campos enviados |
| `PUT /api/accounts/{userId}` | Parcial | Faz merge e valida campos enviados |
| `POST /api/accounts/{userId}/complete-register` | Completo | Exige todos os `required` da raiz |
| `POST /api/accounts/admin` | Completo | Exige documento completo |

### Config do schema

`validateDuplicateEmail`:

| Tipo | Default | Descricao |
|------|---------|-----------|
| `boolean` ou string `"true"`/`"false"` | `true` | Se `false`, nao bloqueia email duplicado |

`Expiry`:

| Campo | Tipo | Descricao |
|-------|------|-----------|
| `expiryRegister` | `boolean` | Define expiracao para conta ativa |
| `daysExpiry` | `int` | Dias ate expirar |

`2FA`:

| Campo | Tipo | Descricao |
|-------|------|-----------|
| `Active` | `boolean` | Liga/desliga confirmacao por email |
| `Type` | `"Code"` ou `"Link"` | Tipo de confirmacao |

## Status da conta

| Valor | Significado |
|-------|-------------|
| `Incomplete` | Cadastro iniciado e ainda incompleto |
| `PendingConfirmation` | Aguardando confirmacao de email |
| `Active` | Conta ativa |
| `Expired` | Cadastro expirado |

## SMTP e emails

O envio real e feito pelo Worker. Para enviar email real:

```env
Smtp__UseSmtp=true
```

As credenciais usadas no envio ficam no tenant:

```text
tenant_{id}.Settings
_id = "smtp"
```

`TenantDefaultSmtp__*` serve para semear esse documento quando um tenant novo e criado. Alterar `.env` depois nao atualiza tenants ja existentes.

Para Gmail na porta 587, use App Password e `UsarSsl=true`:

```json
{
  "Host": "smtp.gmail.com",
  "Port": 587,
  "Usuario": "contato@example.com",
  "Senha": "app-password",
  "EmailRemetente": "contato@example.com",
  "NomeRemetente": "Lightcode",
  "UsarSsl": true
}
```

## Configuracao

| Secao | Descricao |
|-------|-----------|
| `Mongo:ConnectionString` | Conexao MongoDB |
| `Mongo:MasterDatabaseName` | Banco master, default `SaasMasterDb` |
| `Jwt:SigningKey` | Chave HMAC, minimo 32 caracteres |
| `Master:ProvisioningApiKey` | Chave para criar tenants e convidar ADMs centrais |
| `Registration:PublicApiBaseUrl` | URL publica para links de email |
| `RabbitMQ:*` | Publicacao/consumo de mensagens |
| `TenantDefaultSmtp:*` | SMTP inicial gravado em novos tenants |
| `Smtp:UseSmtp` | Liga/desliga envio real no Worker |

Variaveis de ambiente usam `__` como separador:

```env
Mongo__ConnectionString=mongodb://localhost:27017
Master__ProvisioningApiKey=123456789
Smtp__UseSmtp=true
```

## Testes com Bruno

Colecao: [`bruno/Lightcode.Registration`](../bruno/Lightcode.Registration)

Ambiente local: `environments/LOCAL.bru` com `baseUrl: http://localhost:5012`.

Fluxo sugerido para ADM central:

1. **Create Tenant**: cria tenant e vincula `adminEmail`.
2. **Platform / Activate Platform Admin**: ativa o convite com `platformInviteToken`.
3. **Platform / Platform Auth Token**: copia `access_token` para `platformJwt`.
4. **Platform / List Platform Tenants**: lista tenants vinculados.
5. **Platform / Issue Tenant Token**: copia `token.access_token` para `jwt`.
6. Usar `jwt` nos endpoints de contas, schemas e OAuth clients.

Fluxo sugerido para usuario:

1. **Accounts / Register**: cria conta parcial com `X-Tenant-Id`.
2. **Auth / Issue Token**: login do usuario.
3. **Accounts / Update**: completa campos por etapas.
4. **Accounts / Complete Register**: conclui e ativa ou envia confirmacao 2FA.

## Projetos relacionados

| Projeto | Funcao |
|---------|--------|
| [Lightcode.Registration.EmailApi](../Lightcode.Registration.EmailApi/README.md) | CRUD de templates e envio de emails |
| [Lightcode.Registration.Worker](../Lightcode.Registration.Worker/README.md) | Consumo RabbitMQ, emails SMTP e expiracao |
