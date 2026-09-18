# Waterly — Patch notes

Este arquivo registra o conteúdo funcional dos commits do backend e do frontend. Os nomes e hashes abaixo correspondem ao histórico real dos repositórios `backend` e `water`.

## Etapa 6A — Hardening de rankings e proteção contra abuso

Data: 2026-09-18

### Backend — `não commitado`

- Atualização da pontuação dos concursos ativos após entrada, edição ou exclusão de hidratação e após participação, inclusive na recuperação de comandos idempotentes.
- Leaderboard provisório estritamente de leitura, paginado por consulta com funções de janela para calcular posição competitiva e empates sem consultas por pontuação.
- Rate limits configuráveis separados para leitura e mutação de concursos, partição social por usuário autenticado, autenticação por IP, limite global de concorrência e `Retry-After` em respostas `429`.
- Teste de integração do isolamento do limite entre contas e cenário k6 reproduzível com massa PostgreSQL de 1.000 ou 10.000 participantes.

### Frontend — `não commitado`

- Ranking preserva páginas já carregadas durante throttling, respeita `Retry-After` e bloqueia novas tentativas até o prazo indicado pelo servidor.
- Mensagens específicas de excesso de solicitações em português e inglês e teste da interpretação do intervalo de retentativa.
- CI passa a executar os testes Vitest além de lint e TypeScript.

## Etapa 5G — Push de resultados de concursos

Data: 2026-09-17

### Backend — `364a7b5` (`5g`)

- Cadastro autenticado e idempotente de instalações Expo por conta, plataforma e idioma, com desativação explícita e isolamento entre usuários.
- Outbox transacional de resultados de concursos por instalação, deduplicada por concurso e versão da regra, incluindo participantes sem recompensa.
- Worker configurável com envio em lotes ao Expo Push Service, retentativa progressiva, consulta de receipts e desativação automática de tokens `DeviceNotRegistered`.
- Migration `AddPushNotificationOutbox`, configuração desabilitada por padrão e testes de autenticação, propriedade da instalação, idempotência, conteúdo da recompensa e token inválido.

### Frontend — `8e77100` (`5g`)

- Opt-in separado para novidades de concursos na tela de lembretes, sem alterar os lembretes locais existentes.
- Registro e renovação do Expo Push Token por conta, desativação no opt-out/logout e tratamento de permissão negada, web e dispositivo não físico.
- Notificações em primeiro plano e abertura do resultado diretamente no concurso após restauração da autenticação.
- Dependência `expo-device`, textos em português e inglês e testes do fluxo de ativação/desativação.

## Etapa 5F — Recompensas e medalhas de concursos

Data: 2026-09-17

### Backend — `d50bdda` (`5f`)

- Regras versionadas de recompensa para concursos de 7 e 30 dias, com lançamentos separados de participação e colocação nos ledgers de Drops e Prestige.
- Concessão transacional e idempotente após a finalização, restrita a pontuações positivas e com prêmio integral para todas as pessoas empatadas nas posições de pódio.
- Medalhas persistidas por concurso e usuário, evento social de medalha e endpoint autenticado `GET /api/v1/medals`.
- Leaderboard final enriquecido com recompensas e medalha; histórico de progressão identifica o nome do concurso de origem.
- Migration `AddContestRewardsAndMedals` e testes de reprocessamento, valores por posição, empate, pontuação zero, histórico, medalhas e autenticação.

### Frontend — `c4561bf` (`5f`)

- Ranking final mostra Drops, Prestige e medalha recebidos por participante.
- Coleção de medalhas acessível pelo perfil e exibida na tela de progressão, com navegação de volta ao concurso.
- Histórico diferencia recompensa de participação e de colocação; feed renderiza medalhas e abre o concurso correspondente.
- Contratos e textos em português e inglês atualizados, com validação de TypeScript, lint e testes.

## Etapa 5E — Encerramento e resultados imutáveis de concursos

Data: 2026-09-17

### Backend — `748603c` (`5e`)

- Finalização global de concursos por worker configurável, executada somente após todos os participantes ultrapassarem localmente o último dia elegível.
- Snapshot transacional e idempotente da finalização e dos resultados individuais, incluindo posição, pontuação, dias pontuados, empate e perfil público exibido.
- Leaderboard final lido exclusivamente dos resultados persistidos, sem responder a edições posteriores de hidratação ou perfil.
- Posições competitivas compartilhadas em empates e suporte a encerramento válido sem participantes.
- Migration `AddContestFinalizationAndResults` e testes de reprocessamento, imutabilidade, perfil congelado, classificação e concurso vazio.

### Frontend — `e5c689e` (`5e`)

- Pódio exibido em resultados finais para todas as pessoas nas posições 1, 2 e 3, incluindo empates.
- Indicação explícita de empate, destaque do usuário atual e preservação da lista completa paginada.
- Textos do pódio e dos empates em português e inglês.

## Etapa 5D — Leaderboard global de concursos

Data: 2026-09-17

### Backend — `fbd2845` (`5d`)

- Leaderboard global autenticado e paginado por concurso, disponível também para usuários que não participam.
- Pontuação provisória atualizada para todos os participantes antes da leitura, sem depender da abertura da pontuação individual.
- Ranking competitivo com posições compartilhadas em empates e ordenação visual estável.
- Contrato limitado ao perfil público, com anonimato para participantes sem perfil e sem exposição de e-mail, dados físicos ou identificadores internos.
- Testes de ordenação, empate, paginação, privacidade, perfil ausente e autenticação.

### Frontend — `1be8729` (`5d`)

- Ranking global exibido no detalhe do concurso para participantes e não participantes.
- Destaque do usuário atual, posições empatadas, pontuação, dias pontuados e participante anônimo.
- Paginação incremental, estados de carregamento, vazio e erro, com textos em português e inglês.
- Cache do ranking invalidado após participação e alterações online ou sincronizadas da hidratação.

## Etapa 5C — Pontuação diária individual de concursos

Data: 2026-09-17

### Backend — `7cb08fb` (`5c`)

- Snapshots diários únicos por concurso, usuário e data com meta, hidratação equivalente, regra, pontos e estado de finalização.
- Pontuação linear com duas casas decimais, limitada ao teto diário de 100 e iniciada somente na data elegível congelada na entrada.
- Projeção diária de hidratação extraída para serviço compartilhado por streaks e competição.
- Endpoint autenticado de pontuação pessoal e finalização idempotente integrada ao fechamento diário.
- Migration `AddContestDailyScores` compatível com participantes existentes e testes de teto, contribuição das bebidas, data de entrada, atualização provisória e imutabilidade final.

### Frontend — `886d7a7` (`5c`)

- Pontuação total e máximo possível exibidos no detalhe para participantes.
- Lista diária com hidratação versus meta, pontos e distinção entre snapshot provisório e final.
- Cache de pontuação invalidado após alterações online e sincronização offline da hidratação.
- Textos e acessibilidade em português e inglês.

## Etapa 5B — Publicação administrativa de concursos

Data: 2026-09-17

### Backend — `79b1ea4` (`5b`)

- Política `contest-admin` avaliada no servidor pelo usuário autenticado e pela lista de e-mails configurada no ambiente.
- Ambiente local configurado para permitir a publicação pela conta administrativa de desenvolvimento definida pelo responsável do projeto.
- Endpoint administrativo `POST /api/v1/admin/contests` para publicar concursos globais imutáveis de 7 ou 30 dias.
- Endpoint de capacidades que informa somente o resultado da autorização, sem expor a configuração administrativa.
- Validação de nome, duração e data presente ou futura, com resposta `201 Created` para publicações válidas.
- Testes de autenticação, autorização, validação, regras congeladas e disponibilidade imediata na listagem global.

### Frontend — `e4b0cfb` (`concursos`)

- Ação de publicação exibida somente quando a capacidade administrativa retornada pelo backend está ativa.
- Tela administrativa separada com nome, início, duração, resumo do período e regras congeladas.
- Publicação seguida de atualização da listagem global e abertura do concurso criado.
- Nenhuma permissão administrativa é armazenada ou inferida em dados locais do navegador ou dispositivo.
- Textos e acessibilidade em português e inglês e teste de validação de datas civis.

## Etapa 5A — Concursos globais e participação

Data: 2026-09-17

### Backend — `da167f9` (`5a`)

- Concursos globais imutáveis com duração de 7 ou 30 dias e fim exclusivo, independentes de grupos e amizades.
- Versão da regra e teto diário de 100 pontos congelados na criação, com estados derivados pelo relógio da aplicação.
- Consulta disponível a todos os usuários autenticados; criação reservada a operação administrativa interna futura.
- Participação explícita, única e idempotente por usuário e concurso.
- Migration `AddGlobalContests` e testes de domínio, autenticação, visibilidade global, datas e participação.

### Frontend — `c7de047` (`5a`)

- Área própria de concursos globais na navegação principal, sem dependência da tela de grupos.
- Listagem geral com estado, duração, quantidade de participantes e indicação de participação atual.
- Tela de detalhe com período, regras congeladas, participantes e entrada idempotente.
- Textos e acessibilidade em português e inglês.

## Etapa 4F — Bloqueio e segurança social mínima

Data: 2026-09-16

### Backend — `8bf7a27` (`4f`)

- Bloqueio unilateral e idempotente com remoção transacional de amizade ou solicitação pendente.
- Contas bloqueadas são excluídas mutuamente da busca, de novas solicitações e dos eventos visíveis no feed.
- Inclusão direta em grupo é recusada entre contas bloqueadas, preservando associações compartilhadas já existentes.
- Endpoints autenticados para listar, bloquear e desbloquear, protegidos pelo rate limiting social.
- Migration `AddUserBlocks` e testes de domínio, autenticação, privacidade, idempotência, grupos e desbloqueio sem restauração da amizade.

### Frontend — `ec8bd98` (`4e + 4f`)

- Confirmação em dois passos para bloquear um amigo e invalidação dos caches sociais, de grupos e do feed.
- Lista privada de contas bloqueadas com desbloqueio e aviso de que a amizade não será restaurada.
- Estados de carregamento e falha, textos e controles acessíveis em português e inglês.

## Etapa 4E — Reações no feed

Data: 2026-09-16

### Backend — `9223477` (`4e`)

- Catálogo fechado de reações `water`, `celebrate` e `fire`, com uma reação por usuário e evento.
- Endpoints idempotentes para definir, trocar e remover a reação do usuário atual.
- Autorização pela visibilidade atual do evento, bloqueio de reação própria e respostas sem revelar os usuários que reagiram.
- Contagens agregadas e reação atual incorporadas ao contrato paginado do feed.
- Migration `AddFeedReactions` e testes de domínio, autenticação, validação, privacidade, troca e remoção.

### Frontend — `ec8bd98` (`4e + 4f`)

- Botões acessíveis de reação em cada evento, com contagem e destaque da seleção atual.
- Atualização otimista para adicionar, trocar ou remover, com rollback em caso de falha e reconciliação com o servidor.
- Textos de reação e falha em português e inglês, além de testes da atualização entre páginas.

## Etapa 4D — Feed de eventos significativos

Data: 2026-09-16

### Backend — `c0d5f09` (`4d`)

- Feed autenticado e paginado por cursor para desbloqueios de conquistas e entrada em grupos, sem publicar registros de bebida.
- Visibilidade calculada pelas relações atuais: somente amigos aceitos veem conquistas e somente membros atuais veem eventos do grupo.
- Eventos criados na mesma transação da ação de origem, com chave idempotente única e ordenação estável.
- Contrato público limitado ao perfil social, sem e-mail, dados físicos ou detalhes de hidratação.
- Cálculo da vigência da meta alinhado ao `TimeProvider` da aplicação, evitando divergência de data entre perfil e demais serviços.
- Migration `AddSignificantFeed` e testes de domínio, autenticação, privacidade, idempotência, paginação e mudança de visibilidade.

### Frontend — `ec02025` (`4d`)

- Nova aba Atividade com conquistas de amigos e entradas em grupos, textos em português e inglês.
- Atualização por gesto, paginação explícita e remoção de duplicatas entre páginas.
- Navegação do evento para a conquista ou o grupo relacionado e invalidação após ações que produzem eventos.
- Teste da composição ordenada e sem duplicatas das páginas do feed.

## Etapa 4C — Convites e capacidade de grupos

Data: 2026-09-15

### Backend — `c5e6046` (`4c`)

- Limite autoritativo de dois grupos por usuário aplicado à criação, aceite de convite e inclusão direta de membros.
- Convites de grupo reutilizáveis por sete dias, com token aleatório armazenado somente como hash, rotação e revogação pelo proprietário.
- Prévia pública limitada aos dados sociais do grupo e aceite autenticado e idempotente.
- Códigos estáveis para capacidade esgotada e convite indisponível, além de rate limiting social.
- Migration `AddGroupInvitesAndCapacity` e testes de domínio, rotação, revogação, idempotência e limite de slots.

### Frontend — `11f225d` (`4c`)

- Indicador de uso dos dois slots e bloqueio visual da criação quando a capacidade está completa.
- Geração, compartilhamento e revogação de link e QR code pelo proprietário.
- Tela de prévia e aceite de convite com estados de autenticação, associação existente, capacidade e indisponibilidade.
- Convite pendente persistido localmente e retomado depois do onboarding ou login.
- Deep link `water://invite/group/{token}`, textos em português e inglês e testes do parser de links.

## Etapa 4B — Grupos privados e membros

Data: 2026-09-15

### Backend — `13487dd` (`4b`)

- Grupos privados com proprietário e associações únicas por usuário.
- Autorização por associação: externos recebem recurso inexistente e somente o proprietário administra.
- Criação, listagem, detalhe, edição, exclusão, inclusão/remoção de amigos e saída do membro.
- Inclusão idempotente restrita a amizades aceitas e contratos sem e-mail ou dados físicos.
- Migration `AddPrivateGroups` e testes de domínio, autenticação, privacidade, papéis e ciclo de vida.

### Frontend — `0d1b83c` (`4b`)

- Abas principais Hoje, Grupos e Perfil, mantendo rotas auxiliares ocultas da barra.
- Tela Grupos com listagem, estado vazio e criação.
- Detalhe com edição, membros, adição de amigos, remoção, saída e exclusão confirmada.
- Textos em português e inglês e teste da filtragem de amigos disponíveis.

## Etapa 4A — Amizades e solicitações

Data: 2026-09-15

### Backend — `f319803` (`4a`)

- Relação única por par de usuários com solicitações pendentes e amizades aceitas.
- Busca limitada de perfis públicos sem exposição de e-mail ou dados físicos.
- Endpoints autenticados para buscar pessoas, listar amigos e solicitações, enviar, aceitar, recusar, cancelar e remover.
- Solicitações repetidas idempotentes, solicitações cruzadas aceitas automaticamente e autorização por participante.
- Rate limiting social por endereço de origem, migration `AddFriendships` e testes unitários e de integração.

### Frontend — `f913aa4` (`4a`)

- Tela Amigos com busca por username e listas de recebidas, enviadas e amizades.
- Ações de adicionar, aceitar, recusar, cancelar e remover com confirmação e atualização do cache.
- Acesso pela tela Hoje, estados de carregamento/erro/vazio e textos em português e inglês.

## Etapa 3F — Fechamento diário automático

Data: 2026-09-15

### Backend — `b6a79be` (`3f`)

- Worker periódico de fechamento diário com intervalo e tamanho de lote configuráveis.
- Processamento pelo dia local de cada perfil, limitado a dias já encerrados.
- Checkpoint persistente por usuário para retomada segura depois de reinícios e falhas.
- Escopo isolado por usuário, logs estruturados e reaproveitamento das regras autoritativas de streak e recompensas.
- Migration `AddDailyClosureCheckpoint` e testes de avanço monotônico, fuso, checkpoint e reexecução idempotente.

### Frontend — `73646bd` (`3f`)

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
