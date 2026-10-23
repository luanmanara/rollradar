# Mudanças do RollRadar

Registro de todas as mudanças do projeto. Cada mudança é um card com código sequencial (`RR-001`, `RR-002`…).

**Tipos:** Requisição (algo novo ou melhorado para o usuário, branch `feature/`) · Problema (bug sem urgência,
`fix/`) · Incidente (site publicado quebrado, `hotfix/`) · Tarefa (técnico, infra, dados, pesquisa, `chore/`).

**Status:** Aberta → Em andamento → Concluída (ou Descartada). Número nunca é reaproveitado.

**Convenções:** branch `feature/RR-013-diario-de-treino` · commit `RR-013: adiciona tabela de treinos` ·
PR `RR-013 · Diário de treino`. Mudança grande vira cards filhos com "Parte de: RR-0xx". Ideias ficam em
`ideias-futuras.md` e só ganham número quando forem feitas.

## Índice

| Código | Data | Tipo | Mudança | Status |
| --- | --- | --- | --- | --- |
| RR-016 | 23/10/2026 | Requisição | Contas de usuário (fase 0) | Aberta |
| RR-015 | 23/10/2026 | Tarefa | Controle de mudanças | Em andamento |
| RR-014 | 23/10/2026 | Requisição | Mapa do jogo | Aberta |
| RR-013 | 23/10/2026 | Requisição | Diário de treino | Em andamento |
| RR-012 | 19/10/2026 | Requisição | Imagens nas categorias de técnicas | Concluída |
| RR-011 | 19 a 21/10/2026 | Tarefa | Publicar o site (hospedagem provisória) | Concluída |
| RR-010 | 19/10/2026 | Requisição | Logo do RollRadar aplicado no site | Concluída |
| RR-009 | 19 a 20/10/2026 | Requisição | Busca por atleta e catálogo de atletas | Concluída |
| RR-008 | 19 a 20/10/2026 | Requisição | Novo front-end (fases 1 a 3 e ajuste do voto) | Concluída |
| RR-007 | 19/10/2026 | Tarefa | Token bearer na API | Concluída |
| RR-006 | 19/10/2026 | Tarefa | Coletor de legendas em .NET gravando direto no banco | Concluída |
| RR-005 | 18/10/2026 | Requisição | Curadoria manual das menções | Concluída |
| RR-004 | 18/10/2026 | Tarefa | Banco de dados PostgreSQL das lutas | Concluída |
| RR-003 | 16 a 19/10/2026 | Tarefa | Contornar o bloqueio de legendas do YouTube | Concluída |
| RR-002 | 16/10/2026 | Tarefa | Atalho para iniciar o projeto | Concluída |
| RR-001 | 16/10/2026 | Tarefa | Renomear o projeto para RollRadar | Concluída |

## Cards

## RR-016 · Contas de usuário (fase 0)
- Tipo: Requisição
- Status: Aberta (23/10/2026)
- Objetivo: cadastro e login de usuários, base para o Diário de treino (RR-013), o Mapa do jogo (RR-014) e o progresso por técnica.
- Branch: feature/RR-016-contas-de-usuario

## RR-015 · Controle de mudanças
- Tipo: Tarefa
- Status: Em andamento (aberta em 23/10/2026)
- Objetivo: padronizar o registro de mudanças com cards RR-xxx, tipos, status e convenção de branch/commit/PR.
- Notas: cards RR-001 a RR-012 registrados retroativamente.

## RR-014 · Mapa do jogo
- Tipo: Requisição
- Status: Aberta (23/10/2026)
- Objetivo: mapa visual do jogo do atleta (posições, técnicas e transições que ele usa).
- Depende de: RR-016 (contas de usuário)

## RR-013 · Diário de treino
- Tipo: Requisição
- Status: Em andamento (aberta em 23/10/2026)
- Objetivo: o atleta registra cada treino e acompanha o histórico.
- Depende de: RR-016 (contas de usuário)
- Branch: feature/RR-013-diario-de-treino

## RR-012 · Imagens nas categorias de técnicas
- Tipo: Requisição · Status: Concluída (19/10/2026) · Retroativo
- Resumo: ilustração em cada categoria do navegador de técnicas (guardas, passagens, raspagens…).

## RR-011 · Publicar o site
- Tipo: Tarefa · Status: Concluída (21/10/2026) · Retroativo
- Resumo: pesquisa de servidores e primeira publicação. A hospedagem atual é provisória.

## RR-010 · Logo do RollRadar
- Tipo: Requisição · Status: Concluída (19/10/2026) · Retroativo
- Resumo: logo A3 (nó da faixa com sinal de radar), favicon e ícone no cabeçalho.

## RR-009 · Busca por atleta e catálogo de atletas
- Tipo: Requisição · Status: Concluída (20/10/2026) · Retroativo
- Resumo: busca de vídeos por atleta, com catálogo de atletas no banco e sugestões de nome.

## RR-008 · Novo front-end
- Tipo: Requisição · Status: Concluída (20/10/2026) · Retroativo
- Resumo: redesenho em três fases (home, busca e player; categorias e página da técnica; lutas e atleta) e ajuste do voto no player.

## RR-007 · Token bearer na API
- Tipo: Tarefa · Status: Concluída (19/10/2026) · Retroativo
- Resumo: proteção das rotas de escrita da API com token.

## RR-006 · Coletor de legendas em .NET
- Tipo: Tarefa · Status: Concluída (19/10/2026) · Retroativo
- Resumo: RollRadar.Coletor substitui os scripts Python e grava legendas e momentos direto no banco, com fila e pausas.

## RR-005 · Curadoria manual das menções
- Tipo: Requisição · Status: Concluída (18/10/2026) · Retroativo
- Resumo: usuário marca cada momento como bom ou ruim; votos guardados para filtrar resultados.

## RR-004 · Banco de dados PostgreSQL
- Tipo: Tarefa · Status: Concluída (18/10/2026) · Retroativo
- Resumo: PostgreSQL no Docker + EF Core no lugar dos arquivos JSON carregados na inicialização.

## RR-003 · Contornar o bloqueio de legendas do YouTube
- Tipo: Tarefa · Status: Concluída (19/10/2026) · Retroativo
- Resumo: pesquisa de alternativas e download de legendas via yt-dlp, mantendo as sessões lentas.

## RR-002 · Atalho para iniciar o projeto
- Tipo: Tarefa · Status: Concluída (16/10/2026) · Retroativo
- Resumo: atalho para subir API e front-end de uma vez.

## RR-001 · Renomear o projeto para RollRadar
- Tipo: Tarefa · Status: Concluída (16/10/2026) · Retroativo
- Resumo: Jiujitsu Finder vira RollRadar (projetos .NET, solução, front-end e README).
