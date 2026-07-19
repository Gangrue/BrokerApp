# AGENTS.md

Guidance for coding agents working in this repository.

## Project Summary

LobiLend is a prototype loan officer workflow application.

- Backend: ASP.NET Core API on `.NET 10`, Entity Framework Core, PostgreSQL through Npgsql.
- Frontend: React 19 + TypeScript + Vite.
- Database: EF Core migrations in `backend/BrokerApp.Api/Data/Migrations`.
- Product references: `docs/loan_officer_app_design.docx` and `docs/Example Processing Tracker.xlsm`.
- Auth: ASP.NET Core Identity, secure cookie browser sessions, CSRF-protected unsafe API calls, and OpenIddict EF stores for the OIDC foundation.

## Repository Layout

- `backend/BrokerApp.Api` - API, domain entities, EF Core DbContext, services, controllers.
- `backend/BrokerApp.Api.Tests` - xUnit service and endpoint tests.
- `frontend` - Vite React app.
- `database` - database setup and migration notes.
- `docs` - product design assets, tracker workbook, and visual images.
- `docs/hosting-postgresql-plan.md` - hosting options, PostgreSQL conversion notes, and deployment follow-up plan.
- `docs/lobilend-deployment.md` - concrete deployment checklist for `app.lobilend.com`, `api.lobilend.com`, Neon, and Mailgun.

## Backend Architecture

Keep backend changes aligned with the existing feature-folder pattern:

- `Controllers/*Controller.cs` expose HTTP endpoints.
- `Features/<Area>/*Service.cs` contains business logic.
- `Features/<Area>/*Dtos.cs` contains request/response contracts.
- `Domain/*.cs` contains EF entities.
- `Data/BrokerAppDbContext.cs` contains EF configuration.
- `Data/DevDataSeeder.cs` seeds development data and the future `broker-spa` OpenIddict client.
- `Features/Audit/AuditWriter.cs` records material business changes.

Important existing concepts:

- Organization-scoped data is central. Most business entities have `OrganizationId`.
- Services should resolve org/user through `ICurrentUserContext`. Keep `DevDataIds` limited to development seed data and tests.
- Do not introduce a second data-access pattern; use EF Core through `BrokerAppDbContext`.
- Prefer feature services over controller-heavy logic.
- Keep validation failures explicit through existing validation exception patterns.
- Write `AuditEvent` records for user-visible business mutations where existing flows do.

## Frontend Architecture

The frontend is currently a single rich prototype surface:

- `frontend/src/App.tsx` contains app state, workspace views, and major components.
- `frontend/src/App.css` contains the design system and page/component styling.
- `frontend/src/api.ts` contains API DTO types and fetch client functions.
- Vite proxies `/api` to `http://127.0.0.1:5108`.

Current main workspace views:

- `home`
- `dashboard`
- `actionDetail`
- `loans`
- `loanDetail`
- `customers`
- `customerDetail`
- `reports`
- `admin`
- `account`
- `intake`

When adding frontend behavior:

- Put API calls in `frontend/src/api.ts`, not inline fetches in components.
- Keep backend DTO and frontend types synchronized.
- Preserve loading, error, empty, disabled, and success states.
- Keep sidebar navigation and detail-page scroll behavior consistent with current helpers.
- Avoid adding new UI libraries unless explicitly requested.

## Visual Design Direction

Current theme roles:

- Main background: `#34382F`
- Background pattern: `#4B5045`
- Sidebar: `#E8D9B8`
- Active navigation: `#F4E8C9`
- Primary card: `#FBF8F1`
- Secondary card: `#F0ECE4`
- Page canvas: `#F5F0E6`
- Border/divider: `#D7D0C2`
- Primary text: `#1F292D`
- Secondary text: `#56636A`
- Primary action: `#28685F`
- Action hover: `#20564F`
- Subtle highlight: `#A9823A`

Design rules for this app:

- This is an operational workflow tool, not a marketing site.
- Use dense but readable layouts with restrained styling.
- Keep panels/cards light against the dark patterned dashboard canvas.
- Sidebar and dashboard chrome should have clear contrast.
- Rows should have consistent hover and selected behavior.
- Avoid decorative clutter that competes with pipeline/action data.
- Keep text from overflowing buttons, table cells, panels, and mobile layouts.

## Database and Migrations

Development DB is PostgreSQL and is described in `database/README.md`.

Default development connection currently targets:

```text
Host=localhost;Port=5432;Database=brokerapp_dev;Username=postgres;Password=postgres
```

Use `ConnectionStrings__BrokerApp` for hosted environments such as Neon. Do not commit provider credentials.

Apply migrations:

```powershell
dotnet tool restore
dotnet tool run dotnet-ef database update --project backend\BrokerApp.Api\BrokerApp.Api.csproj --startup-project backend\BrokerApp.Api\BrokerApp.Api.csproj
```

Create migrations:

```powershell
dotnet tool run dotnet-ef migrations add MigrationName --project backend\BrokerApp.Api\BrokerApp.Api.csproj --startup-project backend\BrokerApp.Api\BrokerApp.Api.csproj --output-dir Data\Migrations
```

Development startup applies pending migrations and seeds demo data automatically.

Provider note:

- EF is configured for PostgreSQL through `Npgsql.EntityFrameworkCore.PostgreSQL`.
- SQL Server `rowversion` is not mapped under PostgreSQL in the current prototype; add provider-neutral concurrency later when concurrent editing is implemented.
- Hosting/deployment planning lives in `docs/hosting-postgresql-plan.md`.

## Run Commands

Preferred combined startup:

```powershell
powershell.exe -ExecutionPolicy Bypass -File scripts\Start-Dev.ps1 -NoRestore
```

Preferred combined stop:

```powershell
powershell.exe -ExecutionPolicy Bypass -Command "& .\scripts\Stop-Dev.ps1 -Ports 5108,5173,5174"
```

Backend:

```powershell
dotnet run --project backend\BrokerApp.Api\BrokerApp.Api.csproj --launch-profile http
```

Frontend:

```powershell
cd frontend
npm.cmd run dev -- --host 127.0.0.1
```

Frontend URL:

```text
http://127.0.0.1:5173/
```

API URL:

```text
http://127.0.0.1:5108/
```

Local restart notes:

- `Start-Dev.ps1` starts both API and Vite in the foreground. Use Ctrl+C in that same terminal to stop both process trees.
- If Vite says `Port 5173 is in use, trying another one`, do not continue on `5174` for normal testing. Stop stale listeners and restart so the app is back on `http://127.0.0.1:5173/`.
- Check occupied dev ports with `netstat -ano | findstr ":5108 :5173 :5174"`. This proved more reliable than `Get-NetTCPConnection` during recent restarts.
- If stale listeners remain, identify exact process names with `Get-Process -Id <pid>` and stop only the specific API/Vite PIDs. Avoid broad `node` or `dotnet` kills because unrelated work may be running.
- When calling `Stop-Dev.ps1` with multiple ports from another PowerShell process, prefer `-Command "& .\scripts\Stop-Dev.ps1 -Ports 5108,5173,5174"`. Passing array syntax through `-File` can parse unexpectedly.
- Hosted PostgreSQL connection strings should be set in the process environment, for example `ConnectionStrings__BrokerApp`, before startup. Do not write Neon or other provider credentials into committed files.
- If auth state looks inconsistent after backend changes, log out first or stop stale API/Vite processes. Old browser cookies plus a new/stale CSRF token can produce a generic 400 until the client refreshes its CSRF token.
- ASP.NET antiforgery tokens can be identity-bound. After login, logout, register, email confirmation, or any session boundary change, force a fresh `/api/v1/auth/csrf` request.
- Do not read the antiforgery cookie and echo it as `X-XSRF-TOKEN`. The cookie token and request token are different. The SPA must use the `csrfToken` JSON value returned by `/api/v1/auth/csrf`; the server stores the cookie as `BrokerApp.Antiforgery`.
- Logout is intentionally `[IgnoreAntiforgeryToken]` and `[AllowAnonymous]` so a stale/invalid CSRF token cannot trap the browser in an old session. Keep state-changing business APIs CSRF-protected.

Manual API check:

```powershell
Invoke-RestMethod http://127.0.0.1:5108/api/v1/dashboard
```

## Verification Commands

Frontend:

```powershell
cd frontend
npm.cmd run lint
npm.cmd run build
```

Backend:

```powershell
dotnet build BrokerApp.slnx --no-restore
dotnet test backend\BrokerApp.Api.Tests\BrokerApp.Api.Tests.csproj --no-restore
```

If restore is needed:

```powershell
dotnet restore BrokerApp.slnx
```

## Testing Guidance

Backend tests should cover:

- Dashboard bucket classification.
- Endpoint response shape.
- Workflow mutations: complete, reschedule, comment, cancel, reassign.
- Intake and add-loan validation.
- Template generation idempotency.
- Organization/user-scoped behavior through authenticated current-user context.

Frontend checks should include:

- `npm.cmd run lint`
- `npm.cmd run build`
- Manual dashboard interaction checks.
- Manual intake/add-loan/template generation checks.
- Visual checks for sidebar collapse, topbar banner, row selected states, pagination, and mobile wrapping.

## Current Product Capabilities

Implemented prototype areas include:

- Database-backed dashboard.
- Workflow persistence for actions.
- Loan/customer detail pages.
- Intake for new borrower and existing customer.
- Add loan to existing customer.
- Action templates and generation.
- Audit baseline.
- Reports prototype.
- Spreadsheet-parity tracker fields such as title/realtor contacts, co-borrower email, ICD state, closing timing, section needs, notes, and history.
- Home page with hero carousel and summary cards.

## Auth

Current auth uses a Cookie BFF-style browser session:

- The SPA does not store access or refresh tokens in browser storage.
- Login sets the `BrokerApp.Session` `HttpOnly` cookie.
- Unsafe API calls require the readable `XSRF-TOKEN` cookie value in the `X-XSRF-TOKEN` header.
- `frontend/src/api.ts` handles `credentials: 'include'`, CSRF token refresh, and 401 responses.
- API controllers are globally authorized except auth endpoints marked with `[AllowAnonymous]`.
- `ICurrentUserContext` is the required way to access current user/org/role in feature services.
- OpenIddict EF stores are registered for future Authorization Code + PKCE clients; the first slice does not expose browser-held tokens.

Auth-related environment variables:

```text
ConnectionStrings__BrokerApp=<PostgreSQL connection string>
Cors__AllowedOrigins__0=<frontend origin>
Frontend__BaseUrl=<frontend origin>
Auth__RequireConfirmedEmail=true
Auth__CookieDomain=<optional shared parent domain>
Email__Provider=Development|Smtp|Mailgun
Auth__OpenIddict__EncryptionCertificatePath=<production .pfx path>
Auth__OpenIddict__EncryptionCertificatePassword=<production .pfx password>
Auth__OpenIddict__SigningCertificatePath=<production .pfx path>
Auth__OpenIddict__SigningCertificatePassword=<production .pfx password>
```

Production notes:

- Production startup requires OpenIddict signing/encryption certificates. Use either certificate file paths or `Auth__OpenIddict__EncryptionCertificateBase64` / `Auth__OpenIddict__SigningCertificateBase64` with matching passwords.
- Open registration is blocked in Production unless a real email provider is configured.
- Development email confirmation and password reset links are logged by the API and may be returned by auth responses only in Development.
- Use `SameSite=None` + `Secure` cookies for hosted cross-origin frontend/API deployments.
- Team Leads can create organization users from Admin. New users are invited by email and set their own password through the reset-password flow; do not add admin-set passwords.
- `Email__Provider=Smtp` enables SMTP auth email delivery. Required SMTP keys are `Email__Smtp__Host`, `Email__Smtp__Port`, `Email__Smtp__FromAddress`, optional `Email__Smtp__FromName`, `Email__Smtp__Username`, `Email__Smtp__Password`, `Email__Smtp__EnableSsl`, and Postmark-specific `Email__Smtp__MessageStream`.
- `Email__Provider=Mailgun` enables Mailgun HTTP API delivery. Required keys are `Email__Mailgun__ApiKey`, `Email__Mailgun__Domain`, `Email__Mailgun__FromAddress`, and optional `Email__Mailgun__BaseUrl` / `Email__Mailgun__FromName`.
- For local SMTP testing, copy either `scripts/Start-Dev.local.example.ps1` for Postmark or `scripts/Start-Dev.local.mailgun.example.ps1` for Mailgun to `scripts/Start-Dev.local.ps1`, fill in provider credentials and a verified sender, then start with `scripts\Start-Dev.ps1`. The local settings file is ignored by git.
- Frontend hosting uses `netlify.toml` from the repo root. Set `VITE_API_BASE_URL=https://api.lobilend.com` in the frontend host. Backend Docker hosting can use `Dockerfile.api`.

## Coding Conventions

- Keep edits focused and consistent with existing patterns.
- Prefer existing services and DTO style over new abstractions.
- Avoid unrelated refactors.
- Use nullable annotations correctly.
- Keep controller records out of controller files; prefer feature DTO files.
- Add EF indexes for organization-scoped lookup paths.
- Keep seeded data realistic and useful for visible prototype testing.
- Preserve public API response shapes unless the task explicitly changes them.

## Git and Workspace Safety

- The worktree may contain user changes. Do not revert unrelated changes.
- Do not use destructive git commands unless explicitly requested.
- Prefer `rg`/`rg --files` for search.
- Use `apply_patch` for manual edits.
- Use `npm.cmd` from PowerShell for frontend scripts.
