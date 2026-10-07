# Waterly API

## Local development

Requirements: .NET 10 SDK and Docker.

From the repository root:

```powershell
docker compose -f compose.yaml up -d
dotnet tool restore
$env:ASPNETCORE_ENVIRONMENT='Development'
dotnet ef database update --project Water.Infrastructure --startup-project Water.Api
dotnet run --project Water.Api --launch-profile http
```
The API listens on `http://localhost:5004` in the HTTP development profile.

The checked-in database password is only for the disposable local Docker instance. Configure the
production `ConnectionStrings__Water` value through the deployment platform's secret store.

## Contest push notifications

Push delivery is disabled by default. Enable it with `PushNotifications__Enabled=true`. If enhanced
Expo push security is enabled for the EAS project, configure `PushNotifications__ExpoAccessToken`
through the deployment platform's secret store. Never commit that token.

## Verification

```powershell
dotnet build Water.slnx
dotnet test Water.slnx
```

For real PostgreSQL integration tests, backups, restore verification, telemetry, and the local dashboard,
see [ops/README.md](ops/README.md). The current implementation audit is in
`../Docs/estado-implementacao.md`. Health integrations and paid cosmetics are later roadmap stages.

## Leaderboard load test

The load fixture targets a disposable local database. It removes only users whose identifiers start with
`load-user-` and recreates contest `60000000-0000-0000-0000-000000000001`.

```powershell
$restore = ./scripts/Restore-Database.ps1 -BackupPath '<local backup.dump>'
Get-Content -Raw tests/load/seed-contest.sql | docker compose exec -T postgres psql -U waterly -d $restore.Database -v participant_count=10000 -v ON_ERROR_STOP=1
$env:ConnectionStrings__Water="Host=localhost;Port=5433;Database=$($restore.Database);Username=waterly;Password=waterly-local-only"
$env:DailyClosure__Enabled='false'
$env:ContestClosure__Enabled='false'
$env:ACCESS_TOKEN="<authenticated access token>"
$env:RateLimits__ContestReadPermitLimit="1000000"
Set-Item 'Env:Logging__LogLevel__Microsoft.EntityFrameworkCore.Database.Command' 'Warning'
# Start a dedicated API against this database before running k6 in a second terminal.
# dotnet run --project Water.Api --launch-profile http --urls http://localhost:5005
$env:BASE_URL='http://localhost:5005'
k6 run tests/load/contest-leaderboard.js
```

The default scenario runs 25 virtual users for five minutes and requires less than 1% failed requests and
leaderboard p95 below 500 ms. Restart the API after setting the dedicated load-test rate limit.
`BASE_URL`, `CONTEST_ID`, `VUS`, and `DURATION` can be overridden.
