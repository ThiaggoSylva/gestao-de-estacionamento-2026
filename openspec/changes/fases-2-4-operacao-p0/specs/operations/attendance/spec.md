# Spec Delta

## Purpose

Registrar a preparação da unidade e a entrada de veículos com identificação única, vaga, tarifa preservada, vistoria textual, busca por perfil e trilha auditável.

## ADDED Requirements

### Requirement: Cadastro administrativo de vagas
O sistema SHALL permitir que somente o administrador cadastre vagas com código único e SHALL impedir código duplicado ou vazio.

#### Scenario: Vaga cadastrada
- **WHEN** o administrador cadastra uma vaga com código ainda não usado
- **THEN** o sistema persiste a vaga e a apresenta como livre

#### Scenario: Código de vaga repetido
- **WHEN** o administrador tenta cadastrar código de vaga já existente
- **THEN** o sistema rejeita a operação como conflito sem criar duplicata

#### Scenario: Perfil sem permissão
- **WHEN** manobrista ou caixa tenta cadastrar vaga
- **THEN** o sistema nega a operação como acesso proibido

### Requirement: Tarifa versionada com vigência
O sistema SHALL permitir que somente o administrador defina uma tarifa diária positiva e SHALL preservar cada versão com sua vigência, sem alterar valores já capturados em atendimentos.

#### Scenario: Nova tarifa passa a valer
- **WHEN** o administrador define uma tarifa positiva
- **THEN** o sistema a torna a tarifa vigente para novas entradas e preserva as versões anteriores

#### Scenario: Tarifa inválida
- **WHEN** o administrador tenta definir tarifa zero, negativa ou sem precisão válida
- **THEN** o sistema rejeita a operação sem alterar a tarifa vigente

#### Scenario: Alteração posterior não retroage
- **WHEN** a tarifa é alterada depois de uma entrada concluída
- **THEN** o valor capturado naquele atendimento permanece inalterado

### Requirement: Check-in atômico e completo
O sistema SHALL concluir a entrada somente com placa, modelo, nome e documento do titular, vistoria textual, vaga livre e tarifa vigente, registrando identificador público único, operador e horário do servidor na mesma transação.

#### Scenario: Entrada válida
- **WHEN** caixa ou administrador informa todos os dados obrigatórios, escolhe vaga livre e existe tarifa vigente
- **THEN** o sistema cria o atendimento ativo, ocupa a vaga, captura a tarifa, grava a vistoria de entrada e registra auditoria

#### Scenario: Dados obrigatórios ausentes
- **WHEN** qualquer dado obrigatório, vistoria ou tarifa vigente estiver ausente
- **THEN** o sistema não conclui a entrada nem ocupa vaga

#### Scenario: Vaga já ocupada
- **WHEN** duas entradas concorrentes tentam ocupar a mesma vaga livre
- **THEN** no máximo uma é concluída e a outra recebe conflito recarregável, sem dupla ocupação

#### Scenario: Repetição idempotente
- **WHEN** o mesmo check-in é reenviado com a mesma chave e o mesmo conteúdo
- **THEN** o sistema retorna o resultado original sem criar outro atendimento nem ocupar outra vaga

#### Scenario: Perfil sem permissão
- **WHEN** manobrista tenta concluir check-in
- **THEN** o sistema nega a operação como acesso proibido

### Requirement: Busca e consulta de atendimentos
O sistema SHALL permitir busca por placa ou identificador público, distinguindo entradas diferentes do mesmo veículo, e SHALL projetar os dados conforme o perfil autenticado.

#### Scenario: Busca por placa
- **WHEN** um funcionário consulta uma placa que possui histórico
- **THEN** o sistema retorna as entradas correspondentes com identificador, estado, vaga e horários

#### Scenario: Busca por identificador
- **WHEN** um funcionário consulta um identificador público existente
- **THEN** o sistema retorna o atendimento correspondente sem confundi-lo com outro veículo

#### Scenario: Dados financeiros protegidos do manobrista
- **WHEN** manobrista consulta detalhe ou busca de atendimento
- **THEN** o sistema não retorna documentos, tarifa, cobrança, pagamentos ou ajustes

### Requirement: Proteção do documento do titular
O sistema SHALL armazenar o documento protegido em repouso e SHALL exibir o valor completo somente a caixa ou administrador, registrando auditoria da leitura completa.

#### Scenario: Leitura autorizada
- **WHEN** caixa ou administrador abre o detalhe de um atendimento
- **THEN** o sistema retorna o documento necessário à conferência e registra a leitura na auditoria

#### Scenario: Leitura mascarada
- **WHEN** manobrista consulta o mesmo atendimento
- **THEN** o sistema retorna apenas representação mascarada do documento

### Requirement: Trilha de auditoria operacional
O sistema SHALL registrar, para cada operação crítica, atendimento relacionado, ator, ação e horário do servidor em lançamentos não editáveis por operações normais.

#### Scenario: Auditoria da entrada
- **WHEN** um check-in é concluído
- **THEN** o sistema grava a criação do atendimento, a ocupação da vaga e a vistoria com o operador e horário

#### Scenario: Auditoria de configuração
- **WHEN** o administrador cadastra vaga ou tarifa
- **THEN** o sistema registra a ação e o responsável sem expor documento ou vistoria
