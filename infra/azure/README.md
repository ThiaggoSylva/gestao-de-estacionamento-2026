# Homologação Azure — preparação

Este diretório contém a infraestrutura como código e o procedimento para publicar a operação P0 em **homologação** Azure. Nenhum recurso foi provisionado por esta change: a conta Azure disponível não tinha assinatura acessível e o login foi recusado por security defaults (`AADSTS530035`). O ambiente de produção não é suportado por este procedimento.

## Topologia

```text
Browser
  │ HTTPS (mesma origem)
  ▼
Azure Static Web Apps Standard (SPA Angular)
  │ linked backend /api/*
  ▼
Azure App Service Linux .NET 10 (API)
  │
  ▼
Azure SQL (banco operacional)

App Service ── identidade gerenciada ──► Key Vault (connection string e senhas sintéticas)
```

O linked backend do Static Web Apps encaminha `/api/*`, preserva a origem e configura a identidade `Azure Static Web Apps (Linked)` no App Service. O acesso direto ao hostname da API deve ser negado; o smoke test falha se ele responder com sucesso.

## Arquivos

| Arquivo | Função |
| --- | --- |
| `infra/azure/main.bicep` | Static Web Apps, App Service, Azure SQL, Key Vault, identidade e linked backend. |
| `frontend/public/staticwebapp.config.json` | Fallback de rotas Angular e exclusão de `/api/*` do fallback. |
| `scripts/azure/deploy-hml.ps1` | Provisiona, publica API/SPA, aplica migrações, provisiona contas sintéticas e executa smoke test. |
| `scripts/azure/destroy-hml.ps1` | Remove o resource group de homologação. |
| `scripts/azure/validate-local.ps1` | Valida Bicep, scripts PowerShell, YAML do workflow e JSON da SPA sem acessar a Azure. |
| `.github/workflows/azure-hml.yml` | Execução manual com OIDC e aprovação de environment. |

## Pré-requisitos

- Assinatura Azure ativa com permissão **Contributor** no resource group e **User Access Administrator** (ou Owner) para a atribuição de `Key Vault Secrets User`.
- `az login` na conta/tenant correta; o script valida a sessão antes de criar qualquer recurso.
- .NET SDK 10.0.401 (conforme `backend/global.json`), Node.js 22+, npm e acesso ao GitHub para o workflow.
- Bicep CLI (`az bicep install`, se necessário) e extensão `staticwebapp` do Azure CLI (`az extension add --name staticwebapp`, se necessário).
- Login administrativo e senhas fortes para o SQL e para as três contas sintéticas. Não versione esses valores.
- A senha administrativa do SQL deve evitar `;`, aspas simples/duplas e quebra de linha, pois a connection string é montada por interpolação. O script e o workflow validam essa restrição.
- Para o workflow: OIDC configurado com os secrets `AZURE_CLIENT_ID`, `AZURE_TENANT_ID`, `AZURE_SUBSCRIPTION_ID`, `AZURE_SQL_ADMIN_PASSWORD`, `SYNTHETIC_VALET_PASSWORD`, `SYNTHETIC_CASHIER_PASSWORD`, `SYNTHETIC_ADMIN_PASSWORD` e a variável `AZURE_SQL_ADMIN_LOGIN`. O environment `azure-hml` deve ter revisores obrigatórios.

## Validação local (sem Azure)

```powershell
az bicep install   # se o Bicep CLI ainda não estiver instalado
az bicep build --file infra/azure/main.bicep --stdout | Out-Null
.\scripts\azure\validate-local.ps1
git diff --check
```

O Bicep compila sem assinatura. Os scripts e o workflow não provisionam nada durante a validação.

## Publicação manual

O script exige digitar `HOMOLOGACAO` e cria recursos faturáveis.

```powershell
az login --tenant <tenant-id-ou-dominio>

$sqlAdmin = Read-Host -AsSecureString 'Senha do admin SQL'
$valet = Read-Host -AsSecureString 'Senha sintética do manobrista'
$cashier = Read-Host -AsSecureString 'Senha sintética do caixa'
$admin = Read-Host -AsSecureString 'Senha sintética do administrador'

.\scripts\azure\deploy-hml.ps1 `
  -ResourceGroup rg-estacionamento-hml `
  -SqlAdminLogin estacionamentoadmin `
  -SqlAdminPassword $sqlAdmin `
  -ValetPassword $valet `
  -CashierPassword $cashier `
  -AdminPassword $admin
```

O script:

1. cria o resource group e implanta `main.bicep`;
2. adiciona o IP atual ao firewall do SQL para o bootstrap e remove a regra ao final;
3. publica a API no App Service;
4. roda a API localmente em `Development` com `--provision-synthetic-accounts` contra o Azure SQL, aplicando migrações e criando as contas uma única vez;
5. publica a SPA no Static Web Apps;
6. reinicia a API e verifica um token antiforgery válido via SPA, falhando se o acesso direto à API responder com sucesso.

O processo publicado roda em `Production`, sem migração ou provisionamento automáticos. As senhas sintéticas ficam no Key Vault e são usadas apenas pelo bootstrap local.

## Publicação pelo GitHub Actions

Execute manualmente `azure-hml` e informe `HOMOLOGACAO`. O workflow:

- autentica por OIDC, sem credenciais de longa duração;
- valida os secrets e as restrições de senha antes de criar recursos;
- cria o resource group e implanta o Bicep com um arquivo de parâmetros temporário;
- publica a API, aplica migrações/provisiona contas e publica a SPA;
- remove a regra de firewall do runner e o arquivo temporário de parâmetros ao final;
- executa os smoke tests de token antiforgery via proxy e de bloqueio direto;
- usa o environment `azure-hml` para aprovação.

Sem os secrets/variáveis configurados, o workflow falha antes de provisionar. A primeira execução real ainda não foi feita.

## Verificação pós-deploy

- [ ] `https://<spa>/` entrega a SPA por HTTPS.
- [ ] Navegação direta para `/check-in`, `/search`, `/queue` e `/admin` entrega `index.html`.
- [ ] `/api/v1/session/antiforgery` pela SPA retorna 200, um `requestToken` válido e define cookies `Secure`/`HttpOnly` host-only.
- [ ] Login/logout funcionam e o antiforgery é renovado após o login.
- [ ] Cabeçalhos/origens, IP de origem e rate limit do login foram validados no domínio publicado.
- [ ] Acesso direto a `https://<api>/healthz` é negado.
- [ ] Jornadas Playwright de operação passam no domínio publicado.
- [ ] Documento/vistorias não aparecem em logs; perfil manobrista não recebe finanças.
- [ ] Métricas/logs, custos, backup/restore, key ring e política de destruição foram revisados por operação/segurança.

## Limitações e bloqueios

- **Não é produção.** Aprovações de negócio, privacidade, operação, RPO/RTO e restore continuam obrigatórias.
- **Data Protection em `/home`:** adequado a uma instância; não substitui armazenamento gerenciado, backup e rotação de chaves.
- **Senha administrativa do SQL:** usada no bootstrap; produção deve avaliar identidade gerenciada, rede privada e rotação. Evite `;`, aspas e quebra de linha na senha administrativa.
- **IP de origem e rate limit:** a API hoje confia em `X-Forwarded-Proto`; validar no ambiente publicado se o linked backend encaminha `X-Forwarded-For` e se o rate limit por IP identifica o cliente. Qualquer ajuste de aplicação exige nova change e testes.
- **Sem domínio personalizado, WAF, private endpoint, backup automatizado ou monitoramento completo.** O Static Web Apps usa o hostname padrão.
- **Contas sintéticas:** adequadas apenas a homologação; não criar dados reais sem aprovação.
- **Key Vault com purge protection:** recriar o ambiente com o mesmo nome após exclusão pode exigir purge/aguardar a retenção; use outro `namePrefix` se necessário.
- **Primeira execução do workflow:** não validada por falta de assinatura. O login atual retorna `AADSTS530035` e não lista assinaturas.

## Destruição

```powershell
.\scripts\azure\destroy-hml.ps1 -ResourceGroup rg-estacionamento-hml
```

Confirme que o resource group é de homologação antes de executar. O Key Vault mantém soft delete conforme a política configurada.
