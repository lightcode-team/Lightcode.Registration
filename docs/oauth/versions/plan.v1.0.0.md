# OAuth2 e Tokens v1.0.0

## Resumo

Baseline do módulo OAuth atual da Registration API.

O módulo combina:

- registry de `OAuthClient` por tenant;
- endpoint único de token em `/api/auth/token`;
- grants `password`, `refresh_token`, `client_credentials` e
  `authorization_code`;
- refresh tokens opacos por tenant;
- JWT tenant-scoped assinado com RS256;
- discovery/JWKS público por tenant;
- policies para gestão de clients por role ou scope `owner`.
- respostas JSON de controllers são envelopadas em `ApiEnvelope<T>`.

O fluxo de authorization code depende do login hospedado documentado em
`docs/sso-oidc`. Este documento descreve a camada OAuth compartilhada por
tokens, clients, validação e persistência.

## Limites do Protocolo

Esta versão ainda não é um provedor OIDC completo.

Não existem:

- `id_token`;
- `/connect/*`;
- OpenIddict;
- introspection endpoint;
- revocation endpoint público;
- consentimento interativo;
- dynamic client registration público.

Existem endpoints de discovery com formato OpenID para facilitar validação de
JWT tenant-scoped por APIs clientes. O discovery pública o issuer canônico do
tenant; tokens de clients com `iss` customizado continuam assinados pela chave
do tenant, mas exigem validação configurada para esse issuer.

## Componentes

### Controllers

- `AuthController`
  - `POST /api/auth/token`
  - `POST /api/auth/confirm-2fa`
  - endpoints hospedados `/auth/*` usados pelo authorization code flow
- `OAuthClientsController`
  - CRUD tenant-scoped de clients OAuth
- `TenantDiscoveryController`
  - discovery e JWKS público por tenant

### Application

- `AuthenticationAppService`
  - resolve grants de token;
  - aplica 2FA no password grant;
  - valida client credentials;
  - emite refresh tokens;
  - reconstrui perfil durante refresh.
- `HostedAuthenticationAppService`
  - valida authorization request;
  - cria authorization code;
  - consome code com PKCE;
  - integra login hospedado, 2FA e SSO.
- `OAuthClientAppService`
  - cria, lista, atualiza e desativa clients;
  - normaliza redirect URIs e scopes;
  - envia secret por email opcionalmente.

### Domain

- `OAuthClient`
- `OAuthClientTokenConfiguration`
- `OAuthClientTokenClaimValue`
- `RefreshToken`
- `HostedAuthTransaction`
- `HostedAuthSession`
- `AuthorizationCodeGrant`
- `SsoSession`
- `AuthAuditLog`

### Infrastructure

- `MongoOAuthClientRepository`
- `MongoRefreshTokenRepository`
- `MongoHostedAuthenticationRepositories`
- `JwtAccessTokenIssuer`
- `MongoTenantSigningKeyResolver`
- `TenantJwtBearerOptionsPostConfigure`
- `JwtTenantTokenValidator`

## Persistência

### Banco do Tenant

`OAuthClients`

- `ClientId`
- `ClientSecretHash`
- `DisplayName`
- `TokenConfig`
- `RedirectUris`
- `PostLogoutRedirectUris`
- `AllowedScopes`
- `RequireConsent`
- `Active`
- `CreatedAtUtc`
- `UpdatedAtUtc`

`RefreshTokens`

- `TokenHash`
- `SubjectId`
- `SubjectType`
- `Roles`
- `Scopes`
- `ExpiresAtUtc`
- `UseCount`
- `MaxUses`
- `CreatedAtUtc`
- `RevokedAtUtc`

### Banco Master

`Tenants`

- dados do tenant;
- database name;
- chave privada protegida;
- public JWK;
- key id;
- versão da chave.

`HostedAuthTransactions`, `HostedAuthSessions`,
`AuthorizationCodeGrants`, `SsoSessions` e `AuthAuditLogs` sustentam o fluxo
hosted authorization code e o SSO central.

## TokenConfig

`OAuthClient.TokenConfig` controla a emissão para `client_credentials`.

Campos obrigatórios:

- `accessTokenExpirationMinutes` maior que zero;
- `refreshTokenExpirationDays` maior que zero;
- `maxRefreshTokenUses` maior que zero;
- `values` com pelo menos uma entrada;
- uma entrada `type=iss`.

Tipos de claim suportados:

- `iss`
- `aud`
- `scope`
- `role`

Se `aud` não for informado, o valor de `iss` é usado como audience.

## Claims de Access Token

Todos os tokens tenant-scoped incluem:

- `sub`
- `tenantId`
- `token_use=tenant_access`

Tokens de cliente podem incluir:

- `client_id`
- `role`
- `scope`

Tokens de usuário podem incluir:

- `userId`
- `email`
- `username`
- `role`
- `amr`, `auth_time` e `mfa_method` quando 2FA foi confirmado.

## Grants

### client_credentials

Entrada:

- `grant_type=client_credentials`
- `client_id`
- `client_secret`
- header `X-Tenant-Id`

Regras:

- tenant deve existir e estar ativo;
- client deve existir, estar ativo e pertencer ao tenant;
- secret deve bater com `ClientSecretHash`;
- 2FA não é aplicado;
- issuer, audience, scopes, roles e expirações vêm do `TokenConfig`.

Saída HTTP:

- `ApiEnvelope<AuthTokenResponse>` com `requires_2fa=false` dentro de `Data`;
- `Data.token.access_token`: access token RS256;
- `Data.token.refresh_token`: refresh token opaco.

### password

Entrada:

- `grant_type=password`
- `username`
- `password`
- header `X-Tenant-Id`

Regras:

- tenant deve existir e estar ativo;
- credenciais são validadas no tenant;
- usuário precisa estar em estado aceito pelo validador;
- política 2FA vem do schema/configuração do usuário.

Saída HTTP:

- `ApiEnvelope<AuthTokenResponse>`;
- tokens diretos em `Data.token` quando 2FA não é exigido;
- ou `Data.requires_2fa=true` com `Data.challenge`.

### refresh_token

Entrada:

- `grant_type=refresh_token`
- `refresh_token`
- header `X-Tenant-Id`

Regras:

- refresh token é buscado por hash;
- precisa estar ativo, não expirado e abaixo de `MaxUses`;
- `UseCount` é incrementado atomicamente;
- subject `user` exige conta ainda ativa/incompleta;
- subject `client` exige que o `OAuthClient` ainda exista.

Saída HTTP:

- `ApiEnvelope<AuthTokenResponse>`;
- novo access token em `Data.token.access_token`;
- mesmo refresh token recebido em `Data.token.refresh_token`.

### authorization_code

Entrada:

- `grant_type=authorization_code`
- `client_id`
- `code`
- `redirect_uri`
- `code_verifier`
- header `X-Tenant-Id`

Regras:

- client deve existir e estar ativo;
- `redirect_uri` deve ser exatamente uma URI cadastrada no client;
- `code_verifier` deve gerar o mesmo challenge S256 do code grant;
- code é consumido uma única vez;
- code expira em 60 segundos;
- o subject é recarregado antes da emissão do token.

Saída HTTP:

- `ApiEnvelope<AuthTokenResponse>` com tokens de usuário tenant-scoped em
  `Data.token`.

## OAuth Client Registry

Endpoints:

- `GET /api/oauth-clients`
- `GET /api/oauth-clients/me`
- `POST /api/oauth-clients`
- `PUT /api/oauth-clients/me`
- `DELETE /api/oauth-clients/me`

Autorização:

- todos exigem bearer token com `tenantId`;
- leitura exige role `clients-read` ou scope `owner`;
- escrita exige role `clients-write` ou scope `owner`.
- endpoints `*/me` usam `client_id` do token atual e fazem fallback para `sub`.

Criação:

- gera `client_{guid}`;
- gera secret forte;
- grava hash PBKDF2;
- retorna `Data.clientSecret` uma única vez;
- pode enviar o secret por email via template `client-credentials-secret`.

Atualização:

- atualiza display name, token config, redirect URIs, logout URIs, scopes e
  `RequireConsent`;
- não troca o secret.

Desativação:

- marca `Active=false`.

## Redirect URIs e Scopes

Redirect URIs:

- máximo de 20 entradas;
- URI absoluta;
- sem fragment;
- HTTPS recomendado;
- HTTP permitido somente para loopback;
- esquemas `data`, `file` e `javascript` bloqueados;
- comparação exata no login e no exchange.

Scopes:

- entradas podem vir como lista ou strings separadas por espaco;
- caracteres inválidos e whitespace interno são rejeitados;
- `openid`, `profile` e `email` são básicos;
- `AllowedScopes` e claims `scope` do `TokenConfig` ampliam o conjunto
  permitido no hosted authorization request;
- o scope solicitado ainda não é propagado para claims do access token no
  authorization code flow.

## Discovery e Validação

Discovery:

- `GET /tenants/{tenantId}/.well-known/openid-configuration`
- `GET /tenants/{tenantId}/.well-known/jwks.json`

O `openid-configuration` representa o issuer canônico do tenant. Para usar
`Authority = /tenants/{tenantId}` em uma API cliente, configure o `OAuthClient`
para emitir `iss` compatível com esse issuer. Caso contrário, a API cliente
deve validar issuer/audience de forma explícita.

Validação local:

1. `TenantJwtBearerOptionsPostConfigure` lê o JWT e extrai `tenantId`.
2. Se houver `tenantId`, resolve a JWK pública do tenant.
3. O JwtBearer valida assinatura e lifetime.
4. `JwtTenantTokenValidator` valida tenant ativo.
5. Para token de cliente, recarrega o `OAuthClient`.
6. Issuer e audience são comparados com o perfil esperado.

## Auditoria e Dados Sensíveis

Eventos relacionados ficam em `AuthAuditLogs`, principalmente nos fluxos
hosted authorization code, 2FA, SSO e logout.

Não registrar:

- senha;
- `client_secret`;
- refresh token puro;
- access token;
- authorization code puro;
- `code_verifier`;
- código 2FA.

Permitido registrar:

- `tenantId`;
- `clientId`;
- `subjectId`;
- `transactionId`;
- `sessionId`;
- `correlationId`;
- hashes parciais para username, IP e user-agent quando necessário.

## Bruno

Colecoes relevantes:

- `Auth / Client Credentials`
- `Auth / Issue Token`
- `Auth / Confirm 2FA`
- `Auth / Refresh Token`
- `Auth / Open Hosted Login`
- `Auth / Authorization Code Exchange`
- `OAuth Clients / Create OAuth Client`
- `OAuth Clients / Update OAuth Client`
- `OAuth Clients / Get OAuth Client (Me)`
- `OAuth Clients / List OAuth Clients`
- `OAuth Clients / Deactivaté OAuth Client (Me)`
- `Discovery / OpenID Configuration`
- `Discovery / JWKS`

## Testes de Aceite

- `client_credentials` emite token sem challenge 2FA.
- `password` aplica política 2FA do schema.
- hosted start rejeita redirect URI não cadastrada.
- hosted start preserva `nonce` e valida scope antes do login.
- login sem 2FA cria code e exchange e single-use.
- exchange rejeita `code_verifier` errado sem consumir code.
- exchanges concorrentes consomem code apenas uma vez.
- confirmacoes 2FA concorrentes concluem apenas uma vez.
- JWT tenant-scoped pode ser emitido multiplas vezes com a mesma chave RSA.

## Riscos Conhecidos

- `RequireConsent` ainda é apenas campo de contrato.
- O flow hospedado valida scope, mas ainda não injeta o scope solicitado no
  access token.
- Não existe endpoint público de revogação de refresh token.
- Não existe fluxo self-service de rotação de secret.
- Discovery e compatibilidade OIDC parcial; não há `id_token`.

## Próxima Evolução

`v1.1.0`: hardening operacional de clients, rotação de secrets, testes
integrados de discovery/JwtBearer e preparação para consentimento.
