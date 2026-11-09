# Evidências — Fases 2–4 (operação P0)

Registro local executado em 2026-11-08. Este documento separa o que foi reproduzido nesta máquina do que continua pendente de decisão, homologação ou aprovação. Ele não representa aprovação de produção nem valida topologia Azure.

## Escopo verificado

- Cadastro administrativo de vagas e tarifa versionada, com snapshot da tarifa no check-in.
- Check-in idempotente com vistoria de entrada, ocupação única e identificador público.
- Busca e detalhe por placa/identificador com projeções por perfil: manobrista recebe documento mascarado e nenhum valor financeiro; caixa/admin recebem documento completo e finanças.
- Pedido de retirada com conferência documental, cálculo imutável de diárias/total, fila e início de busca.
- Pagamento integral idempotente, autorização excepcional de saldo pendente e entrega transacional com liberação da vaga.
- Ajuste financeiro auditado após a entrega como novo lançamento, sem editar valores históricos.
- Auditoria consultável pelo administrador para autorização, pagamento, ajuste e entrega.

## Ambiente da execução

- Windows, .NET SDK 10.0.401, SQL Server Express local em `.\SQLEXPRESS`.
- Angular 22.2 e Vitest 5.1 no frontend.
- Playwright 1.65 com Chromium 1254 em `%LOCALAPPDATA%/ms-playwright`.
- Bancos de teste dedicados e vazios no SQL Server Express; credenciais sintéticas fornecidas por variáveis de ambiente e não versionadas. Docker não foi utilizado nesta execução.

## Resultados

| Verificação | Comando resumido | Resultado |
| --- | --- | --- |
| Build .NET | `dotnet build backend/Estacionamento.slnx --no-restore` | Êxito, 0 avisos e 0 erros |
| Formatação .NET | `dotnet format Estacionamento.slnx --verify-no-changes --no-restore` | Sem alterações pendentes |
| Testes unitários MSTest | `dotnet test backend/tests/Estacionamento.UnitTests/... --no-build` | 15/15 aprovados |
| Testes arquiteturais | `dotnet test backend/tests/Estacionamento.ArchitectureTests/... --no-build` | 4/4 aprovados |
| Testes de integração MSTest | `dotnet test backend/tests/Estacionamento.IntegrationTests/... --no-build` | 22/22 aprovados em SQL Server real e banco novo |
| Vitest | `cd frontend && npx ng test --watch=false` | 14/14 aprovados |
| Build Angular | `cd frontend && npm run build` | Êxito |
| Playwright | `cd tests/e2e && npm test` | 5/5 aprovados (3 fundação + 2 operação) |

Os testes de integração cobrem, entre outros pontos, migração em banco vazio com tabelas operacionais e índices filtrados, concorrência de ocupação, idempotência de check-in/pedido/pagamento/entrega, projeções por perfil, autorização de saldo pendente, não retroatividade da tarifa e ajuste auditado após a entrega. O banco de integração e o de migração foram criados vazios para esta execução, com nomes dedicados.

## Jornadas Playwright

1. **Jornada completa e projeções por perfil:** administrador publica tarifa e cadastra vaga; caixa registra a entrada; manobrista busca o atendimento e vê documento mascarado, ausência de finanças e bloqueio da rota administrativa; caixa registra pedido, inicia busca, paga integralmente e entrega; painel volta a mostrar a vaga livre.
2. **Exceção de saldo e ajuste pós-entrega:** caixa registra entrada, pedido e busca; administrador autoriza saldo pendente; caixa entrega sem pagamento e o saldo permanece visível; administrador registra ajuste negativo após a entrega e carrega a auditoria com `ExceptionAuthorized`, `DeliveryCompleted` e `AdjustmentRegistered`.

O harness E2E usa `Authentication__IdleTimeoutSeconds=10` para também exercitar a expiração de sessão. `Authentication__LoginPermitLimit=100` é aplicado somente no processo local dos testes para permitir as várias trocas de perfil da suíte; o padrão da aplicação permanece 5 por minuto por origem.

## Reprodução local

Substitua os valores entre `<>` por segredos locais; não registre senhas ou conexões reais em arquivos versionados.

```bash
dotnet build backend/Estacionamento.slnx --no-restore
dotnet format backend/Estacionamento.slnx --verify-no-changes --no-restore

TEST_SQLSERVER_CONNECTION='<conexão de banco de teste novo>' \
TEST_MIGRATION_SQLSERVER_CONNECTION='<conexão de banco de migração novo>' \
TEST_VALET_PASSWORD='<senha sintética>' \
TEST_CASHIER_PASSWORD='<senha sintética>' \
TEST_ADMIN_PASSWORD='<senha sintética>' \
dotnet test backend/tests/Estacionamento.IntegrationTests/Estacionamento.IntegrationTests.csproj --no-build

cd frontend
npx ng test --watch=false
npm run build

cd ../tests/e2e
npm install
npx playwright install chromium

TEST_SQLSERVER_CONNECTION='<conexão de banco E2E novo>' \
TEST_VALET_PASSWORD='<senha sintética>' \
TEST_CASHIER_PASSWORD='<senha sintética>' \
TEST_ADMIN_PASSWORD='<senha sintética>' \
npm test
```

Para os testes de integração, use um banco vazio sempre que as senhas sintéticas mudarem: o provisionamento não redefine senhas de contas já existentes. O harness Playwright inicia a API em `Development` e a SPA separadamente, conforme `tests/e2e/playwright.config.ts`.

## Limites e pendências

Builds e testes locais comprovam apenas o comportamento especificado nesta máquina. Continuam pendentes, sem serem aprovados por estas evidências:

- relatórios P1 (RF-12), política comercial de descontos/estornos, gateway de pagamento, fotos, múltiplas unidades e emissão fiscal;
- CI, provisão/publicação Azure, topologia real de ingress/TLS e validação de proxy entre sites;
- privacidade, retenção e descarte de documentos/vistorias/histórico;
- backup/restore de banco e key ring, rotação de chaves, RPO/RTO, disponibilidade e contingência;
- medição de volume, picos e metas de desempenho;
- homologação e aprovações responsáveis registradas em `docs/pendencias-implantacao.md`.
