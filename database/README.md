# Database

The first implementation uses Entity Framework Core with the local SQL Server Express instance detected on this workstation.

Default development connection:

```text
Server=localhost\SQLEXPRESS;Database=BrokerApp_Dev;Trusted_Connection=True;MultipleActiveResultSets=true;TrustServerCertificate=True;Encrypt=False
```

Apply migrations:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend\BrokerApp.Api\BrokerApp.Api.csproj --startup-project backend\BrokerApp.Api\BrokerApp.Api.csproj
```

Create the next migration:

```powershell
dotnet tool run dotnet-ef migrations add MigrationName --project backend\BrokerApp.Api\BrokerApp.Api.csproj --startup-project backend\BrokerApp.Api\BrokerApp.Api.csproj --output-dir Data\Migrations
```

Development startup applies pending migrations and seeds demo data automatically when the API runs in `Development`.
