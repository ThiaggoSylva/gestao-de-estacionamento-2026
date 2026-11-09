# Design

## Context

`docs/tdd.md` DT-07/DT-11 concentram a publicação Azure na Fase 5: SPA e API em sites distintos, navegador usando `/api` na origem da SPA por proxy, API sem exposição direta, Azure SQL, segredos/chaves gerenciados e implantação primeiro em homologação. O PRD §12 e `docs/pendencias-implantacao.md` mantêm aprovações, privacidade, backup/restore e produção pendentes.

O acesso Azure disponível nesta máquina falhou (`AADSTS530035`, sem assinatura). Esta change prepara os artefatos e o procedimento; não provisiona nem publica.

## Goals / Non-Goals

**Goals:** produzir IaC compilável e parametrizada, scripts de deploy/bootstrap reproduzíveis, workflow manual e documentação que permitam publicar homologação assim que houver assinatura e aprovação.

**Non-Goals:** criar recursos Azure, publicar em homologação/produção, definir domínio/SKU final, implementar rede privada, backup/restore, política de privacidade, P1 ou aprovações de negócio.

## Decisions

1. **Static Web Apps + linked App Service (DT-07).** A SPA fica no Static Web Apps Standard e a API em App Service Linux separado. O linked backend encaminha `/api/*` para a API e configura a identidade `Azure Static Web Apps (Linked)` no App Service, restringindo o acesso direto. Alternativa de dois App Services com proxy IIS foi rejeitada por fragilidade operacional; Front Door foi adiado por custo/complexidade.
2. **API Linux .NET 10.** App Service Plan B1, `DOTNETCORE|10.0`, HTTPS obrigatório, FTP desabilitado, TLS 1.2+, `alwaysOn` e afinidade desabilitada. A versão acompanha o TDD e o build local.
3. **Azure SQL Basic.** Banco dedicado de homologação com firewall para serviços Azure e IP do executor. A connection string fica em Key Vault; o segredo administrativo não é versionado. Para produção, avaliar autenticação gerenciada e SKU/rede aprovados.
4. **Key Vault com RBAC.** Identidade gerenciada atribuída à API recebe `Key Vault Secrets User`; app settings usam referências `@Microsoft.KeyVault(...)`. Segredos sintéticos também ficam no vault para o bootstrap, mas não são necessários no processo em `Production`.
5. **Bootstrap fora do processo publicado.** A aplicação sobe em `Production`, sem migração/provisionamento automáticos. Um comando local em `Development` aplica migrações e provisiona as três contas sintéticas contra o Azure SQL; depois o processo publicado não recebe senhas sintéticas. Alternativa de subir homologação em `Development` foi rejeitada.
6. **Data Protection em `/home` para homologação single-instance.** O key ring fica no armazenamento persistente do App Service. Isso não substitui Blob/Key Vault, backup/restore e rotação exigidos para produção; o limite fica explícito na documentação.
7. **Deploy manual e auditável.** Script PowerShell e workflow `workflow_dispatch`; sem deploy automático, sem produção e sem credenciais de longa duração no repositório. O workflow usa OIDC e falha de forma explícita se os segredos/variáveis não estiverem configurados.
8. **Sem recursos nesta change.** `az bicep build` e validações estáticas são o critério local; provisionamento e E2E no domínio real permanecem tarefas bloqueadas.

## Risks / Trade-offs

- **Linked backend é específico do Static Web Apps Standard:** mudança de plano/região pode exigir revisão; validar no primeiro provisionamento.
- **Key ring em `/home`:** adequado apenas a homologação single-instance; perda do storage ou scale-out sem compartilhamento quebra a leitura de documentos. Produção exige armazenamento gerenciado e restore testado.
- **Senha administrativa do SQL em segredo:** aceitável para bootstrap de homologação; produção deve avaliar identidade gerenciada e rotação.
- **Custo:** SWA Standard + App Service B1 + SQL Basic geram custo contínuo; o ambiente deve ser destruído ou desligado após a homologação.
- **Workflow não executado:** sem assinatura, OIDC e permissões não foram validados; a primeira execução real é um gate.
- **Proxy/cookies:** a sessão depende de o linked backend preservar `Set-Cookie`, `Origin` e `X-Forwarded-*`; o smoke/E2E no domínio publicado é obrigatório.

## Migration Plan

1. Compilar o Bicep e validar os scripts/workflow localmente.
2. Quando houver assinatura aprovada, criar o resource group e implantar o Bicep em homologação.
3. Publicar a API, executar o bootstrap de migração/contas sintéticas e publicar a SPA.
4. Executar smoke tests e as jornadas E2E no domínio publicado, incluindo acesso direto à API negado.
5. Registrar evidências e aprovações. Rollback inicial é remover o resource group de homologação; nenhum dado de produção é tocado.
