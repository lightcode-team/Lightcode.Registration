# Login Hospedado com 2FA

## Resumo

Evoluir a Razor existente em `/auth/login` para uma autenticação funcional de usuários finais, personalizada por tenant e compatível com aplicações web, SPA e mobile.

O fluxo usará Authorization Code com PKCE S256. Nenhum JWT, refresh token, senha ou código 2FA será exposto em URL, HTML ou JavaScript. O escopo inclui login, 2FA por e-mail, reenvio, retorno ao login e recuperação de senha.

## Alterações Principais

### Autorização e OAuth

- Exigir na entrada o formato de Authorization Request OAuth2/OIDC por query: `response_type=code`, `tenant_id`, `client_id`, `redirect_uri`, `scope`, `state`, `nonce`, `code_challenge` e `code_challenge_method=S256`.
- Adicionar `RedirectUris`, `AllowedScopes` e `RequireConsent` ao cadastro de `OAuthClient`; validar callback por correspondência exata, sem curingas ou fragmentos.
- Aceitar HTTPS, custom schemes registrados para mobile e HTTP apenas para localhost.
- Adicionar `grant_type=authorization_code` ao `/api/auth/token`, com `code`, `client_id`, `redirect_uri` e `code_verifier`.
- Preservar sem alterações os grants `password`, `refresh_token` e `client_credentials`.
- Rejeitar a tela antiga aberta somente com `tenantId`, exibindo erro de configuração, pois não existe destino seguro para o resultado.

### Estado Seguro do Fluxo

- Criar uma transação OAuth com identificador opaco e TTL de 15 minutos, contendo tenant, cliente, callback, state, nonce, scope e PKCE; nunca armazenar senha.
- Criar uma sessão de autenticação separada, vinculada à transação OAuth, para controlar usuário autenticado, estágio atual, 2FA, expiração e cancelamento.
- Criar authorization code aleatório, armazenado somente como hash, com validade de 60 segundos e consumo atômico.
- Vincular o code a tenant, cliente, usuário, callback, nonce, scope, correlationId e PKCE.
- No exchange, consumir o code uma única vez, recarregar usuário, status, roles e permissões e só então emitir access/refresh tokens.
- Redirecionar ao callback com `code` e o `state` original; erros anteriores à autenticação permanecem na UI e não são enviados para callbacks não validados.

### Views Razor

- Extrair um layout/partial compartilhado para branding, logo, fundo, alertas, campos, botões e estados de carregamento.
- Atualizar `Login.cshtml` com username, senha, mostrar/ocultar senha, “Esqueci minha senha” e validação acessível.
- Criar view de 2FA com seis dígitos, `autocomplete="one-time-code"`, destino mascarado, expiração, erros, reenvio com espera de 30 segundos e ação “Voltar ao login”.
- Reenvio cria novo challenge `purpose=login`, invalida o anterior e aplica rate limiting; voltar invalida o desafio e limpa a etapa autenticada.
- Criar view inicial de recuperação por username ou e-mail, sempre mostrando resposta genérica para evitar enumeração.
- Reestilizar a view existente de definição de nova senha e permitir retorno à jornada original quando a transação ainda for válida.
- Garantir funcionamento por formulários server-side com antiforgery; JavaScript será apenas melhoria de UX.
- Tornar todas as telas responsivas, navegáveis por teclado e com foco/alertas anunciados corretamente.

### Backend e Personalização

- Criar um orquestrador específico para login hospedado, reutilizando validação de credenciais, política 2FA, challenge service e emissão de tokens sem chamar diretamente o password grant que já emite JWT.
- Associar a sessão de autenticação ao challenge no estágio `AwaitingTwoFactor`; confirmar ou reenviar sem guardar nem solicitar novamente a senha.
- Manter `/api/auth/confirm-2fa` disponível para integrações API existentes.
- Expandir `FrontConfigMessages` com textos de login, 2FA, reenvio, recuperação e redefinição, sempre com fallback padrão para configurações antigas.
- Reutilizar o CSS, logo e background do tenant em todas as telas.
- Aplicar os limitadores existentes a login, confirmação 2FA e recuperação, acrescentando limite específico para reenvio.
- Criar coleções Mongo com TTL e índices para transações OAuth, sessões de autenticação, challenges 2FA e authorization codes; consumo e invalidação devem ser atômicos.
- Registrar auditoria em `AuthAuditLogs` para início do fluxo, tentativa/falha/sucesso de login, 2FA, emissão/consumo de code, recuperação e reset de senha.
- Usar `correlationId` para rastrear o fluxo de `/auth/login` até `/api/auth/token`, sem registrar senha, código 2FA, authorization code puro, tokens ou `code_verifier`.

## Contratos Públicos

- `GET /auth/login?...`: inicia a transação OAuth hospedada usando parâmetros OAuth2/OIDC na query.
- `POST /auth/login`: valida credenciais e encaminha para 2FA ou callback.
- `POST /auth/2fa`: confirma o código e conclui a autenticação.
- `POST /auth/2fa/resend`: substitui o challenge ativo.
- `POST /auth/2fa/cancel`: descarta o challenge e retorna ao login.
- `GET|POST /auth/forgot-password`: inicia recuperação com resposta não enumerável.
- `GET|POST /reset-password`: mantém o reset existente, agora com branding e continuidade.
- `POST /api/auth/token` com `grant_type=authorization_code`: troca code + PKCE por `AuthTokenResponse`.
- DTOs de OAuth client passam a aceitar e devolver `redirect_uris`, `allowed_scopes` e `require_consent`; clientes existentes recebem lista vazia e não ficam automaticamente habilitados para login hospedado.

## Testes e Aceite

- Login sem 2FA redireciona com code/state e o exchange emite tokens.
- Login com 2FA não cria token antes da confirmação e exibe o destino mascarado.
- Código correto conclui; código incorreto, expirado, reutilizado ou acima do limite é recusado.
- Reenvio respeita cooldown/rate limit e invalida o código anterior.
- Voltar ao login impede uso posterior do challenge.
- Callback, cliente, tenant, state, nonce, scope, correlationId e PKCE são preservados e validados; URI ou verifier divergente bloqueia o exchange.
- Authorization code e conclusão 2FA só podem ser consumidos uma vez, inclusive sob requisições concorrentes.
- Usuário desativado ou com permissões alteradas durante o fluxo não recebe token com estado antigo.
- Recuperação retorna a mesma mensagem para conta existente e inexistente; link válido redefine senha e revoga refresh tokens. Alteração de senha, e-mail, roles críticas e remoção de 2FA também revogam refresh tokens.
- Views passam em testes de controller, integração Mongo, antiforgery, validação HTML, teclado e viewport mobile.
- Testes de regressão confirmam que password, refresh e client credentials continuam funcionando.

## Premissas

- Primeira versão suporta somente `email_code`; a UI usa `verification_type` para permitir TOTP futuramente.
- Gestão de ativação/desativação de 2FA e platform admin ficam fora desta entrega.
- PKCE S256 é obrigatório para todos os clientes, inclusive aplicações com backend.
- Não haverá armazenamento de tokens em localStorage, sessionStorage, cookies da view ou parâmetros de URL.
