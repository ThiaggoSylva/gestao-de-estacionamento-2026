# Proposal

## Why

A operação P0 está implementada e verificada localmente, mas a publicação Azure continua bloqueada: o tenant disponível não possui assinatura acessível e o login foi recusado por security defaults. Para não repetir trabalho manual quando o acesso existir, esta change prepara a infraestrutura e o processo reproduzível de homologação, sem provisionar recursos nem publicar.

## What Changes

- Adicionar Bicep para um ambiente de homologação com Azure Static Web Apps (SPA), App Service Linux (.NET 10) para a API, Azure SQL e Key Vault.
- Configurar a SPA para servir rotas Angular com fallback e encaminhar `/api` pelo linked backend do Static Web Apps, mantendo a API acessível somente pelo proxy.
- Adicionar scripts PowerShell para provisionar, publicar, aplicar migrações/provisionar contas sintéticas e executar smoke tests.
- Adicionar workflow manual do GitHub Actions com autenticação OIDC, sem gatilho automático e sem produção.
- Documentar parâmetros, procedimento, limitações e gates pendentes.

## Capabilities

### New Capabilities

- `deployment/homologation`: infraestrutura como código e procedimento reproduzível para publicar a SPA e a API em homologação Azure, com segredos em Key Vault e dados sintéticos.

### Modified Capabilities

Nenhuma. O comportamento da aplicação e os requisitos de sessão/operação não mudam.

## Impact

Novos arquivos em `infra/azure`, `scripts/azure`, `.github/workflows`, `.gitignore` e configuração estática da SPA. Nenhum recurso Azure é criado por esta change. Permanecem fora do escopo: provisionamento/publicação real, produção, relatórios P1, domínio personalizado, rede privada, backup/restore, política de privacidade/retenção e aprovações de negócio/operação.
