# Coleção Bruno — Lightcode.Registration

## Requisitos

- [Bruno](https://www.usebruno.com/) instalado (desktop ou CLI).

## Abrir no Bruno

1. **Open Collection** → escolha a pasta `bruno` deste repositório (a que contém `bruno.json`).
2. No canto superior direito, selecione o ambiente **LOCAL** (desenvolvimento na máquina) ou **DOCKER** (`docker compose up` com as portas deste repositório).

## Ambiente DOCKER

Ficheiro: `environments/DOCKER.bru`

Use quando **api**, **email-api**, **mongo** e **rabbitmq** estiverem a correr via `docker compose` na raiz do projeto.

| Variável | Valor predefinido | Notas |
|----------|-------------------|--------|
| `baseUrl` | `http://localhost:8080` | API principal (`api` no compose) |
| `emailApiBaseUrl` | `http://localhost:8081` | API de email (`email-api`, mapeamento `8081:8080`) |
| `rabbitManagementUrl` | `http://localhost:15672` | UI de gestão RabbitMQ (guest/guest) — só referência para testes manuais |
| `mongoUrl` | `mongodb://localhost:27017` | Referência; os pedidos HTTP vão sempre às APIs |
| `provisioningKey` | *(vazio)* | Deve coincidir com `Master__ProvisioningApiKey` do compose (defina `MASTER_PROVISIONING_KEY` no `.env` ou nas variáveis do compose) |
| `tenantId` / `jwt` / `schemaId` | *(vazio)* | Preencher após **Create Tenant** e **Issue Token** contra as URLs DOCKER |
| `loginUsername` / `loginPassword` | admin / *(vazio)* | Só funcionam se o contentor tiver `Master__TenantBootstrapAdminPassword` (e user) configurados; caso contrário use **Register Account** + **Issue Token** |

O **JWT** emitido contra `localhost:5012` **não** serve no Docker se `Jwt__SigningKey` for diferente: emita token de novo com **Issue Token** apontando para `baseUrl` do ambiente DOCKER.

## Ambiente LOCAL

Ficheiro: `environments/LOCAL.bru`

| Variável | Descrição |
|----------|-----------|
| `baseUrl` | URL base da API (predefinido: `http://localhost:5012`, perfil HTTP do `launchSettings.json`) |
| `tenantId` | Id do tenant (preencher após **Create Tenant**; usado em `X-Tenant-Id` e nos pedidos públicos) |
| `jwt` | `access_token` devolvido por **Issue Token** (colar sem prefixo `Bearer`) |
| `loginUsername` / `loginPassword` | Credenciais para **Issue Token** (devem coincidir com o registo em **Register Account**) |
| `provisioningKey` | Valor de `Master:ProvisioningApiKey` (ex.: `appsettings` em Development) |
| `schemaId` | Id de um schema (copiar de **List Account JSON Schemas**) |

## Roles (`admin` / `user`)

- No MongoDB, o utilizador tem o campo **`roles`** (array de strings), ex.: `["user"]` ou `["admin","user"]`.
- Registo público define sempre **`roles: ["user"]`** (o cliente não pode impor `admin`).
- Em **Development**, o bootstrap cria **`roles: ["admin"]`** para o utilizador inicial.
- No JWT, cada role aparece como uma claim **`role`** repetida (padrão ASP.NET Core para `RequireRole`).
- **Criar / atualizar / apagar** JSON Schemas exige pelo menos uma claim **`role: admin`**. Listar / obter por id bastam utilizador autenticado no tenant.

## Ordem sugerida de pedidos

1. **Create Tenant** → copiar `id` da resposta para `tenantId` no ambiente LOCAL.
2. **Issue Token** com o **admin** de bootstrap (`loginUsername` / `loginPassword` alinhados ao `appsettings.Development.json`) → copiar `access_token` para `jwt` (para gerir schemas).
3. Opcional: **Register Account** (novo `user`) — header `X-Tenant-Id` + corpo `email`, `username`, `password`.
4. **Issue Token** com um `user` registado, se precisar de token só com permissões de utilizador.
5. Pedidos autenticados: **Create/Update/Delete** JSON Schemas apenas com token **admin**; **List/Get** com qualquer token do tenant.

A rota legada `POST /api/tenants/{tenantId}/accounts` continua disponível; sem `X-Tenant-Id`, o tenant vem do URL.

## Docker (resumo)

- Preferir o ambiente **DOCKER** em vez de editar o LOCAL.
- Portas expostas pelo `docker-compose.yml` deste repo: API **8080**, Email API **8081**, RabbitMQ management **15672**, AMQP **5672**, Mongo **27017**.
