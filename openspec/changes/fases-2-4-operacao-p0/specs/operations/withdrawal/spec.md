# Spec Delta

## Purpose

Registrar o pedido de retirada com conferência documental, fixar a cobrança por diárias iniciadas e acompanhar a fila operacional sem duplicidade.

## ADDED Requirements

### Requirement: Pedido de retirada com conferência presencial
O sistema SHALL registrar pedido de retirada somente para atendimento ativo, com resultado da conferência presencial do documento, operador e horário do servidor, e SHALL impedir mais de um pedido no ciclo do MVP.

#### Scenario: Pedido válido
- **WHEN** caixa ou administrador registra pedido para atendimento ativo com documento conferido
- **THEN** o sistema cria o pedido aguardando, fixa a cobrança e registra operador e horário

#### Scenario: Atendimento inexistente ou não ativo
- **WHEN** o pedido é solicitado para atendimento inexistente, já entregue ou já com pedido
- **THEN** o sistema rejeita a operação como conflito ou recurso não revelável

#### Scenario: Divergência documental
- **WHEN** a conferência presencial indica divergência entre documento apresentado e dados registrados
- **THEN** o sistema registra a divergência no pedido e bloqueia a entrega normal até autorização de administrador

#### Scenario: Perfil sem permissão
- **WHEN** manobrista tenta registrar pedido ou conferência documental
- **THEN** o sistema nega a operação como acesso proibido

#### Scenario: Pedido repetido com a mesma chave
- **WHEN** o mesmo pedido é reenviado com idêntica chave e conteúdo
- **THEN** o sistema retorna o pedido original sem criar segunda cobrança

### Requirement: Cálculo imutável de diárias e total
O sistema SHALL calcular no instante do pedido a quantidade de diárias como o maior valor entre um e o teto de períodos iniciados de 24 horas desde a entrada, e SHALL fixar tempo, tarifa capturada e total sem alteração posterior.

#### Scenario: Até 24 horas
- **WHEN** o pedido ocorre em até 24 horas após a entrada
- **THEN** o sistema fixa uma diária e total igual à tarifa capturada

#### Scenario: Exatamente 24 horas
- **WHEN** o pedido ocorre exatamente 24 horas após a entrada
- **THEN** o sistema fixa uma diária

#### Scenario: Mais de 24 até 48 horas
- **WHEN** o pedido ocorre 24 horas e um segundo após a entrada e antes de 48 horas
- **THEN** o sistema fixa duas diárias e total igual a duas vezes a tarifa capturada

#### Scenario: Nova tarifa não recalcula pedido
- **WHEN** a tarifa vigente muda após o pedido
- **THEN** o tempo, as diárias e o total daquele pedido permanecem inalterados

### Requirement: Fila com estados e responsáveis
O sistema SHALL apresentar pedidos como aguardando, em busca ou entregue, registrar transições com responsável e horário, e SHALL impedir entrega sem pedido ativo.

#### Scenario: Pedido entra na fila
- **WHEN** um pedido é criado
- **THEN** ele aparece como aguardando com atendimento, vaga e horário do pedido

#### Scenario: Início da busca
- **WHEN** manobrista, caixa ou administrador inicia a busca de pedido aguardando
- **THEN** o sistema o move para em busca registrando responsável e horário

#### Scenario: Transição inválida
- **WHEN** se tenta iniciar busca de pedido já em busca ou entregue
- **THEN** o sistema rejeita a transição como conflito

#### Scenario: Fila por perfil
- **WHEN** manobrista consulta a fila
- **THEN** o sistema retorna estados, vaga, placa e identificador sem valores financeiros ou documento completo

#### Scenario: Entrega sem pedido
- **WHEN** se tenta entregar atendimento que nunca teve pedido ativo
- **THEN** o sistema rejeita a entrega como regra de domínio não satisfeita

### Requirement: Configuração financeira restrita
O sistema SHALL restringir pedido, conferência e visualização de valores a caixa e administrador, mantendo manobrista apto apenas à consulta operacional e início de busca.

#### Scenario: Valores visíveis ao caixa
- **WHEN** caixa consulta um pedido
- **THEN** o sistema retorna diárias, total fixado e estado de quitação pertinente à operação

#### Scenario: Valores ocultos ao manobrista
- **WHEN** manobrista consulta a fila ou o atendimento
- **THEN** o sistema não retorna cobranças, pagamentos, ajustes ou documento completo
