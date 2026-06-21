# Plano de Refatoração do Core de 2FA

## Objetivo

Criar um core de 2FA reutilizável para diferentes superfícies da API:

- usuário final do tenant, usado principalmente por `AccountsController` e `/api/auth/token`;
- admin da plataforma, usado por `/api/platform-auth/token`;
- painel de tenant, consumindo o mesmo fluxo de platform admin via BFF/front;
- futuro suporte a TOTP sem reescrever o fluxo de autenticação.

O foco inicial deve ser o usuário final. Depois, o mesmo core deve ser adaptado para o painel admin.

## Diagramas dos Fluxos

Os fluxos propostos estão documentados em Mermaid em `docs/two-factor-auth/diagrams`:

- [Preparação do cliente/aplicação](../diagrams/01-preparacao-cliente-aplicacao.mmd)
- [Client credentials sem 2FA](../diagrams/02-client-credentials-sem-2fa.mmd)
- [Política de 2FA no schema do usuário final](../diagrams/03-politica-2fa-schema-usuario-final.mmd)
- [Login de usuário sem 2FA](../diagrams/04-login-usuario-sem-2fa.mmd)
- [Login de usuário com 2FA por e-mail](../diagrams/05-login-usuario-com-2fa-email.mmd)
- [Ativação de 2FA por e-mail](../diagrams/06-ativar-2fa-email-usuario.mmd)
- [Desativação de 2FA](../diagrams/07-desativar-2fa-usuario.mmd)
- [Login do platform admin com 2FA](../diagrams/08-login-platform-admin-com-2fa.mmd)
- [Emissão do tenant token após 2FA do platform admin](../diagrams/09-emitir-tenant-token-apos-platform-2fa.mmd)
- [Rate limiting dos fluxos de autenticação e 2FA](../diagrams/10-rate-limiting-2fa-auth.mmd)
- [Caminho futuro para TOTP](../diagrams/11-caminho-futuro-totp.mmd)

## Contexto de Autenticação de Ponta a Ponta

O produto possui dois tipos de autenticação que não devem ser confundidos:

1. Autenticação técnica do cliente/aplicação.
   - O tenant é criado e recebe `Tenant ID`, `Client ID` e `Client Secret`.
   - A aplicação cliente chama `/api/auth/token` com `grant_type=client_credentials`.
   - Esse token permite criar schema, templates, contas administradas etc.
   - Este fluxo não deve exigir 2FA, pois representa uma credencial de aplicação, não um usuário humano.

2. Autenticação humana do usuário final.
   - A aplicação cliente cria contas via `/api/accounts/admin` ou fluxo público de conta.
   - O usuário confirma e-mail.
   - Depois, a aplicação terceira chama `/api/auth/token` com `grant_type=password`, `username` e `password`.
   - Este é o primeiro alvo do 2FA.

Para o painel de tenant, o admin da plataforma usa `/api/platform-auth/token`. Esse fluxo também é humano, mas deve ser a segunda etapa de implementação, reutilizando o core criado para o usuário final.

## Diagnóstico Atual

Hoje existe uma noção de `2FA` em `AccountJsonSchemaConfig`, mas ela está ligada ao fluxo de confirmação de e-mail no cadastro.

Pontos relevantes:

- `AccountJsonSchemaConfig.TwoFactor` define `Active` e `Type` com `Code` ou `Link`.
- `AccountRegistrationTwoFactorSupport` usa essa configuração para confirmar e-mail após registro ou criação por admin.
- `AuthenticationAppService.IssuePasswordGrantAsync` emite token direto se usuário e senha forem válidos.
- `UserCredentialValidator` válida usuário, senha, status e e-mail confirmado, mas não avalia 2FA de login.
- `PlatformAdminAppService.IssueTokenAsync` também emite token direto se e-mail/senha forem válidos.

Conclusão: o nome atual `TwoFactor` no schema está semanticamente misturado com confirmação de e-mail. Para 2FA de login, precisamos de um core separado.

## Conceito Proposto

Separar claramente:

- confirmação de e-mail: prova que o e-mail pertence ao usuário durante cadastro;
- 2FA de login: segunda etapa depois de senha válida;
- método de 2FA: `email_code` agora, `totp` no futuro.

O endpoint de token não deve emitir JWT quando 2FA estiver ativo. Ele deve retornar um resultado intermediário informando que a verificação 2FA é obrigatória.

2FA deve ser aplicado apenas a sujeitos humanos:

- `grant_type=password` para usuários finais;
- `/api/platform-auth/token` para admins da plataforma.

2FA não deve ser aplicado a:

- `grant_type=client_credentials`;
- `grant_type=refresh_token`, salvo se futuramente houver regra de reautenticação por risco.

## Contratos Sugeridos

Importante: a API hoje usa envelope padronizado via `ServiceResult`/`ApiResponse`. Logo, os exemplos abaixo representam o conteúdo de `Data`, não necessariamente o corpo HTTP bruto.

### Resposta de login com 2FA pendente

```json
{
  "requires_2fa": true,
  "challenge_id": "uuid-ou-token-opaco",
  "verification_type": "email_code",
  "expires_in": 300
}
```

Status HTTP recomendado: `200` para manter compatibilidade com clientes que tratam autenticação como resposta de negócio, ou `202 Accepted` se decidirmos formalizar que o login ficou pendente. A decisão deve ser consistente entre `/api/auth/token` e `/api/platform-auth/token`.

### Confirmação de 2FA

`POST /api/auth/confirm-2fa`

```json
{
  "challenge_id": "uuid-ou-token-opaco",
  "code": "123456"
}
```

Resposta de sucesso deve ser o mesmo `IssueTokenResponse` já usado hoje:

```json
{
  "access_token": "...",
  "token_type": "Bearer",
  "expires_in": 7200,
  "refresh_token": "..."
}
```

Para admin da plataforma:

`POST /api/platform-auth/confirm-2fa`

Mesmo conceito, mas emitindo token de platform admin.

## Modelo de Domínio

Criar entidades/agregados independentes do tipo de usuário:

### TwoFactorSettings

Representa configuração permanente do sujeito autenticável.

Campos sugeridos:

- `Enabled`
- `PreferredMethod`: `email_code` ou `totp`
- `EmailEnabled`
- `TotpEnabled`
- `TotpSecretEncrypted`
- `RecoveryCodesHash`
- `UpdatedAtUtc`

Para usuário final, pode viver dentro do documento `Users` do tenant.

Para Platform Admin, pode viver em `PlatformAdmin`, no master database.

### Política de 2FA no Schema de Auth do Usuário Final

A configuração atual `AccountJsonSchemaConfig.TwoFactor` deve continuar sendo tratada como confirmação de e-mail de cadastro até ser renomeada. Para 2FA de login, criar uma configuração separada no config do schema, por exemplo:

```json
{
  "auth": {
    "twoFactor": {
      "mode": "optional",
      "allowedMethods": ["email_code", "totp"],
      "defaultMethod": "email_code"
    }
  }
}
```

Modos propostos:

- `disabled`: o schema não aplica 2FA de login e não permite ativação pelo usuário final.
- `optional`: o schema permite 2FA, mas cada usuário ativa/desativa no próprio perfil.
- `required`: todo login humano de usuário final vinculado ao schema deve passar por 2FA.

Regras:

- Essa política vale apenas para `grant_type=password`.
- `grant_type=client_credentials` continua emitindo token técnico sem 2FA.
- `grant_type=refresh_token` continua sem novo desafio de 2FA, salvo regra futura de risco.
- Em `required` com `email_code`, o usuário pode passar por 2FA sem enrollment prévio, desde que tenha e-mail confirmado.
- Em `required` com `totp`, será necessário fluxo de enrollment antes de tornar TOTP obrigatório.
- Em `optional`, o login só exige 2FA quando o próprio usuário tiver `TwoFactorSettings.Enabled=true`.
- A resolução da política deve usar o schema vinculado ao usuário, não um parâmetro enviado no login.

### TwoFactorChallenge

Representa uma tentativa de autenticação pendente.

Campos sugeridos:

- `Id`
- `SubjectType`: `tenant_user` ou `platform_admin`
- `SubjectId`
- `TenantId`, opcional para platform admin puro
- `Method`: `email_code` inicialmente
- `DestinationHint`: e-mail mascarado
- `CodeHash`
- `AuthContext`
- `Status`: `pending`, `used`, `expired`, `failed`
- `Attempts`
- `MaxAttempts`
- `ExpiresAtUtc`
- `CreatedAtUtc`
- `ConsumedAtUtc`

Recomendação: armazenar em coleção própria, com TTL index por `ExpiresAtUtc`.

### AuthContext do Challenge

O challenge precisa conter contexto suficiente para emitir o token depois da confirmação, sem armazenar senha em claro e sem pedir senha novamente.

Para usuário final:

- `TenantId`
- `UserId`
- `SchemaId`
- `GrantType=password`
- `Email`
- `Username`
- `SubjectType=user`

Para admin da plataforma:

- `AdminId`
- `Email`
- `SubjectType=platform_admin`

Para cliente OAuth com `client_credentials`, não aplicar 2FA.

Isso é essencial para não quebrar o fluxo inicial do tenant:

```text
client_credentials -> cria json schema -> cria conta -> usuário confirma e-mail -> password grant -> 2FA se ativo
```

## Serviços de Aplicação

Criar um core em Application, sem depender diretamente de controller:

- `ITwoFactorSettingsService`
- `ITwoFactorChallengeService`
- `ITwoFactorMethod`
- `ITwoFactorMethodProvider`

Implementação inicial:

- `EmailCodeTwoFactorMethod`

Futura:

- `TotpTwoFactorMethod`

### Responsabilidades do Challenge Service

- criar desafio após senha válida;
- gerar código de 6 dígitos;
- armazenar apenas hash do código;
- enviar e-mail via `IEmailEnqueuePublisher`;
- validar código;
- controlar expiração e tentativas;
- impedir reuso;
- devolver contexto necessário para emissão do token final;
- nunca armazenar senha, client secret, access token ou refresh token no challenge.

## Fluxo para Usuário Final

### Preparação pelo cliente/aplicação

1. Tenant é criado via `POST /api/tenants` e recebe `Tenant ID`, `Client ID` e `Client Secret` via e-mail.
2. Deve ser realizado uma chamada no `POST /api/oauth-clients/me` para definir role 'admin'.
3. Cliente chama `POST /api/auth/token` com `grant_type=client_credentials`.
4. API emite access token técnico do cliente.
5. Cliente cria `json_schema` via `POST /api/account-json-schemas`.
6. Cliente cria conta do usuário final via `POST /api/accounts/admin` ou fluxo público equivalente.
7. Usuário confirma e-mail por código ou link
   1. Caso código: `POST /api/accounts/confirm-email-code/`.
   2. Caso link: Clicar no link do e-mail.

### Login humano do usuário final

1. Aplicação terceira chama `POST /api/auth/token` com `grant_type=password`.
2. `AuthenticationAppService` válida tenant, username e password.
3. Se credenciais forem inválidas, retorna erro genérico como hoje.
4. Se e-mail não confirmado, retorna 403 como hoje.
5. API resolve o schema do usuário e a política `auth.twoFactor`.
6. Se a política for `required` ou se for `optional` com 2FA ativo no usuário:
   - cria `TwoFactorChallenge`;
   - envia e-mail com código de 6 dígitos;
   - retorna `requires_2fa=true`;
   - não emite JWT.
7. Aplicação terceira redireciona o usuário para tela de código.
8. Aplicação terceira chama `POST /api/auth/confirm-2fa`.
9. API válida challenge e código.
10. Se válido, emite `IssueTokenResponse`.

## Fluxo para Platform Admin

1. Admin ativa 2FA no perfil do painel de tenant.
2. Em login posterior, admin chama `POST /api/platform-auth/token`.
3. `PlatformAdminAppService` válida e-mail e senha.
4. Se admin tiver 2FA ativo:
   - cria challenge com `SubjectType=platform_admin`;
   - envia e-mail;
   - retorna `requires_2fa=true`;
   - não emite platform token.
5. Front do painel redireciona para tela de código.
6. `POST /api/platform-auth/confirm-2fa` válida código.
7. API emite platform token.
8. Fluxo atual continua: listar tenants e emitir tenant token principal.

## Endpoints Novos

Usuário final:

- `POST /api/auth/confirm-2fa`
- `POST /api/accounts/me/2fa/email/enable`
- `POST /api/accounts/me/2fa/disable`
- opcional para admin: `POST /api/accounts/{userId}/2fa/disable`
- futuramente `POST /api/accounts/{userId}/2fa/totp/setup`
- futuramente `POST /api/accounts/{userId}/2fa/totp/confirm`

Platform admin:

- `POST /api/platform-auth/confirm-2fa`
- `POST /api/platform-admins/me/2fa/email/enable`
- `POST /api/platform-admins/me/2fa/disable`
- `GET /api/platform-admins/me/2fa/status`
- futuramente endpoints TOTP equivalentes.

Preferir endpoints `me` para operações do próprio usuário. Quando houver operação administrativa sobre outro usuário, exigir policy admin e registrar auditoria.

## Alterações em Contratos

Hoje `IssueTokenAsync` retorna apenas `ServiceResult<IssueTokenResponse>`.

Para suportar 2FA sem gambiarra, criar um resultado discriminado:

- `TokenIssued`
- `TwoFactorRequired`

Exemplo:

```csharp
public sealed record AuthTokenResult(
    bool RequiresTwoFactor,
    IssueTokenResponse? Token,
    TwoFactorChallengeDto? Challenge);
```

Ou criar `IssueTokenOrChallengeResponse` com JSON compatível.

O mesmo ajuste deve existir no fluxo de Platform Admin, porque `PlatformAdminAppService.IssueTokenAsync` também emite token direto hoje.

Evitar overloads que retornem tipos diferentes no mesmo endpoint sem discriminador. Sempre incluir `requires_2fa` ou `kind`.

Exemplo de `Data` quando token foi emitido:

```json
{
  "requires_2fa": false,
  "token": {
    "access_token": "...",
    "token_type": "Bearer",
    "expires_in": 7200,
    "refresh_token": "..."
  }
}
```

Exemplo de `Data` quando precisa de 2FA:

```json
{
  "requires_2fa": true,
  "challenge": {
    "challenge_id": "...",
    "verification_type": "email_code",
    "expires_in": 300,
    "destination_hint": "c***@empresa.com"
  }
}
```

## Pontos de Segurança

- Código de e-mail deve ter 6 dígitos e expirar rápido, por exemplo 5 minutos.
- Armazenar apenas hash do código.
- Limitar tentativas, por exemplo 5.
- Invalidar challenge após sucesso.
- Invalidar challenges anteriores pendentes para o mesmo sujeito/método ao criar um novo.
- Não informar se e-mail existe além do que o login já informa.
- Não emitir refresh token antes de concluir 2FA.
- Adicionar rate limiting por IP, tenant, username e challenge.
- Para ativar/desativar 2FA, exigir autenticação recente ou confirmação de senha/código.
- Para desativar 2FA por admin, registrar auditoria explícita.
- Logar eventos sem código ou segredo:
  - `2fa_challenge_created`
  - `2fa_challenge_sent`
  - `2fa_challenge_failed`
  - `2fa_challenge_verified`
  - `2fa_enabled`
  - `2fa_disabled`

## Refatorações Recomendadas

1. Renomear mentalmente o `AccountRegistrationTwoFactorSupport`.
   - Ele deve virar algo como `AccountEmailConfirmationSupport`.
   - Isso evita confundir confirmação de e-mail com 2FA de login.

2. Criar core novo para 2FA de login.
   - Não reutilizar campos `emailConfirmationSecretHash`.
   - Não reutilizar status `PendingConfirmation`.

3. Expandir `UserCredentialValidator`.
   - Hoje retorna apenas credenciais validadas.
   - Pode passar a retornar também `TwoFactorSettings` ou um identificador suficiente para consultar settings.

4. Criar repositórios dedicados.
   - `ITwoFactorChallengeRepository`
   - `IUserTwoFactorRepository`
   - `IPlatformAdminTwoFactorRepository`

   O repositório de challenge pode ser único, mas os repositórios de settings devem respeitar a persistência de cada sujeito:
   - usuário final: coleção `Users` do database do tenant;
   - platform admin: coleção `PlatformAdmins` no master database.

5. Adaptar `AuthenticationAppService` primeiro.
   - Prioridade no usuário final.
   - Interceptar apenas `IssuePasswordGrantAsync`.
   - Não alterar comportamento de `IssueClientCredentialsGrantAsync`.
   - Não alterar comportamento inicial de `IssueRefreshGrantAsync`.

6. Adaptar `PlatformAdminAppService` depois.
   - Reutilizar o mesmo `TwoFactorChallengeService`.

## Lacunas Obrigatórias Após Revisão da API

Esta revisão comparou o plano com os fluxos atuais de `AuthenticationAppService`, `PlatformAdminAppService`, `AccountsController`, `AccountUpdateAppService`, `UserAccountMongoWriter` e refresh tokens. Os pontos abaixo devem entrar na implementação para evitar bypass ou adulteração de 2FA.

### Proteger campos internos de 2FA no documento `Users`

O schema default permite `additionalProperties=true` e o endpoint de atualização de conta faz merge de campos arbitrários, exceto uma lista pequena de chaves bloqueadas. Se `TwoFactorSettings` ficar dentro do documento `Users`, esses campos não podem ser aceitos em payloads genéricos.

Obrigatório:

- bloquear/remover campos reservados em cadastro público, cadastro admin, complete-register e update genérico;
- impedir patch direto de campos como `twoFactor`, `twoFactorSettings`, `mfa`, `totpSecret`, `recoveryCodes`, `trustedDevices`, `twoFactorEnabled`;
- permitir alteração desses campos apenas por serviços/endpoints próprios de 2FA;
- garantir que `UserAccountApiSanitizer` nunca exponha segredos de TOTP, recovery codes, códigos, hashes ou estado interno de challenge.

### Ativação de 2FA deve ser em duas etapas

`POST /2fa/email/enable` não deve simplesmente ligar o 2FA. O fluxo deve ser:

1. usuário autenticado solicita ativação;
2. API exige autenticação recente, senha atual ou sessão já verificada;
3. API envia código por e-mail e cria challenge de ativação;
4. usuário confirma o código;
5. somente após confirmação a configuração permanente passa para `Enabled=true`.

O mesmo princípio vale para TOTP: gerar segredo pendente, confirmar código TOTP e só então ativar.

### Endpoints de gerenciamento devem respeitar o estado atual

Os endpoints de `begin` para ativação e desativação não devem disparar e-mail quando a operação já estiver incompatível com o estado salvo.

Obrigatório:

- `begin enable` deve retornar `409` quando o 2FA já estiver ativo para o sujeito;
- `begin disable` deve retornar `409` quando o 2FA já estiver desativado para o sujeito;
- essa validação deve existir para usuário final e para platform admin;
- o front/BFF pode consultar `GET /api/platform-admins/me/2fa/status` para UX, mas a proteção principal deve estar no backend;
- a UI deve desabilitar ações redundantes e tratar `409` de forma amigável caso o estado tenha mudado fora da aba.

### Challenge deve ser validado e consumido de forma atômica

A confirmação de 2FA precisa impedir reuso e corrida entre requisições concorrentes.

Obrigatório:

- confirmar usando filtro atômico por `challengeId`, `SubjectType`, `TenantId` quando existir, `Status=pending`, `ExpiresAtUtc > now` e `Attempts < MaxAttempts`;
- ao sucesso, marcar como `used` na mesma operação lógica antes de emitir token;
- ao erro, incrementar tentativas de forma atômica;
- bloquear challenge após exceder tentativas;
- invalidar challenges pendentes anteriores do mesmo sujeito e método quando criar um novo.

### Challenge deve ser vinculado ao contexto correto

`/api/auth/confirm-2fa` deve exigir o `X-Tenant-Id` e validar o challenge dentro do tenant informado. `/api/platform-auth/confirm-2fa` não usa tenant, mas deve validar `SubjectType=platform_admin`.

Não aceitar:

- challenge de outro tenant;
- challenge de `platform_admin` em endpoint de usuário final;
- challenge de usuário final em endpoint de platform admin;
- challenge criado para ativação de 2FA sendo usado para login, ou o contrário.

Adicionar campo `Purpose`: `login`, `enable_2fa`, `disable_2fa`, `totp_setup`, etc.

### Não confiar em roles/scope salvos no challenge

O challenge não deve congelar permissões por vários minutos e emitir token com dados possivelmente antigos.

Recomendação:

- armazenar no `AuthContext` apenas o mínimo para retomar a operação: sujeito, tenant, tipo de grant e método;
- no `confirm-2fa`, recarregar usuário/admin atual, status, roles/scopes e configuração 2FA antes de emitir token;
- se usuário/admin estiver inativo, expirado, sem e-mail confirmado ou sem vínculo necessário, negar emissão.

### Hash de código de 6 dígitos precisa de proteção extra

Código de 6 dígitos tem baixa entropia. Em caso de vazamento de banco, um hash sem segredo pode ser testado offline.

Obrigatório:

- armazenar hash com pepper/segredo de servidor, preferencialmente HMAC com chave em configuração segura;
- incluir `challengeId`, `subjectId` e `purpose` no material assinado/hasheado;
- manter expiração curta;
- nunca salvar código em claro;
- nunca logar código, token de challenge ou payload bruto de autenticação.

### Rate limiting e proteção contra abuso são parte do core

Hoje não há rate limiting global visível para `/api/auth/token`, `/api/platform-auth/token`, confirmação de e-mail, reset de senha ou futuro `confirm-2fa`.

Obrigatório:

- limitar tentativas por IP, tenant, username/e-mail, subject e challenge;
- limitar criação de challenge para evitar bombardeio de e-mails;
- aplicar resposta genérica para não facilitar enumeração;
- registrar eventos de segurança sem segredos;
- considerar lockout temporário progressivo para login humano e 2FA.

Detalhamento esperado:

- aplicar rate limiting apenas aos fluxos humanos, sem penalizar agressivamente `grant_type=client_credentials`;
- separar políticas por finalidade:
  - `auth_password_grant`: `POST /api/auth/token` quando `grant_type=password`;
  - `platform_password_grant`: `POST /api/platform-auth/token`;
  - `auth_confirm_2fa`: `POST /api/auth/confirm-2fa`;
  - `platform_confirm_2fa`: `POST /api/platform-auth/confirm-2fa`;
  - `two_factor_management`: endpoints `POST /api/accounts/me/2fa/*` e `POST /api/platform-admins/me/2fa/*`;
  - `account_recovery`: confirmação de e-mail, forgot password e reset password;
- compor chaves de rate limit com os dados disponíveis para cada endpoint:
  - IP remoto;
  - tenant, quando existir `X-Tenant-Id` ou claim `tenantId`;
  - username/e-mail normalizado para login, reset e confirmação de e-mail;
  - `challenge_id` para confirmação 2FA;
  - subject autenticado para endpoints `me/2fa`;
- manter resposta HTTP `429 Too Many Requests` com mensagem genérica, sem informar se usuário, e-mail ou challenge existem;
- registrar evento sanitizado quando o limite for atingido, sem senha, código, token, cookie ou body completo;
- manter contadores em memória nesta etapa, sem Redis, assumindo limite por instância;
- documentar que ambientes com múltiplas instâncias precisarão de backend distribuído de rate limit em etapa futura.

Limites iniciais sugeridos:

- password grant humano: 10 tentativas por minuto por IP + tenant + username/e-mail;
- criação de challenge 2FA: 5 challenges por 10 minutos por subject;
- confirmação 2FA: 5 tentativas por 5 minutos por IP + challenge;
- forgot/reset password: 5 tentativas por 10 minutos por IP + tenant + e-mail/username;
- endpoints `me/2fa`: 5 tentativas por 10 minutos por subject.

### Revogar sessões em ações sensíveis

Refresh tokens atuais são persistidos por tenant e não existe método para revogar todos os tokens de um sujeito.

Adicionar ao `IRefreshTokenRepository`:

- revogar por `tenantId`, `subjectId` e `subjectType`;
- revogar por token específico, se necessário;
- opcionalmente revogar por `CreatedAtUtc < authTime`.

Usar revogação ao:

- ativar/desativar 2FA;
- resetar senha;
- trocar senha;
- desativar conta;
- remover admin de tenant;
- desativar platform admin.

Sem isso, um refresh token antigo pode continuar emitindo access token depois de uma mudança de segurança.

### Tokens emitidos após 2FA devem carregar contexto de autenticação

Adicionar claims úteis para auditoria e policies futuras:

- `amr`: `pwd` e `mfa`, ou `pwd`, `email_otp`;
- `auth_time`;
- `mfa_method`: `email_code` ou `totp`;
- opcionalmente `mfa_verified=true`.

Isso permite exigir MFA em endpoints sensíveis no futuro sem redesenhar a autenticação.

### E-mails de 2FA não devem depender de template inseguro

Para platform admin, o envio deve usar canal/template de sistema, não template editável por tenant. Para usuário final, definir explicitamente se o tenant pode customizar o template de 2FA; se puder, o template não deve permitir exfiltração de segredos além do próprio código destinado ao usuário.

Não usar código de 2FA em URL, path ou query string. Confirmação de login deve usar `POST` com body.

### Requests Bruno devem acompanhar os endpoints

Criar arquivos `.bru` no padrão atual da collection `bruno/Lightcode.Registration`, com `meta`, verbo, `url`, headers, body, variáveis necessárias e bloco `docs`.

Obrigatório:

- manter os endpoints de usuário final na pasta `bruno/Lightcode.Registration/Auth` quando forem parte do login/token;
- manter os endpoints de gerenciamento de conta em `bruno/Lightcode.Registration/Accounts`;
- manter os endpoints de platform admin em `bruno/Lightcode.Registration/Platform`;
- usar `{{baseUrl}}`, `{{tenantId}}`, `{{platformToken}}`, `{{accessToken}}`, `{{challengeId}}` e `{{twoFactorCode}}` conforme o padrão existente;
- não commitar códigos reais, senhas reais, JWTs reais, refresh tokens ou client secrets reais nos `.bru`;
- documentar no bloco `docs`:
  - finalidade do endpoint;
  - headers obrigatórios;
  - corpo esperado;
  - formato esperado de `Data`;
  - erros comuns;
  - observação de segurança quando o endpoint lida com senha, código 2FA ou token.

Arquivos mínimos:

- `Auth/Confirm 2FA.bru`;
- `Accounts/2FA Enable Begin.bru`;
- `Accounts/2FA Enable Confirm.bru`;
- `Accounts/2FA Disable Begin.bru`;
- `Accounts/2FA Disable Confirm.bru`;
- `Platform/Platform Auth Confirm 2FA.bru`;
- `Platform/Platform 2FA Enable Begin.bru`;
- `Platform/Platform 2FA Enable Confirm.bru`;
- `Platform/Platform 2FA Disable Begin.bru`;
- `Platform/Platform 2FA Disable Confirm.bru`.

Atualizar também os `.bru` já existentes:

- `Auth/Issue Token.bru` para documentar `requires_2fa`;
- `Auth/Client Credentials.bru` para registrar explicitamente que não aplica 2FA;
- `Platform/Platform Auth Token.bru` para documentar `requires_2fa`;
- ambientes `LOCAL.bru` e `DOCKER.bru` com placeholders seguros para `challengeId` e `twoFactorCode`, se necessário.

## Fases de Implementação

### Fase 1 - Preparar base

- Renomear suporte atual para confirmação de e-mail.
- Criar contratos de resposta para token ou challenge.
- Criar entidades e repositórios de challenge.
- Criar serviço `EmailCodeTwoFactorMethod`.
- Definir campos reservados de segurança e bloquear esses campos nos payloads genéricos de conta.
- Adicionar revogação por sujeito em `IRefreshTokenRepository`.
- Criar políticas de rate limiting humanas com chaves por IP, tenant, username/e-mail, subject e challenge.

### Fase 2 - Usuário final

- Adicionar configuração 2FA no documento de usuário.
- Adicionar configuração `auth.twoFactor.mode` no config do schema de autenticação/cadastro do usuário final.
- Suportar `disabled`, `optional` e `required`.
- Criar endpoints de begin/confirm enable e disable email 2FA para conta.
- Alterar `AuthenticationAppService.IssuePasswordGrantAsync`.
- Criar `POST /api/auth/confirm-2fa`.
- Garantir que `client_credentials` continue emitindo token técnico sem 2FA. Esse fluxo é autenticação de aplicação e deve apenas continuar funcionando sem desafio 2FA.
- Garantir que a criação de schema e `/api/accounts/admin` não dependam de 2FA.
- Garantir que `confirm-2fa` recarregue usuário, status e roles antes de emitir token.
- Garantir revogação de refresh tokens em ativação/desativação de 2FA, troca de senha e reset de senha.
- Adicionar `.bru` dos endpoints de usuário final e atualizar `.bru` de `/api/auth/token`.
- Criar o projeto `Lightcode.Registration.Tests`, se ainda não existir.
- Cobrir com testes no projeto `Lightcode.Registration.Tests`.

### Fase 3 - Platform Admin

- Adicionar campos 2FA em `PlatformAdmin`.
- Criar endpoints de begin/confirm enable e disable no perfil admin.
- Alterar `PlatformAdminAppService.IssueTokenAsync`.
- Criar `POST /api/platform-auth/confirm-2fa`.
- Ajustar BFF/front para tela de código.
- Expor endpoint de status para o painel e bloquear operações redundantes de enable/disable com `409`.
- Enviar e-mail de 2FA para platform admin via SMTP master, usando e-mails globais da plataforma.
- Adicionar `.bru` dos endpoints de Platform Admin e atualizar `.bru` de `/api/platform-auth/token`.

Observação: hoje `IEmailEnqueuePublisher` recebe `tenantId`, então o envio de 2FA para Platform Admin não deve depender de um tenant do usuário logado, pois no login ainda não existe tenant ativo. A decisão é seguir com a Opção C: SMTP master para e-mails globais da plataforma.

### Fase 4 - Caminho para TOTP

- Adicionar `TotpTwoFactorMethod`.
- Guardar segredo criptografado.
- Criar setup com QR code/otpauth URI.
- Criar recovery codes.

## Testes Mínimos

Os testes devem ser incluídos no projeto `Lightcode.Registration.Tests`. Se o projeto ainda não existir, ele deve ser criado antes da implementação dos cenários abaixo.

- Password grant sem 2FA emite token como hoje.
- Password grant com 2FA ativo não emite token e cria challenge.
- Password grant com `auth.twoFactor.mode=disabled` nunca cria challenge.
- Password grant com `auth.twoFactor.mode=optional` cria challenge apenas quando o usuário ativou 2FA.
- Password grant com `auth.twoFactor.mode=required` cria challenge mesmo sem ativação individual do usuário, usando `email_code` quando configurado.
- Client credentials continua emitindo token técnico sem challenge 2FA.
- Confirmação com código válido emite token.
- Confirmação com código inválido incrementa tentativas.
- Challenge expirado não emite token.
- Challenge usado não pode ser reutilizado.
- Refresh token não deve exigir novo 2FA.
- Platform admin com 2FA ativo recebe challenge antes do platform token.
- Platform admin não gera challenge de desativação quando o 2FA já estiver inativo.
- Platform admin não gera challenge de ativação quando o 2FA já estiver ativo.
- Challenge não armazena senha nem segredo sensível.
- Novo login inválida challenge pendente anterior do mesmo sujeito.
- Ativar/desativar 2FA exige autenticação recente.
- Payload genérico de cadastro/update não consegue criar, alterar ou remover campos internos de 2FA.
- `confirm-2fa` não aceita challenge de outro tenant, outro subject type ou outro purpose.
- Duas confirmações concorrentes do mesmo challenge resultam em apenas um token emitido.
- Ativar/desativar 2FA, trocar senha e resetar senha revogam refresh tokens existentes do sujeito.
- Token emitido após 2FA contém claims de contexto como `amr` e `auth_time`.
- Rate limiting bloqueia excesso de tentativas em password grant humano, confirmação 2FA e endpoints de gerenciamento 2FA.
- Rate limiting de password grant humano usa username/e-mail na chave e não trata `client_credentials` como login humano.
- Resposta de rate limit é genérica e não revela existência de usuário, e-mail ou challenge.
- Arquivos `.bru` existem para todos os endpoints novos e os `.bru` de token documentam `requires_2fa`.
