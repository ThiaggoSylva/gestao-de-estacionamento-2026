# Instruções para agentes

## Geral

- Sempre utilizar o sistema de questões do ambiente (harness) ao invés de fazer perguntas na resposta.

## Fluxo de trabalho

Para novas funcionalidades e alterações de comportamento, seguir a ordem **PRD → TDD → SDD → Pull Request**, sem pular etapas:

1. **PRD (Product Requirements Document):** definir e validar problema, escopo, requisitos e critérios de aceite em `docs/prd.md` antes do desenho técnico.
2. **TDD (Technical Design Document):** registrar em `docs/tdd.md` as decisões de arquitetura, contratos, dados, segurança, testes e o roadmap de implementação, alinhados ao PRD, antes de especificar a mudança.
3. **SDD (Spec-Driven Development):** detalhar a mudança em especificações e tarefas rastreáveis ao PRD e ao TDD; revisar e validar essas especificações antes de implementar. Implementar e testar seguindo as tarefas aprovadas, mantendo documentação e especificações coerentes com eventuais decisões posteriores.
4. **Pull Request:** somente após implementação e verificações aplicáveis, abrir PR com o template de `.github/pull_request_template.md`, relacionando requisitos, decisões e especificações, descrevendo testes executados e pendências.

Em mudanças com documentos existentes, reutilizar e atualizar os artefatos pertinentes em vez de recriá-los. Se uma etapa não se aplicar (por exemplo, correção puramente editorial), registrar a justificativa no PR; não tratar pendências de validação ou aprovação como concluídas.
