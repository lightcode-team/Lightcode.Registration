# Coleção Bruno — Lightcode.Registration

## Requisitos

- [Bruno](https://www.usebruno.com/) instalado (desktop ou CLI).

## Abrir no Bruno

1. **Open Collection** → escolha a pasta `bruno` deste repositório (a que contém `bruno.json`).
2. No canto superior direito, selecione o ambiente **LOCAL**.

## Ambiente LOCAL

Ficheiro: `environments/LOCAL.bru`

| Variável | Descrição |
|----------|-----------|
| `baseUrl` | URL base da API (predefinido: `http://localhost:5012`, perfil HTTP do `launchSettings.json`) |
| `userId` | Valor de `sub` no JWT (ex.: `demo-user`) |
| `tenantId` | Id do tenant (preencher após **Create Tenant**; necessário para token e registo) |
| `jwt` | `access_token` devolvido por **Issue Token** (colar sem prefixo `Bearer`) |
| `provisioningKey` | Valor de `Master:ProvisioningApiKey` se configurado; em Development pode ficar vazio |
| `schemaId` | Id de um schema (copiar da listagem **List Account JSON Schemas**) |

## Ordem sugerida de pedidos

1. **Create Tenant** → copiar `id` da resposta para `tenantId`.
2. **Issue Token** → copiar `access_token` para `jwt`.
3. Pedidos autenticados (Weather, Account JSON Schemas).
4. **Register Account** — usa `tenantId` no URL; não precisa de `jwt`.

Se `X-Provisioning-Key` não for necessário em Development, remova o header no pedido **Create Tenant** ou deixe `provisioningKey` vazio conforme o Bruno enviar.

## Docker

Se a API correr em Docker na porta **8080**, altere no ambiente LOCAL:

`baseUrl`: `http://localhost:8080`
