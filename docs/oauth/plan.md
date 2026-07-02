# Plano Vivo do Módulo OAuth

## Estado Atual

Módulo OAuth implementado como registry de clientes por tenant, endpoint
unificado de tokens e infraestrutura de validação de JWT tenant-scoped.

Snapshot completo:
[versions/plan.v1.0.1.md](versions/plan.v1.0.1.md)

Snapshot anterior:
[versions/plan.v1.0.0.md](versions/plan.v1.0.0.md)

Baseline alinhado aos commits recentes:

- `4797481`: adicionou OAuth2/OIDC ao login hospedado e autenticação SSO.
- `801691b`: adicionou sessão SSO, `prompt`, `max_age` e logout central.
- `39f7d78`: ajustou Bruno para os fluxos de integração.
- `033427c`: consolidou documentação de SSO hospedado e diagramas.

## Decisões Ativas

- `OAuthClient` é o client registry atual. Não existe entidade `SsoClient`
  separada.
- Clientes OAuth ficam no banco do tenant, em `tenant_{id}.OAuthClients`.
- O tenant provisionado recebe um cliente inicial `client_{tenantId}` com
  scope `owner`.
- Secrets de client são armazenados como hash PBKDF2 e o valor puro aparece
  somente na criação/onboarding.
- `POST /api/auth/token` é anônimo, mas exige `X-Tenant-Id`.
- Grants suportados: `password`, `refresh_token`, `client_credentials` e
  `authorization_code`.
- `client_credentials` não aplica 2FA.
- `password` aplica 2FA conforme schema/configuração do usuário final.
- `authorization_code` só é gerado pelo login hospedado em `/auth/login`.
- PKCE é obrigatório no authorization code flow e aceita somente `S256`.
- Authorization code é opaco, armazenado por hash, expira em 60 segundos e é
  consumido uma única vez.
- Refresh token é opaco, armazenado por hash SHA-256 em `RefreshTokens` do
  tenant, e é limitado por expiração e `MaxRefreshTokenUses`.
- Access tokens tenant-scoped usam RS256, chave privada por tenant e claim
  `token_use=tenant_access`.
- Tokens de cliente incluem `client_id`; tokens de usuário incluem `userId`,
  `email`, `username` e roles normalizadas quando disponíveis.
- O middleware JWT resolve a JWK pública pelo `tenantId` presente no token e
  valida issuer/audience depois da assinatura.
- Discovery público fica em `/tenants/{tenantId}/.well-known/*`.
- APIs externas que usam discovery padrão devem receber tokens cujo `iss`
  esteja alinhado ao issuer do tenant. Clients com `iss` customizado exigem
  validação customizada compatível.
- CRUD de `/api/oauth-clients` exige JWT tenant-scoped e policy por role
  `clients-read`/`clients-write` ou scope `owner`.
- A gestão administrativa por detalhe, edição e desativação usa o `id`
  interno do documento `OAuthClient`.
- `clientId` continua sendo o identificador público usado em grants OAuth e
  nos endpoints `/api/oauth-clients/me`.
- `notifyEmail` e persistido no `OAuthClient` e exposto em listagem, detalhe,
  criação e edição.
- `redirect_uris` e `post_logout_redirect_uris` exigem correspondência exata.
- HTTP em redirect URI só é aceito para loopback; `data`, `file`,
  `javascript` e fragments são rejeitados.
- `allowed_scopes` complementa os scopes básicos `openid`, `profile` e
  `email`.
- `RequireConsent` existe no contrato, mas ainda não há tela de consentimento.
- Endpoints JSON da API retornam `ApiEnvelope<T>`; o payload específico do
  contrato fica dentro de `Data`.

## Contratos Ativos

### Token

`POST /api/auth/token`

Header obrigatório:

- `X-Tenant-Id`: tenant onde o grant será validado.

Grants aceitos:

- `grant_type=password`: usa `username` e `password`; pode retornar
  `requires_2fa=true` com `challenge`.
- `grant_type=refresh_token`: usa `refresh_token` e retorna novo access token
  com o mesmo refresh token.
- `grant_type=client_credentials`: usa `client_id` e `client_secret`; emite
  token técnico do cliente.
- `grant_type=authorization_code`: usa `client_id`, `code`, `redirect_uri` e
  `code_verifier`; delega o exchange para o serviço de login hospedado.

Resposta HTTP de sucesso usa `ApiEnvelope<AuthTokenResponse>`. O
`AuthTokenResponse` fica dentro de `Data`:

```json
{
  "Error": false,
  "Errors": [],
  "StatusCode": 200,
  "Data": {
    "requires_2fa": false,
    "token": {
      "access_token": "<jwt>",
      "token_type": "Bearer",
      "expires_in": 3600,
      "refresh_token": "<opaque-refresh-token>"
    },
    "challenge": null
  }
}
```

### Confirmação 2FA

`POST /api/auth/confirm-2fa`

Header obrigatório:

- `X-Tenant-Id`

Corpo:

```json
{
  "challenge_id": "<challenge-id>",
  "code": "123456"
}
```

### OAuth Clients

Todos exigem `Authorization: Bearer <jwt>` com `tenantId`.

- `GET /api/oauth-clients`: requer policy `OAuthClientsRead`.
- `GET /api/oauth-clients/me`: requer policy `OAuthClientsRead`.
- `GET /api/oauth-clients/{id}`: requer policy `OAuthClientsRead`.
- `POST /api/oauth-clients`: requer policy `OAuthClientsWrite`.
- `PUT /api/oauth-clients/me`: requer policy `OAuthClientsWrite`.
- `PUT /api/oauth-clients/{id}`: requer policy `OAuthClientsWrite`.
- `DELETE /api/oauth-clients/me`: requer policy `OAuthClientsWrite`.
- `DELETE /api/oauth-clients/{id}`: requer policy `OAuthClientsWrite`.

Os endpoints `*/me` usam o `client_id` do token atual; se não houver
`client_id`, o controller usa `sub` como fallback.

Os endpoints `/{id}` usam o `Id` interno do documento `OAuthClient` no Mongo.
Eles não aceitam `clientId` para evitar ambiguidade entre identificador
público do client e identificador administrativo do registro.

Campos principais do contrato de OAuth Client:

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

Na criação, a resposta inclui também `clientSecret`, retornado uma única vez.

Permissões:

- Scope `owner` concede leitura e escrita.
- Role `clients-read` concede leitura.
- Role `clients-write` concede escrita.

### Discovery

- `GET /tenants/{tenantId}/.well-known/openid-configuration`
- `GET /tenants/{tenantId}/.well-known/jwks.json`

Os endpoints são públicos e retornam apenas metadados/JWK pública. O
`openid-configuration` informa `issuer`, `jwks_uri` e suporte a `RS256`.
O issuer publicado é o issuer canônico do tenant; clients OAuth com `iss`
customizado continuam assinados pela chave do tenant, mas precisam de
validação configurada para esse issuer.

## Modelo de Dados

No banco do tenant:

- `OAuthClients`: id interno, client id público, hash do secret, display name,
  notify email, token config, redirect URIs, post logout URIs, scopes
  permitidos, flag `RequireConsent`, status ativo e timestamps.
- `RefreshTokens`: hash do refresh token, subject, subject type, roles,
  scopes, expiração, contador de uso, max uses e revogação.
- `Users`: identidade recarregada antes de emitir token de usuário ou renovar
  refresh token de usuário.

No banco master:

- `Tenants`: dados do tenant e chave pública/privada protegida para RS256.
- `HostedAuthTransactions`: authorization request validada, TTL de 15 minutos.
- `HostedAuthSessions`: estado curto das views hospedadas, TTL de 15 minutos.
- `AuthorizationCodeGrants`: code hash, PKCE, tenant, client, callback,
  subject e TTL de 60 segundos.
- `SsoSessions`: sessão central por tenant usada por `/auth/login`.
- `AuthAuditLogs`: eventos de login, 2FA, code, SSO e logout.

## Fluxos Atuais

### Provisionamento e Registry

1. `TenantOnboardingAppService` chama `ITenantProvisioner`.
2. `MongoTenantProvisioner` cria tenant, schema default, templates, SMTP e
   chave RSA do tenant.
3. Um `OAuthClient` inicial é criado no tenant com issuer/audience por tenant
   e scope `owner`.
4. O `clientSecret` puro é enviado no retorno/onboarding e não é recuperável
   depois.
5. Novos clients são criados por `/api/oauth-clients` com token owner ou role
   `clients-write`.

### Client Credentials

1. Cliente chama `/api/auth/token` com `grant_type=client_credentials`.
2. API valida `X-Tenant-Id` e tenant ativo.
3. `AuthenticationAppService` busca `OAuthClient` ativo no tenant.
4. Secret recebido e comparado com o hash PBKDF2.
5. `TokenIssuanceProfile.FromOAuthClient` resolve issuer, audience, roles,
   scopes e expirações.
6. `JwtAccessTokenIssuer` assina access token RS256 com a chave do tenant.
7. `RefreshTokenRepository` grava refresh token opaco e retorna o valor puro.

### Password e 2FA

1. Usuário chama `/api/auth/token` com `grant_type=password`.
2. Credenciais são validadas no tenant.
3. Política 2FA é resolvida pelo schema/configuração do usuário.
4. Se 2FA for exigido, a resposta retorna `requires_2fa=true`.
5. `POST /api/auth/confirm-2fa` valida o challenge e emite tokens.

### Authorization Code com PKCE

1. App abre `/auth/login` com `response_type=code`, `tenant_id`,
   `client_id`, `redirect_uri`, `state`, `nonce`, `scope`,
   `code_challenge` e `code_challenge_method=S256`.
2. `HostedAuthenticationAppService` valida client, redirect URI, scope e
   PKCE.
3. Login hospedado autentica o usuário e aplica 2FA quando necessário.
4. Backend cria `AuthorizationCodeGrant` com hash do code e redireciona para
   `redirect_uri?code&state`.
5. App troca o code em `/api/auth/token` com `grant_type=authorization_code`,
   `X-Tenant-Id` e `code_verifier`.
6. O code é consumido de forma atômica e tokens de usuário são emitidos.

### Refresh

1. Cliente chama `/api/auth/token` com `grant_type=refresh_token`.
2. Refresh token é localizado por hash no tenant.
3. API valida expiração, revogação e limite de uso.
4. Uso é incrementado de forma atômica.
5. Para subject `user`, status da conta é recarregado.
6. Perfil de emissão é reconstruído a partir do `OAuthClient` ou do usuário.
7. Novo access token é emitido e o mesmo refresh token é retornado.

## Arquivos Fonte Principais

- `Lightcode.Registration/Controllers/AuthController.cs`
- `Lightcode.Registration/Controllers/OAuthClientsController.cs`
- `Lightcode.Registration/Controllers/TenantDiscoveryController.cs`
- `Lightcode.Registration.Application/Services/AuthenticationAppService.cs`
- `Lightcode.Registration.Application/Services/HostedAuthenticationAppService.cs`
- `Lightcode.Registration.Application/Services/OAuthClientAppService.cs`
- `Lightcode.Registration.Domain/Entities/OAuthClient.cs`
- `Lightcode.Registration.Domain/Entities/RefreshToken.cs`
- `Lightcode.Registration.Domain/Entities/HostedAuthentication.cs`
- `Lightcode.Registration.Infrastructure/Persistence/Mongo/MongoOAuthClientRepository.cs`
- `Lightcode.Registration.Infrastructure/Persistence/Mongo/MongoRefreshTokenRepository.cs`
- `Lightcode.Registration.Infrastructure/Security/JwtAccessTokenIssuer.cs`
- `Lightcode.Registration.AspNetCore/Security/JwtTenantTokenValidator.cs`
- `Lightcode.Registration.AspNetCore/Security/TenantJwtBearerOptionsPostConfigure.cs`

## Bruno

Requests relevantes:

- `Auth / Client Credentials`
- `Auth / Issue Token (password)`
- `Auth / Confirm 2FA`
- `Auth / Refresh Token`
- `Auth / Open Hosted Login`
- `Auth / Authorization Code Exchange`
- `OAuth Clients / Create OAuth Client`
- `OAuth Clients / Update OAuth Client`
- `OAuth Clients / Update OAuth Client By Id`
- `OAuth Clients / Get OAuth Client (Me)`
- `OAuth Clients / Get OAuth Client`
- `OAuth Clients / List OAuth Clients`
- `OAuth Clients / Deactivate OAuth Client (Me)`
- `OAuth Clients / Deactivate OAuth Client`
- `Discovery / OpenID Configuration`
- `Discovery / JWKS`

Variáveis relevantes:

- `tenantId`
- `oauthClientRecordId`
- `oauthClientId`
- `oauthClientSecret`
- `jwt`
- `refreshToken`
- `hostedRedirectUri`
- `authorizationCode`
- `pkceVerifier`
- `pkceChallenge`

## Testes

Cobertura existente relacionada:

- `AuthenticationAppServiceTests.Client_credentials_emits_token_without_two_factor_challenge`
- `AuthenticationAppServiceTests.Password_grant_applies_schema_policy`
- `HostedAuthenticationAppServiceTests.Start_rejects_redirect_uri_not_registered_for_client`
- `HostedAuthenticationAppServiceTests.Start_preserves_nonce_and_validates_scope_before_login`
- `HostedAuthenticationAppServiceTests.Login_without_two_factor_creates_code_and_exchange_is_single_use`
- `HostedAuthenticationAppServiceTests.Exchange_rejects_wrong_pkce_verifier_without_consuming_code`
- `HostedAuthenticationAppServiceTests.Concurrent_exchange_consumes_authorization_code_once`
- `JwtAccessTokenIssuerTests.CreateAccessToken_can_issue_multiple_tokens_with_the_same_tenant_signing_key`
- `OAuthClientAppServiceTests.Create_persists_notify_email_and_returns_secret_status_and_dates`
- `OAuthClientAppServiceTests.Get_by_id_returns_active_client_from_tenant`
- `OAuthClientAppServiceTests.Update_by_id_updates_client_fields`
- `OAuthClientAppServiceTests.Deactivate_by_id_marks_client_inactive`
- `OAuthClientAppServiceTests.Missing_id_returns_not_found`
- `OAuthClientAppServiceTests.Invalid_notify_email_returns_bad_request`

## Segurança

- Nunca registrar secrets, senhas, refresh tokens, access tokens,
  authorization codes puros, `code_verifier` ou códigos 2FA.
- Auditar apenas ids, hashes parciais, tenant/client/subject e correlation id.
- Redirect URI deve ser validado por correspondência exata.
- Authorization code e refresh token devem continuar opacos.
- Access token deve continuar assinado por chave do tenant.
- Token tenant-scoped deve carregar `tenantId` e `token_use=tenant_access`.
- Tokens de cliente devem ser rejeitados se o `OAuthClient` não existir mais.
- Issuer e audience devem ser validados contra a configuração ativa do tenant
  ou do client.

## Fora de Escopo Atual

- `id_token` e validação de `nonce` em token OIDC.
- `/connect/authorize`, `/connect/token` e `/connect/logout`.
- OpenIddict.
- Introspection endpoint.
- Revocation endpoint público para refresh/access token.
- Consentimento interativo quando `RequireConsent=true`.
- Registro dinâmico público de clientes.
- Rotação self-service de `client_secret`.
- Propagação do `scope` solicitado no hosted flow para claims do access token.

## Pendências

- Formalizar propagação de scopes solicitados no authorization code flow.
- Emitir `id_token` quando o produto evoluir para OIDC completo.
- Implementar consentimento para `RequireConsent=true`.
- Definir rotação/reemissão de client secrets sem recriar client.
- Expandir testes de controller/policy para `/api/oauth-clients`, incluindo
  as rotas administrativas por `id`.
- Expandir testes integrados de discovery/JWKS e validação JwtBearer.
- Definir runbook operacional de rotação de signing keys por tenant.

## Próxima Versão Prevista

`v1.1.0`: hardening operacional de clients, rotação de secrets e cobertura
integrada de discovery/JWT.
