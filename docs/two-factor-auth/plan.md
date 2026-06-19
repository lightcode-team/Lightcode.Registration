# Plano Vivo de 2FA

## Estado Atual

Core inicial implementado conforme baseline v1.0.0.

Snapshot completo:
[versions/plan.v1.0.0.md](versions/plan.v1.0.0.md)

## Decisões Ativas

- `client_credentials` não usa 2FA.
- `password grant` pode retornar `requires_2fa`.
- `refresh_token` não cria challenge.
- Platform Admin usa SMTP master.
- TOTP está preparado, mas não funcional.

## Contratos Ativos

- `/api/auth/token`
- `/api/auth/confirm-2fa`
- `/api/platform-auth/token`
- `/api/platform-auth/confirm-2fa`

Resumo:

- `requires_2fa=false`: retorna `token`.
- `requires_2fa=true`: retorna `challenge`, sem JWT.

## Pendências

- Backfill/template `account-login-2fa-code`.
- Logs de segurança centralizados `2fa_*`.
- Sanitizar ambientes Bruno com tokens reais.
- TOTP funcional em versão futura.

## Próxima Versão Prevista

`v1.1.0`: hardening operacional.
