# Database

The app uses Entity Framework Core with PostgreSQL through the Npgsql provider.

Default development connection:

```text
Host=localhost;Port=5432;Database=brokerapp_dev;Username=postgres;Password=postgres
```

Local setup options:

```powershell
# Docker, if available
docker run --name brokerapp-postgres -e POSTGRES_PASSWORD=postgres -e POSTGRES_DB=brokerapp_dev -p 5432:5432 -d postgres:17
```

Or install PostgreSQL locally and create database `brokerapp_dev` with user `postgres` / password `postgres`, or update `ConnectionStrings__BrokerApp` to match your local credentials.

For hosted environments, override the connection string through environment configuration:

```powershell
ConnectionStrings__BrokerApp="<provider connection string>"
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

For production hosting, prefer a controlled migration step during deployment instead of automatic migrations on every app boot.

## Authentication Tables

The database includes ASP.NET Core Identity tables and OpenIddict tables:

- `Users`, `Roles`, `UserClaims`, `UserRoles`, `UserLogins`, `UserTokens`, `RoleClaims`
- `OpenIddictApplications`, `OpenIddictAuthorizations`, `OpenIddictScopes`, `OpenIddictTokens`

Development seeding creates confirmed demo users and a `broker-spa` public OpenIddict client for future Authorization Code + PKCE work.

Required hosted auth configuration:

```powershell
ConnectionStrings__BrokerApp="<hosted PostgreSQL connection string>"
Cors__AllowedOrigins__0="https://your-frontend.example"
Frontend__BaseUrl="https://your-frontend.example"
Auth__RequireConfirmedEmail="true"
Email__Provider="Smtp"
Email__Smtp__Host="smtp.postmarkapp.com"
Email__Smtp__Port="587"
Email__Smtp__FromAddress="no-reply@your-verified-domain.example"
Email__Smtp__FromName="LobiLend"
Email__Smtp__Username="<postmark-server-api-token-or-smtp-access-key>"
Email__Smtp__Password="<postmark-server-api-token-or-smtp-secret-key>"
Email__Smtp__EnableSsl="true"
Email__Smtp__MessageStream="<optional Postmark stream id>"
Auth__OpenIddict__EncryptionCertificatePath="<path to pfx>"
Auth__OpenIddict__SigningCertificatePath="<path to pfx>"
```

For local SMTP testing, copy one provider template to `scripts\Start-Dev.local.ps1`,
fill in the credentials and verified sender address, then run
`scripts\Start-Dev.ps1`. Do not commit `Start-Dev.local.ps1`.

Provider templates:

- Postmark: `scripts\Start-Dev.local.example.ps1`
- Mailgun HTTP API: `scripts\Start-Dev.local.mailgun.example.ps1`

Do not enable public production registration until SMTP is configured and verified. Team Lead user creation sends invitation emails; invited users confirm email and set their own password.
