# Tasks

## 1. Preparação e contratos

- [x] 1.1 Validar `proposal.md`, specs e este design contra PRD/TDD; manter relatórios P1, política comercial pendente e Azure fora do escopo.
- [x] 1.2 Consolidar `EmployeeRoles` no domínio e ajustar referências de Identity, API e testes sem duplicar constantes.
- [x] 1.3 Definir contratos HTTP `/api/v1` para vagas, tarifa, atendimentos, fila, pagamentos, ajustes, autorizações, entrega e auditoria, incluindo `Idempotency-Key` e Problem Details.

## 2. Persistência e migração

- [x] 2.1 Configurar DbSets, FKs, índices únicos/filtrados, `rowversion` e tabela de idempotência no `IdentityDbContext`.
- [x] 2.2 Gerar e revisar a migração `AddOperations`; atualizar o teste de migração para exigir tabelas operacionais e verificá-la em SQL Server real vazio.
- [x] 2.3 Implementar repositório de escrita, consultas de leitura, unidade de trabalho, relógio, gerador de identificador público, store de idempotência e protetor de documento.

## 3. Casos de uso P0

- [x] 3.1 Implementar cadastro administrativo de vagas e tarifa versionada com auditoria e autorização.
- [x] 3.2 Implementar check-in atômico idempotente com snapshot de tarifa, vistoria de entrada, ocupação única e busca/detalhe por perfil.
- [x] 3.3 Implementar pedido de retirada idempotente, conferência documental e cálculo imutável de diárias/total.
- [x] 3.4 Implementar fila, início de busca e transições de estado com responsável e horário.
- [x] 3.5 Implementar pagamento integral idempotente, ajustes e autorizações excepcionais auditadas.
- [x] 3.6 Implementar entrega transacional idempotente com vistoria de saída, exceções válidas, encerramento e liberação única da vaga.

## 4. API e composição

- [x] 4.1 Adicionar `HttpCurrentUser`, middlewares de erro e correlação e registrar serviços/portas no `Program.cs`.
- [x] 4.2 Expor controllers versionados de vagas, tarifas, atendimentos, retirada, cobrança, entrega e auditoria com autorização por perfil.
- [x] 4.3 Garantir que projeções e logs não exponham documento completo, vistoria ou finanças ao manobrista, cobrindo com testes HTTP.

## 5. Frontend operacional

- [x] 5.1 Criar serviços HTTP, modelos, tratamento de erros, guard/estado de sessão e layout autenticado com rotas.
- [x] 5.2 Implementar painéis de vagas, check-in, busca/detalhe, fila e início de busca com polling curto e estados acessíveis.
- [x] 5.3 Implementar cobrança/pagamento, exceções administrativas, entrega e administração de vagas/tarifa.
- [x] 5.4 Adicionar feedback textual de conflito, recarga após comandos, validação com Signal Forms e testes Vitest.

## 6. Testes e evidências locais

- [x] 6.1 Adicionar testes unitários MSTest de cálculo de diárias, estados, saldo, autorização e validação de comandos.
- [x] 6.2 Adicionar testes de integração MSTest com SQL Server real para migração, concorrência de vaga, idempotência, pagamento/entrega duplicados e projeções por perfil.
- [x] 6.3 Executar build/testes .NET, Vitest e Playwright com SQL Server Express local e registrar evidências reproduzíveis em `docs/`.
- [x] 6.4 Atualizar documentação de execução local e pendências, deixando explícito que Azure/P1/política comercial continuam pendentes.

## 7. Entrega

- [x] 7.1 Revisar diffs, executar verificações finais e registrar decisões/limitações no PR.
- [x] 7.2 Criar commits atômicos e abrir PR da feature branch para `main` usando o template do repositório; não publicar nem fazer merge sem revisão/aprovações.
