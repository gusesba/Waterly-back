# Waterly — Patch notes

Este arquivo registra o conteúdo funcional dos commits do backend e do frontend. Os nomes e hashes abaixo correspondem ao histórico real dos repositórios `backend` e `water`.

## Etapa 2E — Feedback visual e conclusão da beta individual

Data: 2026-09-15

### Backend — `não commitado`

- Sem alterações funcionais nesta etapa.

### Frontend — `não commitado`

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
