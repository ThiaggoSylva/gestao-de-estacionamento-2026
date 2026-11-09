# Design

## Context

Ver `proposal.md`, `docs/prd.md` §7–9 e `docs/tdd.md` §§3–5, 7–8. A Fase 1 entregou sessão, Identity, antiforgery, SPA/edge local e SQL Server real. Esta change implementa as Fases 2–4 (P0 operacional) localmente e mantém relatórios P1, CI, provisionamento Azure, homologação e publicação fora de escopo.

## Goals / Non-Goals

**Goals:** entregar a jornada completa de entrada, busca, pedido de retirada, cobrança fixada, pagamento integral, exceções administrativas e entrega transacional; aplicar autorização e projeções por perfil no servidor; persistir idempotência e auditoria nas mesmas transações; provar invariantes com SQL Server real, Vitest e Playwright locais.

**Non-Goals:** alterar autenticação/topologia; implementar relatórios RF-12; criar gateway de pagamento, fotos, múltiplas unidades, importação ou emissão fiscal; definir política comercial de desconto/estorno; provisionar/publicar Azure; substituir as aprovações e gates de produção.

## Decisions

1. **Modelo único e transacional (DT-04/05/12/14).** O `IdentityDbContext` existente passa a conter também as tabelas operacionais. Uma única transação SQL abrange atendimento, snapshot de tarifa, vistoria, ocupação, pedido, cobrança, pagamento, ajuste, autorização e auditoria. Alternativa de contexto separado exigiria transação distribuída e foi rejeitada. Entidades mutáveis (`ParkingSpot`, `ParkingSession`, `Occupancy`, `WithdrawalRequest`) usam `rowversion`; índices filtrados garantem no máximo uma ocupação aberta por vaga e por atendimento.

2. **Aggregate e regras de domínio.** `ParkingSession` é o aggregate root do ciclo operacional e concentra transições `Active → AwaitingWithdrawal → Searching → Delivered`. `WithdrawalRequest` e `Charge` pertencem ao ciclo e são criados juntos no pedido; a cobrança fixa segundos, diárias, tarifa capturada e total. `Payment`, `Adjustment`, `ExceptionAuthorization`, `Inspection`, `Occupancy` e `AuditEntry` são lançamentos append-only pelas operações normais. Diárias usam `max(1, ceil((pedido - entrada)/24h))`, com 24h exatas = 1 e 24h + 1s = 2.

3. **Tarifa e snapshot.** O administrador cria uma nova versão positiva com duas casas decimais e vigência a partir do relógio do servidor. O check-in captura `DailyRate` no atendimento; mudanças posteriores não recalculam atendimentos nem pedidos.

4. **Idempotência persistida (DT-12).** Check-in, pedido, pagamento e entrega exigem `Idempotency-Key` (1–100 caracteres). A chave é única por ator+operação; o conteúdo canônico é serializado e armazenado como SHA-256 junto do resultado JSON e status. Mesma chave/conteúdo devolve o resultado original; mesma chave/conteúdo diferente devolve `409`. O replay dentro da transação também cobre corrida após conflito de unicidade. Não há TTL nesta fase; a retenção operacional continua pendência documentada.

5. **Documento e projeções por perfil (DT-06/09; RF-10/RNF-02).** O documento é protegido com ASP.NET Data Protection no purpose `Estacionamento.Document.v1` e persistido cifrado. Caixa/admin recebem documento completo no detalhe e a leitura gera `DocumentRead` na auditoria; manobrista recebe máscara. Listagens e fila nunca expõem documento completo nem valores financeiros ao manobrista. Auditoria completa só é exposta ao administrador.

6. **API e erros.** Endpoints versionados em `/api/v1` usam controllers com `[Authorize]` e verificação de caso de uso no servidor. `DomainException` é traduzida por middleware para Problem Details: validação `400`, não autenticado `401` (Identity), proibido `403`, não encontrado `404`, conflito `409` e regra de negócio `422`. Nenhuma resposta/log inclui documento, vistoria ou payload financeiro sensível. A chave de idempotência trafega no header `Idempotency-Key` e é vinculada ao corpo da requisição.

7. **Leitura e polling (DT-08).** Consultas usam projeções EF somente leitura; a SPA faz polling curto de vagas e fila, pausa em aba inativa, cancela requisições sobrepostas e refaz leitura após cada comando. O servidor permanece a única fonte de verdade para disponibilidade, valores e estados.

8. **Frontend (DT-01/02).** A SPA Angular standalone/zoneless ganha rotas protegidas, layout operacional, serviços HTTP por domínio e telas de vagas, check-in, busca/detalhe, fila, cobrança/pagamento, exceções, entrega e administração. Signal Forms valida entradas; mensagens de erro são textuais e acessíveis; nenhuma sessão/dado sensível vai para storage.

9. **Verificação local (DT-10/11).** Testes unitários MSTest cobrem cálculo/estados/saldo; integração MSTest usa SQL Server real para migração, índices filtrados, `rowversion`, autorização/projeções, idempotência e concorrência; Vitest cobre serviços/estados da UI; Playwright cobre as jornadas no edge local. CI e qualquer recurso Azure continuam fora desta change; os resultados locais não comprovam isolamento de ingress, TLS entre sites nem recuperação gerenciada.

## Risks / Trade-offs

- **`rowversion`/índices filtrados dependem de SQL Server real:** testes com SQLite/InMemory não seriam representativos; usar SQL Server Express local, documentando a conexão por variável de ambiente.
- **Idempotência sem TTL cresce indefinidamente:** aceitável no MVP local; retenção e limpeza ficam como pendência operacional explícita, sem apagar lançamentos de negócio.
- **Chave de Data Protection é requisito de recuperação:** perda do key ring impede ler documentos históricos; backup/restore e rotação permanecem gates de produção.
- **Projeções ricas podem vazar dados:** centralizar máscaras/filtros nas queries e cobrir com testes HTTP por perfil; não confiar em ocultação apenas na SPA.
- **Polling e concorrência:** conflitos são respostas explícitas com recarga; medir intervalo/carga no piloto, sem prometer tempo real.
- **Ajustes/estornos sem política comercial final:** implementar apenas lançamentos auditados do administrador; não inventar desconto, estorno automático ou pagamento parcial.
- **Frontend amplo em uma única change:** dividir em features e serviços pequenos, mantendo commits atômicos por fatia verificável.

## Migration Plan

1. Gerar migração EF `AddOperations` sobre a migração Identity existente, com tabelas, FKs, índices únicos/filtrados e `rowversion`; revisar o SQL gerado antes de aplicar.
2. Aplicar em banco SQL Server local isolado via bootstrap de `Development` e via `dotnet ef database update` em banco de teste; atualizar o teste de migração para exigir as tabelas operacionais.
3. Implantar os endpoints e a SPA em processos locais separados; executar testes unitários/integração, Vitest e Playwright com dados sintéticos.
4. Registrar evidências e pendências em `docs/`; nenhuma migração, deploy ou alteração de recurso Azure nesta change. Rollback local é recriar o banco de teste; rollback de release e restore pertencem à Fase 5.
