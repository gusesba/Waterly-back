# Operações da beta Waterly

Este runbook cobre ferramentas locais verificáveis. O ambiente de produção e seus responsáveis ainda precisam ser definidos; subir este Compose não publica a API nem configura entrega externa de alertas.

## Verificação com PostgreSQL

```powershell
docker compose -f compose.yaml up -d postgres
$env:WATERLY_TEST_POSTGRES='Host=localhost;Port=5433;Database=postgres;Username=waterly;Password=waterly-local-only'
dotnet test Water.slnx
dotnet ef migrations has-pending-model-changes --project Water.Infrastructure --startup-project Water.Api
Remove-Item Env:WATERLY_TEST_POSTGRES
```

Cada fixture cria uma base `waterly_test_<guid>`, aplica todas as migrations e remove somente sua própria base. O usuário do teste precisa poder criar bases. Sem a variável, a suíte usa SQLite em memória. O CI executa os dois provedores.

## Backup e restauração

```powershell
./scripts/Backup-Database.ps1 -OutputPath 'C:/backups/waterly/2026-10-07.dump'
./scripts/Restore-Database.ps1 -BackupPath 'C:/backups/waterly/2026-10-07.dump'
./scripts/Test-DatabaseRestore.ps1
```

Os scripts usam o PostgreSQL do Compose principal. Backup usa `pg_dump` em formato custom e recusa sobrescrever um arquivo existente. Restauração aceita somente uma **nova** base `waterly_restore_<sufixo>`; `createdb` recusa nomes existentes. Nunca restaura sobre `waterly`. Uma restauração com falha permanece disponível para inspeção.

O teste de restauração compara migrations, contas, registros, resultados e quantidades/somas de ledgers, e remove sua base temporária quando a restauração termina. Pause escritas da API e workers durante a comparação para que a base original não avance em relação ao snapshot. O dump permanece no diretório temporário informado pelo resultado. Dumps contêm dados privados: use armazenamento restrito, criptografado, externo à máquina de produção, com retenção, agendamento e testes periódicos definidos no ambiente de implantação. Não versionar dumps. Uma cópia local não substitui essa política.

## Telemetria, dashboard e alertas

```powershell
$env:WATERLY_GRAFANA_PASSWORD='<senha local exclusiva>'
docker compose -f ops/compose.yaml up -d
$env:OTEL_EXPORTER_OTLP_ENDPOINT='http://localhost:4317'
$env:OTEL_EXPORTER_OTLP_PROTOCOL='grpc'
dotnet run --project Water.Api --launch-profile http
```

Grafana: `http://localhost:3002`, usuário `admin`, dashboard **Waterly beta**. Prometheus: `http://localhost:9090`. OTLP: `localhost:4317`. Todos os ports publicados ficam em loopback. Para alterar Grafana, defina `WATERLY_GRAFANA_PORT`. A senha é obrigatória inclusive em comandos Compose que consultam apenas outro serviço; mantenha a variável na sessão. As imagens e configurações são locais, com armazenamento efêmero; configurar volumes/retenção no ambiente durável.

A API funciona sem collector; exportação só é ativada com `OTEL_EXPORTER_OTLP_ENDPOINT`. As métricas `waterly_http_duration_seconds_*`, `waterly_job_runs_total`, `waterly_job_duration_seconds_*` e `waterly_job_last_success_seconds` têm labels de método, template de rota, status, nome do job e resultado. Não incluem email, usuário, query string, volume ou dados físicos. Traces registram os mesmos atributos técnicos; o collector local usa somente o exporter de debug, sem armazenamento de traces para consulta histórica. Logs JSON incluem correlação e trace ID e exportam apenas o campo permitido dos scopes. Mantenha ASP.NET Core em nível Warning e não habilite logging de dados sensíveis do EF.

As regras sinalizam erros HTTP acima de 1%, p95 acima de 500 ms, falhas de jobs, fechamento sem sucesso por uma hora e ausência de telemetria. Estão provisionadas no Prometheus; entrega de notificações exige Alertmanager/contato configurado pela implantação. Jobs desabilitados não produzem métricas de sucesso. Um job que nunca teve sucesso deve ser diagnosticado junto ao alerta de ausência e aos logs de inicialização; o alerta de atraso usa a última execução bem-sucedida conhecida.

```powershell
docker compose -f ops/compose.yaml exec -T prometheus promtool check config /etc/prometheus/prometheus.yml
docker compose -f ops/compose.yaml exec -T prometheus promtool check rules /etc/prometheus/alerts.yaml
```

DailyClosure e ContestClosure usam checkpoints persistidos e reexecução idempotente. Push usa outbox, retentativas e receipts; permanece desabilitado por padrão. Falhas de um item tornam a execução do lote uma falha de telemetria, preservando a continuação dos outros itens. Não apagar checkpoints para tentar recuperar um incidente: corrigir a causa e deixar o próximo ciclo retomar.

## Métricas de produto

`GET /api/v1/admin/metrics` exige autenticação e email em `Administration__ContestAdminEmails`. O relatório é agregado, sem SDK de analytics no cliente. D1/D7/D30 usam primeiro dia de hidratação e grupo atual; não equivalem a coortes de instalação nem medem causalidade. `null` significa que não há elegíveis. Tempo até primeiro registro usa cadastro conhecido e primeira persistência no servidor; contas antigas ficam fora dessa amostra. Quick-add/custom têm classificação declarada pelo cliente; origens antigas ficam como desconhecidas. Previews de convites contam chamadas válidas, sem identificar visitantes; aceites contam entradas efetivas e não retries. Streaks e fluxo de Drops por tipo também estão agregados. Exclusão de dados/conta pode alterar os totais conservados. Consulte `Docs/decisoes/metricas-beta.md` para interpretação.

A migration `20261007140649_AddMvpAcquisitionMetrics` deve ser aplicada antes de iniciar a API atual. Ela adiciona campos com defaults conservadores e não reescreve pontuação ou ledgers. Metadados privados de cadastro e classificação de entrada integram a exportação da própria conta. `waterly_hydration_replays_total{operation=~"create|update|delete"}` registra retentativas idempotentes; histogramas HTTP filtrados por template de hidratação, método e status permitem observar erros de sincronização sem exportar payloads.

## Carga de leaderboard

Restaure um dump em uma nova base descartável e aponte uma segunda API para ela. Desabilite workers e aumente `RateLimits__ContestReadPermitLimit` somente nessa API. Nunca execute `tests/load/seed-contest.sql` na base de uso real: a fixture remove registros de teste com prefixo conhecido. Rode 10 mil participantes, 25 VUs e cinco minutos, mantendo o token fora de arquivos versionados. Consulte `README.md` para os comandos e `Docs/estado-implementacao.md` para o resultado desta auditoria.

## Aceite antes de publicar a beta

Validar development builds em dispositivos iOS/Android (SecureStore, retomada offline, deep link/QR, redução de movimento e permissões de notificações). Push remoto requer projeto EAS e credenciais das lojas. Configurar domínio/HTTPS, CORS explícito, segredos de produção e persistência das chaves de Data Protection; instâncias precisam compartilhar as mesmas chaves para tokens Identity. Definir backup externo, retenção, entrega de alertas e responsáveis por incidentes. Validar a política de privacidade/idade mínima com responsáveis pelo produto. Integrações de saúde pertencem à etapa 7 e precisam de spike e dispositivos físicos.
