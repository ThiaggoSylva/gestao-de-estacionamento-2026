# Tasks

## 1. Preparação

- [x] 1.1 Registrar proposta, design e especificação da preparação de homologação Azure, mantendo provisionamento/publicação e produção fora desta change.
- [ ] 1.2 Criar Bicep parametrizado para Static Web Apps, App Service Linux, Azure SQL e Key Vault, sem segredos versionados.
- [ ] 1.3 Configurar fallback de rotas Angular e encaminhamento `/api` no Static Web Apps.
- [ ] 1.4 Criar scripts de deploy, bootstrap de banco/contas sintéticas e smoke tests.
- [ ] 1.5 Criar workflow manual com OIDC, sem gatilho automático e sem produção.
- [ ] 1.6 Documentar parâmetros, procedimento, limitações e gates pendentes.

## 2. Verificação local

- [ ] 2.1 Compilar o Bicep com `az bicep build` e revisar avisos.
- [ ] 2.2 Validar sintaxe dos scripts PowerShell e do workflow YAML.
- [ ] 2.3 Verificar que nenhum segredo, connection string ou senha foi versionado.
- [ ] 2.4 Executar build/testes existentes novamente se houver alteração de código de aplicação; a configuração estática da SPA não altera o runtime local.

## 3. Provisionamento e homologação real (bloqueados)

- [ ] 3.1 Confirmar assinatura/tenant Azure com permissão de Contributor e aprovar região, custos e responsáveis.
- [ ] 3.2 Executar o deploy do Bicep em homologação e publicar API/SPA.
- [ ] 3.3 Executar bootstrap de migrações e contas sintéticas no Azure SQL.
- [ ] 3.4 Validar no navegador real cookies, CSRF, logout, cabeçalhos/origens, IP de origem, fallback de rotas, `/api` pelo linked backend e acesso direto à API negado.
- [ ] 3.5 Repetir as jornadas E2E de operação no domínio publicado e registrar evidências.
- [ ] 3.6 Configurar monitoramento, backup/restore de SQL e key ring, rotação de chaves, custos e política de destruição do ambiente.
- [ ] 3.7 Obter aprovações de negócio, privacidade e operação antes de qualquer produção.
