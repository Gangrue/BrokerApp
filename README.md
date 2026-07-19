# LobiLend

Prototype loan officer workflow application.

## Structure

- `backend/BrokerApp.Api` - ASP.NET Core API
- `frontend` - React TypeScript app powered by Vite
- `database` - schema and migration notes
- `docs` - product design and Excel workflow prototype

## Run locally

Backend:

```powershell
dotnet run --project backend\BrokerApp.Api\BrokerApp.Api.csproj
```

Frontend:

```powershell
cd frontend
npm.cmd run dev
```

Use `npm.cmd` from PowerShell if script execution blocks `npm.ps1`.

Database:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend\BrokerApp.Api\BrokerApp.Api.csproj --startup-project backend\BrokerApp.Api\BrokerApp.Api.csproj
```
