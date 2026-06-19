# Fluxos de autenticacao com 2FA

Diagramas Mermaid na ordem principal do fluxo.

## Arquivos

- `01-preparacao-cliente-aplicacao.mmd`: onboarding do tenant, token tecnico, schema, conta e confirmacao de e-mail.
- `02-client-credentials-sem-2fa.mmd`: detalhe da autenticacao tecnica da aplicacao cliente. Nao usa 2FA.
- `03-politica-2fa-schema-usuario-final.mmd`: decisao de politica `disabled`, `optional` ou `required` no schema.
- `04-login-usuario-sem-2fa.mmd`: login de usuario final quando a politica nao exige challenge.
- `05-login-usuario-com-2fa-email.mmd`: login de usuario final com challenge por e-mail.
- `06-ativar-2fa-email-usuario.mmd`: ativacao individual de 2FA por e-mail.
- `07-desativar-2fa-usuario.mmd`: desativacao de 2FA.
- `08-login-platform-admin-com-2fa.mmd`: login do admin da plataforma com 2FA via SMTP master.
- `09-emitir-tenant-token-apos-platform-2fa.mmd`: emissao do token principal do tenant.
- `10-rate-limiting-2fa-auth.mmd`: rate limiting dos fluxos humanos de autenticacao e 2FA.
- `11-caminho-futuro-totp.mmd`: caminho futuro para TOTP.

## Decisoes fixas

- Confirmacao de e-mail de cadastro e diferente de 2FA de login.
- `client_credentials` sempre funciona sem 2FA.
- `auth.twoFactor.mode=disabled` nao cria challenge.
- `auth.twoFactor.mode=optional` permite ativacao pelo proprio usuario.
- `auth.twoFactor.mode=required` forca challenge para todos os usuarios do schema.
- Platform Admin usa SMTP master para e-mails globais antes de haver tenant selecionado.
