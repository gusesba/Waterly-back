# Waterly API

## Local development

Requirements: .NET 10 SDK and Docker.

From the repository root:

```powershell
docker compose -f backend/compose.yaml up -d
cd backend
dotnet tool restore
dotnet ef database update --project Water.Infrastructure --startup-project Water.Api
dotnet run --project Water.Api --launch-profile http
```
The API listens on `http://localhost:5004` in the HTTP development profile.

The checked-in database password is only for the disposable local Docker instance. Configure the
production `ConnectionStrings__Water` value through the deployment platform's secret store.

## Verification

```powershell
dotnet build Water.slnx
dotnet test Water.slnx
```
