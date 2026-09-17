# Waterly API

## Local development

Requirements: .NET 10 SDK and Docker.

From the repository root:

```powershell
docker compose -f compose.yaml up -d
dotnet tool restore
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
