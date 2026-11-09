# Spec Delta

## Purpose

Preparar uma publicação reproduzível de homologação Azure para a SPA e a API, com segredos gerenciados, banco dedicado e validações que separam o que é local do que exige assinatura e aprovação.

## ADDED Requirements

### Requirement: Infraestrutura como código compilável
O projeto SHALL conter Bicep parametrizado para criar resource group (via script), Static Web Apps, App Service, Azure SQL e Key Vault em homologação, sem segredos versionados.

#### Scenario: Compilação sem assinatura
- **WHEN** o executor roda `az bicep build` sobre o arquivo principal
- **THEN** o template compila sem erros e sem credenciais embutidas

#### Scenario: Parâmetros obrigatórios ausentes
- **WHEN** o deploy é iniciado sem login Azure ou sem parâmetros obrigatórios
- **THEN** o script falha antes de criar qualquer recurso

### Requirement: SPA e API em sites distintos com `/api` na origem da SPA
O sistema SHALL publicar a SPA em Static Web Apps e a API em App Service separado, com o linked backend encaminhando `/api/*` e a API restrita ao tráfego do linked backend.

#### Scenario: Navegação direta para rota Angular
- **WHEN** o browser acessa uma rota como `/check-in` no domínio da SPA
- **THEN** o Static Web Apps entrega `index.html` e o roteador Angular resolve a tela

#### Scenario: Chamada de API pela SPA
- **WHEN** a SPA chama `/api/v1/session/antiforgery` no domínio da SPA
- **THEN** o linked backend encaminha para a API e preserva cookies, `Origin`, cabeçalhos de CSRF e a cadeia de proxy/origem

#### Scenario: Acesso direto à API
- **WHEN** o browser tenta acessar a URL do App Service da API fora do linked backend
- **THEN** o acesso é negado pela identidade `Azure Static Web Apps (Linked)`

### Requirement: Segredos e configuração gerenciados
O sistema SHALL armazenar connection string e senhas sintéticas em Key Vault e SHALL conceder à API somente leitura dos segredos necessários por identidade gerenciada.

#### Scenario: App settings sem segredo em texto claro
- **WHEN** o Bicep é revisado
- **THEN** connection string e senhas não aparecem em arquivos versionados nem em app settings literais

#### Scenario: Processo publicado em Production
- **WHEN** a API inicia no App Service
- **THEN** ela usa `ASPNETCORE_ENVIRONMENT=Production`, sem migração ou provisionamento automáticos

### Requirement: Bootstrap controlado de banco e contas sintéticas
O procedimento SHALL aplicar migrações e provisionar contas sintéticas por comando local explícito antes do primeiro uso da homologação.

#### Scenario: Primeiro bootstrap
- **WHEN** o executor roda o script de bootstrap com a connection string de homologação
- **THEN** as migrações são aplicadas, as três contas são provisionadas uma única vez e nenhuma senha é impressa

#### Scenario: Repetição do bootstrap
- **WHEN** o bootstrap é executado novamente
- **THEN** migrações pendentes são aplicadas de forma idempotente e contas existentes não têm senha redefinida

### Requirement: Publicação manual e sem produção
O repositório SHALL oferecer workflow manual e script de deploy que exigem confirmação explícita e SHALL NOT publicar em produção.

#### Scenario: Workflow sem credenciais
- **WHEN** o workflow é disparado sem variáveis/segredos OIDC configurados
- **THEN** ele falha de forma explícita antes de provisionar ou publicar

#### Scenario: Ambiente de produção
- **WHEN** o executor tenta usar o procedimento para produção
- **THEN** a documentação e os parâmetros deixam claro que o procedimento é exclusivo de homologação e exige nova aprovação

### Requirement: Verificação pós-deploy
O procedimento SHALL executar smoke tests do domínio publicado e registrar que cookies/CSRF, cabeçalhos/origens, jornadas e acesso direto à API só são comprovados após o provisionamento real.

#### Scenario: Smoke local do artefato
- **WHEN** não há assinatura Azure
- **THEN** a change é considerada preparada, mas as tarefas de provisionamento e E2E publicado permanecem pendentes
