# Fluxos de autenticaÃ§Ã£o com 2FA

Diagramas Mermaid dos fluxos previstos para o core de 2FA.

## Arquivos

- `01-client-credentials-sem-2fa.mmd`: autenticaÃ§Ã£o tÃ©cnica da aplicaÃ§Ã£o cliente. NÃ£o usa 2FA.
- `02-login-usuario-sem-2fa.mmd`: login de usuÃ¡rio final quando a polÃ­tica nÃ£o exige challenge.
- `03-login-usuario-com-2fa-email.mmd`: login de usuÃ¡rio final com challenge por e-mail.
- `04-ativar-2fa-email-usuario.mmd`: ativaÃ§Ã£o individual de 2FA por e-mail.
- `05-desativar-2fa-usuario.mmd`: desativaÃ§Ã£o de 2FA.
- `06-login-platform-admin-com-2fa.mmd`: login do admin da plataforma com 2FA via SMTP master.
- `07-emitir-tenant-token-apos-platform-2fa.mmd`: emissÃ£o do token principal do tenant.
- `08-caminho-futuro-totp.mmd`: caminho futuro para TOTP.
- `09-politica-2fa-schema-usuario-final.mmd`: decisÃ£o de polÃ­tica `disabled`, `optional` ou `required` no schema do usuÃ¡rio final.
- `10-rate-limiting-2fa-auth.mmd`: rate limiting dos fluxos humanos de autenticacao e 2FA.

## DecisÃµes fixas

- `client_credentials` sempre funciona sem 2FA.
- `auth.twoFactor.mode=disabled` nÃ£o cria challenge.
- `auth.twoFactor.mode=optional` permite ativaÃ§Ã£o pelo prÃ³prio usuÃ¡rio.
- `auth.twoFactor.mode=required` forÃ§a challenge para todos os usuÃ¡rios do schema.
- Platform Admin usa SMTP master para e-mails globais antes de haver tenant selecionado.

