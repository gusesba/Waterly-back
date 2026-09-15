# Waterly — Patch notes

Este arquivo registra o conteúdo funcional dos commits do backend e do frontend. Os nomes e hashes abaixo correspondem ao histórico real dos repositórios `backend` e `water`.

## Etapa 3F — Fechamento diário automático

Data: 2026-09-15

### Backend — `não commitado`

- Worker periódico de fechamento diário com intervalo e tamanho de lote configuráveis.
- Processamento pelo dia local de cada perfil, limitado a dias já encerrados.
- Checkpoint persistente por usuário para retomada segura depois de reinícios e falhas.
- Escopo isolado por usuário, logs estruturados e reaproveitamento das regras autoritativas de streak e recompensas.
- Migration `AddDailyClosureCheckpoint` e testes de avanço monotônico, fuso, checkpoint e reexecução idempotente.

### Frontend — `não commitado`

- Integração do `AppState` nativo com o foco do TanStack Query.
- Streak, conquistas, progressão e loadout desatualizados são consultados novamente quando o aplicativo volta ao primeiro plano.

## Etapa 3E — Personagem e loadout inicial

Data: 2026-09-15

### Backend — `20dee7a` (`3e`)

- Catálogo de auras ligado às conquistas, posse permanente e loadout persistido por usuário.
- Concessão automática e idempotente das auras Natural, Oceano, Pôr do sol e Estelar.
- Endpoints autenticados `GET /api/v1/cosmetics`, `GET /api/v1/profile/loadout` e `PUT /api/v1/profile/loadout`.
- Equipamento restrito a itens possuídos, sem consumo de Drops ou Prestige.
- Migration `AddCharacterCosmetics` e testes unitários, de autenticação, bloqueio, concessão, idempotência e saldo.

### Frontend — `d843bf5` (`3e`)

- Tela Meu personagem com prévia, catálogo de auras, estados bloqueado/disponível/em uso e troca persistida.
- Aura selecionada aplicada ao mascote compartilhado na tela Hoje e na prévia do perfil.
- Textos completos em português e inglês e teste da regra de seleção.

## Etapa 3D — Perfil público básico

Data: 2026-09-15

### Backend — `b6a126e` (`3d`)

- Perfil público isolado dos dados físicos do onboarding.
- Username normalizado e único, nome de exibição e biografia opcional.
- Endpoints autenticados `GET` e `PUT /api/v1/profile`.
- Validação de campos, conflito de username e migration de `PublicProfiles`.
- Testes de autenticação e de ausência de idade, peso e altura no contrato público.

### Frontend — `ee35037` (`3d`)

- Tela Meu perfil para criar e editar username, nome e biografia.
- Validação e normalização local do username, tratamento de conflito e aviso de privacidade.
- Acesso pela área da conta na tela Hoje e textos em português e inglês.


## Etapa 3C — Fundação de Drops e Prestige

Data: 2026-09-15

### Backend — `ef0914a` (`3c`)

- Ledgers independentes, imutáveis e append-only para Drops e Prestige.
- Saldos calculados pela soma dos lançamentos, sem estado de saldo mutável.
- Recompensas versionadas por conquista: primeira meta, streak de 3 dias e streak de 7 dias.
- Chaves idempotentes únicas por usuário impedem concessões duplicadas.
- Desbloqueio e recompensas novas persistidos atomicamente, com retrocompatibilidade para conquistas já desbloqueadas.
- Endpoints autenticados `GET /api/v1/wallet` e `GET /api/v1/prestige` com saldo e histórico recente.
- Migration dos dois ledgers e dos valores de recompensa do catálogo.
- Testes integrados de saldo, histórico, permanência, idempotência e autenticação.

### Frontend — `f597930` (`3c`)

- Tela de progressão com saldos separados de Drops e Prestige.
- Histórico recente combinado e ordenado por data.
- Explicação da diferença entre moeda de personalização e progressão real.
- Acesso pela tela de conquistas e invalidação após hidratação ou sincronização offline.
- Textos disponíveis em português e inglês e teste da composição do histórico.

## Etapa 3B — Conquistas simples

Data: 2026-09-15

### Backend — `b096733` (`3b`)

- Catálogo versionado com conquistas de primeira meta e sequências de 3 e 7 dias.
- Desbloqueios permanentes e idempotentes, com unicidade por usuário e conquista.
- Avaliação baseada nos agregados diários e no maior streak autoritativo.
- Endpoint autenticado `GET /api/v1/achievements` com progresso, requisito e data de desbloqueio.
- Migration para `AchievementDefinitions` e `UserAchievements` e carga do catálogo inicial.
- Testes unitários dos critérios e testes integrados de autenticação, idempotência e permanência após exclusão.

### Frontend — `8803669` (`3b`)

- Tela de conquistas com progresso, estados bloqueado/desbloqueado e data da conquista.
- Card de streak transformado em acesso para a nova tela.
- Conquistas desbloqueadas aparecem primeiro; as restantes são ordenadas por proximidade da conclusão.
- Invalidação após alterações online e sincronização da fila offline.
- Textos e descrições disponíveis em português e inglês.

## Etapa 3A — Agregado diário e sequência de hidratação

Data: 2026-09-15

### Backend — `12447ce` (`3a`)

- Projeções persistidas e reconstruíveis de hidratação diária e sequência do usuário.
- Cálculo versionado que preserva a sequência enquanto o dia atual ainda está aberto e limita a conclusão a 100% da meta.
- Reprocessamento após criação, edição ou exclusão a partir dos registros e metas que continuam como fontes de verdade.
- Endpoint autenticado `GET /api/v1/habits/streak` com sequência atual, recorde, conclusão de hoje e último dia concluído.
- Migration para `DailyHydrations` e `UserStreaks`, com unicidade por usuário e data.
- ADR das regras de fuso, mudança de fuso, meta histórica e reprocessamento idempotente.
- Testes unitários do cálculo e teste integrado do ciclo de criação e exclusão.

### Frontend — `a175896` (`3a`)

- Card compacto na tela Hoje com sequência atual, recorde e estado da meta do dia.
- Invalidação da sequência após alterações online e depois da sincronização da fila offline.
- Textos do hábito disponíveis em português e inglês.

## Etapa 2E — Feedback visual e conclusão da beta individual

Data: 2026-09-15

### Backend — `e6d551e` (`2e`)

- Sem alterações funcionais nesta etapa.

### Frontend — `87c2c89` (`2e`)

- Barra e percentual de progresso animados após mudanças na hidratação.
- Preferência de redução de movimento respeitada.
- Mascote integrado aos estados sem registros, progresso em andamento e meta concluída.
- Feedback háptico para criação, edição, exclusão e conclusão da meta.
- Celebração exibida uma única vez ao cruzar a meta diária durante a sessão.
- Regras puras e testes para estado do mascote e elegibilidade da celebração.

## Etapa 2D — Lembretes locais

Data: 2026-09-15

### Backend — `f2e4eb4` (`2d`)

- Sem alterações funcionais nesta etapa.

### Frontend — `0e9777c` (`2d`)

- Tela de configuração com ativação e até três horários diários.
- Preferências versionadas e persistidas localmente por conta.
- Solicitação de permissão somente após ação do usuário.
- Canal Android dedicado e agendamentos locais diários.
- Reagendamento e cancelamento restritos aos identificadores criados pelo Waterly.
- Tratamento de permissão negada, plataforma web e abertura das configurações do dispositivo.
- Testes de horários, persistência, permissões, agendamento e cancelamento.


## Etapa 2C — Sincronização offline robusta

Data: 2026-09-15

### Backend — `1b88aee` (`2c`)

- Proteção contra concorrência no processamento idempotente de edições e exclusões.
- Recuperação após colisão do índice único de `ClientOperationId`, retornando o estado atual em vez de erro interno quando a mesma operação já foi processada.

### Frontend — `b21d4e8` (`2c`)

- Fila offline migrada para uma estrutura versionada com estados `pending` e `failed`.
- Compactação de operações pendentes: criação seguida de edição é combinada; criação seguida de exclusão é removida; múltiplas edições mantêm apenas a mais recente.
- Retentativas automáticas com backoff exponencial, jitter e suporte ao cabeçalho HTTP `Retry-After`.
- Erros permanentes deixam de ser reenviados automaticamente e podem ser reenviados ou descartados pelo usuário.
- Sincronização protegida contra execuções simultâneas e acionada ao reconectar ou retornar o app ao primeiro plano.
- Estado de sincronização exibido globalmente e em cada registro do dia.
- Testes unitários adicionados para compactação e recuperação de operações com falha.

## Etapa 2B — Bebidas, histórico e primeira versão offline

Data: 2026-09-14

### Backend — `15ace9d` (`etapa 2b`)

- Catálogo inicial de bebidas: água, água com gás, café e chá.
- Fatores de hidratação configurados em 100% para água e água com gás, 80% para café e 90% para chá.
- Volume consumido e equivalente de hidratação armazenados separadamente no registro.
- Bebidas ordenadas pelo volume consumido pelo usuário.
- Sugestões de registro rápido aprendidas e separadas por bebida selecionada.
- Endpoints de edição, exclusão, histórico, bebidas e sugestões.
- Recibos de operações para idempotência de edição e exclusão.
- Compatibilidade entre o identificador local e o identificador criado pelo servidor.
- Migrations do catálogo, fatores de hidratação e recibos de operações.
- Testes de fatores, sugestões por bebida e repetição idempotente das operações.

### Frontend — `8a7834a` (`etapa 2b`)

- Seletor de bebidas ordenado pelo consumo.
- Três sugestões de registro rápido específicas para a bebida selecionada.
- Exibição da porcentagem de água de cada bebida.
- Registro do dia e histórico mostrando volume da bebida e equivalente em água.
- Edição e exclusão de registros.
- Tela de histórico de hidratação.
- Atualizações otimistas para criação, edição e exclusão.
- Primeira versão da fila offline persistida por conta e sincronização ao recuperar a conexão.
- Snapshot local do resumo do dia e restauração da sessão do usuário quando a API está indisponível.

## Etapa 1 — Primeiro corte do tracker de hidratação

Data: 2026-09-14

### Backend — `7990f92` (`stage 1`)

- Entidade e persistência dos registros de hidratação.
- Meta diária versionada integrada ao resumo do dia.
- Endpoints para consultar o dia e adicionar um registro.
- Idempotência de criação por identificador gerado pelo cliente.
- Cálculo do dia respeitando o fuso horário do perfil.
- Health check do banco, CI e validação de migrations.
- Testes unitários e de integração do fluxo inicial de hidratação.

### Frontend — `f30d8d1` (`stage 1`)

- Tela principal conectada à API com progresso diário e registros rápidos.
- Quantidade personalizada de água.
- Integração com TanStack Query.
- Separação entre rotas públicas, autenticação e área protegida.
- Tela de conta e logout.
- Melhorias na autenticação, restauração do onboarding e armazenamento da sessão.
- CI para lint e verificação de tipos.

## Autenticação e onboarding persistente

Data: 2026-09-02

### Backend — `71a17bf` (`auth e onboarding`)

- Solution ASP.NET Core/.NET 10 com PostgreSQL e Entity Framework Core.
- ASP.NET Core Identity com cadastro, login e renovação de sessão.
- Perfil do usuário, objetivos e meta diária persistidos no banco.
- Endpoint idempotente de conclusão do onboarding.
- OpenAPI, Problem Details e estrutura inicial de testes.
- Ambiente local com Docker Compose.

### Frontend — `7f2a9f2` (`auth`)

- Cadastro e login integrados à API.
- Armazenamento seguro de access token e refresh token em ambiente nativo.
- Restauração e renovação da sessão.
- Sincronização do onboarding com o backend.
- Redirecionamento conforme autenticação e conclusão do onboarding.
- Textos de autenticação em português e inglês.

## Estrutura inicial do frontend

Data: 2026-09-01

### Frontend

- `bde43a7` (`Initial commit`): projeto base Expo e React Native.
- `40e7723` (`telas iniciais`): primeira proposta visual do onboarding e inclusão dos assets do mascote.
- `1fb4f95` (`estrutura de arquivos`): onboarding dividido por rotas, telas e componentes; provider e armazenamento local do rascunho; localização em português e inglês.

## Manutenção

Ao concluir uma etapa que será commitada:

1. adicionar uma seção no topo deste arquivo;
2. registrar separadamente backend e frontend;
3. informar o hash e a mensagem de cada commit assim que existirem;
4. enquanto o commit ainda não existir, usar `não commitado` e substituir pelo hash posteriormente;
5. descrever mudanças funcionais, migrations, contratos e testes relevantes, evitando uma simples lista de arquivos.
