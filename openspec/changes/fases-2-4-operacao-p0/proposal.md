# Proposal

## Why

O PRD e o TDD aprovados exigem a operação P0 completa (entrada, vaga, retirada, cobrança, pagamento, exceções e entrega), mas a Fase 1 entregou somente a fundação de sessão e infraestrutura local. As Fases 2–4 são a próxima fatia verificável e podem ser concluídas localmente sem antecipar relatórios P1, aprovações de negócio pendentes ou publicação Azure.

## What Changes

- Implementar o modelo operacional persistido de atendimentos, vagas, ocupações, tarifas versionadas, pedidos de retirada, cobranças, pagamentos, ajustes, autorizações excepcionais e trilha de auditoria, com migrações EF Core e invariantes no SQL Server.
- Expor endpoints versionados `/api/v1` para cadastro de vagas e tarifa, check-in atômico com snapshot de tarifa e vistoria textual, busca por placa/identificador, fila de retirada, pedido com conferência documental, cálculo de diárias, pagamento integral, ajustes e autorizações de administrador, entrega e liberação de vaga.
- Aplicar a matriz de autorização no servidor: manobrista consulta e inicia busca sem dados financeiros/documento completo; caixa opera entrada, retirada, pagamento e entrega; administrador configura, autoriza exceções, ajusta valores e consulta tudo.
- Implementar idempotência persistida nos comandos críticos e conflitos explícitos (`409`) para dupla ocupação, pedido repetido, pagamento repetido e entrega repetida.
- Entregar a SPA autenticada com painéis de vagas, check-in, busca, fila, cobrança/pagamento, exceções, entrega e administração, com polling curto, estados textuais acessíveis e sem armazenar sessão no navegador.
- Cobrir as jornadas com testes unitários de regras, integração MSTest contra SQL Server real, Vitest e Playwright local.
- Excluir desta change: relatórios P1 (RF-12), provisionamento/publicação Azure, IaC/CD, integrações de pagamento, fotos, múltiplas unidades e qualquer decisão comercial ainda pendente de desconto/estorno. Ajustes serão lançamentos auditados do administrador, sem inventar política comercial.

## Capabilities

### New Capabilities

- `operations/attendance`: cadastro de vagas e tarifa versionada, check-in atômico, snapshot de tarifa, vistoria textual, busca e consulta de atendimentos com projeções por perfil e auditoria.
- `operations/withdrawal`: pedido de retirada com conferência documental, fila e estados, cálculo imutável de diárias/total, início de busca e bloqueios de duplicidade.
- `operations/settlement`: pagamento integral manual, ajustes e autorizações excepcionais auditadas, vistoria de saída, entrega transacional e liberação única da vaga.

### Modified Capabilities

Nenhuma. Os requisitos existentes de sessão, edge e prontidão local permanecem válidos; as novas capacidades reutilizam essa base.

## Impact

Novas entidades e migrações no banco SQL Server existente, endpoints `/api/v1` de operação, serviços de aplicação e repositórios de infraestrutura, telas e serviços Angular, testes MSTest/Vitest/Playwright e documentação de evidências. Não há alteração de stack, autenticação ou topologia. Permanecem bloqueados relatórios P1, política comercial de ajustes/estornos, privacidade, backup/RPO/RTO e homologação/publicação Azure.
