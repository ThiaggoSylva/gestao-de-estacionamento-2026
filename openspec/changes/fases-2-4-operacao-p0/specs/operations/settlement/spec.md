# Spec Delta

## Purpose

Registrar pagamento integral e exceções auditadas, concluir a entrega com vistoria de saída e liberar a vaga de forma transacional e única.

## ADDED Requirements

### Requirement: Pagamento integral manual
O sistema SHALL registrar pagamento apenas na forma integral do saldo exigível, com valor, forma dinheiro/cartão/Pix, operador e horário do servidor, sem processar liquidação externa.

#### Scenario: Quitação integral
- **WHEN** caixa ou administrador registra pagamento igual ao saldo exigível
- **THEN** o sistema quita o atendimento, registra o lançamento e atualiza valor pago e saldo

#### Scenario: Pagamento parcial
- **WHEN** o valor informado é menor que o saldo exigível
- **THEN** o sistema rejeita o lançamento sem alterar a quitação

#### Scenario: Forma ou valor inválido
- **WHEN** a forma não é dinheiro, cartão ou Pix, ou o valor não é positivo
- **THEN** o sistema rejeita a operação como dados inválidos

#### Scenario: Repetição idempotente
- **WHEN** o mesmo pagamento é reenviado com idêntica chave e conteúdo
- **THEN** o sistema retorna o lançamento original sem duplicar valor recebido

#### Scenario: Perfil sem permissão
- **WHEN** manobrista tenta registrar pagamento
- **THEN** o sistema nega a operação como acesso proibido

### Requirement: Ajustes financeiros append-only
O sistema SHALL permitir que somente o administrador registre ajuste com justificativa e SHALL preservar todos os lançamentos originais, recalculando saldo sem apagar ou editar pagamentos anteriores.

#### Scenario: Ajuste autorizado
- **WHEN** o administrador registra ajuste com valor e justificativa
- **THEN** o sistema adiciona o lançamento, recalcula o saldo e mantém o histórico visível

#### Scenario: Ajuste sem justificativa
- **WHEN** o administrador tenta ajustar sem motivo informado
- **THEN** o sistema rejeita a operação sem alterar valores

#### Scenario: Não administrador ajusta
- **WHEN** manobrista ou caixa tenta registrar ajuste
- **THEN** o sistema nega a operação como acesso proibido

### Requirement: Autorização excepcional por atendimento
O sistema SHALL permitir que somente o administrador autorize, com tipo, justificativa, autor e horário, divergência documental ou entrega com saldo pendente, sem substituir pagamento ou conferência.

#### Scenario: Autorização registrada
- **WHEN** o administrador autoriza exceção para atendimento específico
- **THEN** o sistema persiste a autorização vinculada ao atendimento e ao tipo de exceção

#### Scenario: Exceção sem motivo
- **WHEN** a autorização é solicitada sem justificativa
- **THEN** o sistema rejeita a operação

#### Scenario: Autorização de outro atendimento
- **WHEN** a entrega tenta usar autorização emitida para outro atendimento
- **THEN** o sistema não a considera válida

### Requirement: Entrega transacional com liberação única
O sistema SHALL concluir a entrega somente com vistoria textual de saída, conferência documental aprovada ou autorização de divergência e quitação ou autorização de saldo pendente, gravando entrega, encerramento e liberação da vaga na mesma transação.

#### Scenario: Entrega normal
- **WHEN** o atendimento possui pedido, vistoria de saída, conferência aprovada e quitação integral
- **THEN** o sistema registra entrega e responsável, encerra o atendimento e libera a vaga

#### Scenario: Vistoria de saída ausente
- **WHEN** a entrega é solicitada sem vistoria textual de saída
- **THEN** o sistema rejeita a operação e mantém a vaga ocupada

#### Scenario: Divergência documental sem autorização
- **WHEN** o pedido registra divergência documental e não existe autorização válida
- **THEN** o sistema bloqueia a entrega normal

#### Scenario: Saldo pendente sem autorização
- **WHEN** o saldo exigível não foi quitado e não existe autorização de saldo pendente
- **THEN** o sistema bloqueia a entrega

#### Scenario: Entrega com autorizações válidas
- **WHEN** existem as autorizações específicas necessárias e a vistoria de saída
- **THEN** o sistema conclui a entrega excepcional e preserva o saldo pendente no relatório futuro

#### Scenario: Repetição concorrente da entrega
- **WHEN** a entrega é repetida ou executada concorrentemente
- **THEN** o sistema libera a vaga no máximo uma vez e não duplica a entrega

### Requirement: Histórico preservado após a entrega
O sistema SHALL manter consultáveis atendimentos concluídos, cobrança original, pagamentos, ajustes, autorizações e auditoria, sem alteração silenciosa de valores ou eventos passados.

#### Scenario: Consulta após entrega
- **WHEN** caixa ou administrador consulta atendimento entregue
- **THEN** o sistema retorna o histórico e os valores originais preservados

#### Scenario: Correção após encerramento
- **WHEN** uma correção financeira é necessária após a entrega
- **THEN** ela só é aceita como novo ajuste auditado do administrador, sem editar lançamentos anteriores
