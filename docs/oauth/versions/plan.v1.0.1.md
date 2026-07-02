# OAuth2 e Tokens v1.0.1

## Resumo

Evolução incremental do módulo OAuth para alinhar o backend com os contratos
administrativos esperados pelo front e pelas requests Bruno de OAuth Clients.

Esta versão mantém todos os comportamentos de `v1.0.0` e adiciona:

- gestão administrativa de OAuth Clients por `id` interno;
- persistência e exposição de `notifyEmail`;
- contrato de criação mais completo, com campos de detalhe mais
  `clientSecret`;
- testes unitários focados no `OAuthClientAppService`.

## Compatibilidade

Não houve breaking change nos endpoints existentes:

- `GET /api/oauth-clients`
- `GET /api/oauth-clients/me`
- `POST /api/oauth-clients`
- `PUT /api/oauth-clients/me`
- `DELETE /api/oauth-clients/me`

Os endpoints `/me` continuam operando sobre o `client_id` do token atual, com
fallback para `sub`.

## Novos Endpoints

Todos exigem `Authorization: Bearer <jwt>` com claim `tenantId`.

- `GET /api/oauth-clients/{id}`: requer policy `OAuthClientsRead`.
- `PUT /api/oauth-clients/{id}`: requer policy `OAuthClientsWrite`.
- `DELETE /api/oauth-clients/{id}`: requer policy `OAuthClientsWrite`.

O parâmetro `{id}` é o `Id` interno do documento `OAuthClient` no Mongo. Ele
não aceita `clientId`, para evitar ambiguidade entre identificador público do
cliente OAuth e identificador administrativo do registro.

## Contratos

### CreateOAuthClientRequest

Payload ativo:

```json
{
  "displayName": "FinAi Integration",
  "notifyEmail": "admin@example.com",
  "redirect_uris": ["https://app.example.com/callback"],
  "post_logout_redirect_uris": ["https://app.example.com/logout"],
  "allowed_scopes": ["openid", "email"],
  "require_consent": false,
  "tokenConfig": {
    "accessTokenExpirationMinutes": 30,
    "refreshTokenExpirationDays": 30,
    "maxRefreshTokenUses": 5,
    "values": [
      { "type": "iss", "value": "FinAi" },
      { "type": "aud", "value": "FinAi.Api" },
      { "type": "role", "value": "send-email" }
    ]
  }
}
```

Regras de `notifyEmail`:

- opcional;
- normalizado com `Trim`;
- validado quando informado;
- persistido em `OAuthClient.NotifyEmail`;
- usado para envio opcional do secret via template
  `client-credentials-secret`.

### UpdateOAuthClientRequest

Passa a aceitar `notifyEmail` além dos campos já existentes:

- `displayName`
- `notifyEmail`
- `tokenConfig`
- `redirect_uris`
- `post_logout_redirect_uris`
- `allowed_scopes`
- `require_consent`

O update por `/me` e por `{id}` compartilha as mesmas validações de
`tokenConfig`, redirect URIs, post logout URIs, scopes e `notifyEmail`.

### OAuthClientDto

Listagem e detalhe retornam:

- `id`
- `clientId`
- `displayName`
- `notifyEmail`
- `tokenConfig`
- `redirect_uris`
- `post_logout_redirect_uris`
- `allowed_scopes`
- `require_consent`
- `active`
- `createdAtUtc`
- `updatedAtUtc`

### OAuthClientCreatedDto

A resposta de criação retorna os mesmos campos de `OAuthClientDto` mais
`clientSecret`, que continua sendo exibido somente uma vez:

- `id`
- `clientId`
- `clientSecret`
- `displayName`
- `notifyEmail`
- `tokenConfig`
- `redirect_uris`
- `post_logout_redirect_uris`
- `allowed_scopes`
- `require_consent`
- `active`
- `createdAtUtc`
- `updatedAtUtc`

## Persistência

`OAuthClient` recebeu o campo nullable `NotifyEmail`.

Não há migração obrigatória para Mongo:

- documentos antigos continuam validos com `NotifyEmail = null`;
- o class map usa `AutoMap`;
- `IgnoreExtraElements` continua habilitado.

## Application Service

`IOAuthClientAppService` agora expoe:

- `GetByIdAsync(tenantId, id)`
- `UpdateByIdAsync(tenantId, id, request)`
- `DeactivateByIdAsync(tenantId, id)`

`OAuthClientAppService` usa os metodos ja existentes do repositorio:

- `GetByIdAsync`
- `ReplaceAsync`
- `DeactivateAsync`

O fluxo de update foi compartilhado entre `/me` e `{id}` para manter paridade
de validação e comportamento.

## Testes

Cobertura adicionada:

- create persiste `NotifyEmail` e retorna `ClientSecret`, `Active`,
  `CreatedAtUtc`, `UpdatedAtUtc` e `NotifyEmail`;
- get by id retorna cliente ativo;
- update by id altera `displayName`, `notifyEmail`, `tokenConfig`,
  `redirect_uris`, `post_logout_redirect_uris`, `allowed_scopes` e
  `require_consent`;
- deactivate by id marca cliente como inativo;
- id inexistente retorna 404;
- `notifyEmail` inválido retorna 400.

## Bruno

Requests adicionadas:

- `OAuth Clients / Get OAuth Client`
- `OAuth Clients / Update OAuth Client By Id`
- `OAuth Clients / Deactivate OAuth Client`

Requests atualizadas:

- `OAuth Clients / Create OAuth Client` agora salva `oauthClientRecordId`,
  `oauthClientId` e `oauthClientSecret` a partir da resposta.
- `OAuth Clients / Update OAuth Client` agora envia `notifyEmail` e salva
  `oauthClientRecordId`.

Variáveis:

- `oauthClientRecordId`: id interno do documento `OAuthClient`, usado em
  `/api/oauth-clients/{id}`.
- `oauthClientId`: `client_id` público, usado nos grants OAuth e no login
  hospedado.

Verificação executada:

- `dotnet test`: 40 testes aprovados.
- `dotnet build`: solução compilada com sucesso.

Avisos conhecidos do build:

- vulnerabilidades NuGet existentes em `Microsoft.OpenApi`, `SharpCompress` e
  `Snappier`.

## Arquivos Alterados na Implementacao

- `Lightcode.Registration/Controllers/OAuthClientsController.cs`
- `Lightcode.Registration.Application/Abstractions/IOAuthClientAppService.cs`
- `Lightcode.Registration.Application/Contracts/OAuthClients/OAuthClientContracts.cs`
- `Lightcode.Registration.Application/OAuthClients/OAuthClientMapping.cs`
- `Lightcode.Registration.Application/Services/OAuthClientAppService.cs`
- `Lightcode.Registration.Domain/Entities/OAuthClient.cs`
- `Lightcode.Registration.Tests/OAuthClientAppServiceTests.cs`

## Próxima Evolução

`v1.1.0` continua reservado para hardening operacional de clients, rotação de
secrets, cobertura integrada de discovery/JWT e preparação para consentimento.
