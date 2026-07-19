# Hosting and PostgreSQL Plan

This document is the reference plan for moving LobiLend from a local prototype to a low-cost hosted environment. Keep `AGENTS.md` linked to this file when making deployment, database, or environment changes. The concrete `lobilend.com` deployment checklist lives in `docs/lobilend-deployment.md`.

## Recommended First Hosting Stack

Use this as the first deploy target unless a later decision changes the constraints:

- Database: Neon PostgreSQL.
- Backend API: Render Web Service or Railway service.
- Frontend: Netlify, Vercel, Render Static Site, or DigitalOcean App Platform static site.

Pragmatic first choice:

- Neon for PostgreSQL because the free tier is persistent and purpose-built for hosted Postgres.
- Render Web Service for the ASP.NET Core API because it is simple and has a usable free web-service tier for prototype testing.
- Netlify or Vercel for the Vite frontend because the frontend is static after `npm.cmd run build`.

Avoid Render free PostgreSQL for anything beyond throwaway testing because Render documents a 30-day expiration for free Postgres databases. Neon is a better fit for persistent prototype data.

## Hosting Options To Compare

| Layer | Option | Why Consider It | Main Caution |
| --- | --- | --- | --- |
| PostgreSQL | Neon | Free persistent Postgres starter plan, branching, serverless scale-to-zero style workflow. | Verify current limits before production; connection pooling and cold starts need testing. |
| PostgreSQL | Supabase | Good free Postgres plus admin UI and auth options. | More platform features than needed can pull architecture off course. |
| Backend API | Render Web Service | Easy Git deploy for ASP.NET Core, TLS, logs, free prototype tier. | Free web services spin down after inactivity, causing cold starts. |
| Backend API | Railway | Very fast deploy experience and PostgreSQL support. | Free trial changes into paid usage; watch spend limits. |
| Backend API | DigitalOcean App Platform | Predictable pricing; static frontend tier is free and app containers start low. | Backend is paid compute; database is paid unless external Neon is used. |
| Backend API | Fly.io | Good for Dockerized apps and regional placement. | More operational work than Render/Railway. |
| Frontend | Netlify | Excellent static hosting for Vite, deploy previews, custom domains, CDN. | New plans are credit-based; monitor monthly credits. |
| Frontend | Vercel | Excellent static hosting and previews. | Best fit is frontend; avoid accidentally moving API logic into serverless functions. |
| Frontend | Render Static Site | Can colocate frontend and API in one provider. | Less specialized frontend workflow than Netlify/Vercel. |
| Frontend | DigitalOcean Static Site | Predictable option if API is also on DigitalOcean. | Lower free outbound allowance than some frontend-first platforms. |

## PostgreSQL Conversion Plan

1. Replace SQL Server EF provider with `Npgsql.EntityFrameworkCore.PostgreSQL`.
2. Use `options.UseNpgsql(...)` in API startup.
3. Change local development connection strings to PostgreSQL.
4. Regenerate EF migrations with the Npgsql provider.
5. Treat SQL Server migration history as obsolete after the provider switch.
6. Keep EF tests on the InMemory provider for service behavior tests.
7. Use hosted connection strings through `ConnectionStrings__BrokerApp`, never committed secrets.
8. Validate DateOnly, decimal precision, indexes, cascade behavior, and unique constraints under PostgreSQL.

Current prototype note: SQL Server `rowversion` does not directly map to PostgreSQL. For this conversion, row-version columns are ignored unless the SQL Server provider is active. Add provider-neutral optimistic concurrency later with PostgreSQL `xmin` or an application-managed concurrency token when concurrent editing becomes real.

## Frontend Hosting Plan

The Vite proxy only works for local development. Hosted frontend builds need an API base URL.

Planned frontend environment variable:

```text
VITE_API_BASE_URL=https://api.lobilend.com
```

Next frontend conversion:

- Update `frontend/src/api.ts` to prefix API calls with `import.meta.env.VITE_API_BASE_URL` when present.
- Keep relative `/api` calls locally so the existing Vite proxy remains useful.
- Configure production CORS on the API to allow the hosted frontend origin.
- Add frontend provider settings:
  - Build command: `npm.cmd run build` locally, `npm run build` on Linux hosts.
  - Publish directory: `frontend/dist`.
  - Root directory: `frontend`.

## Backend Hosting Plan

Backend deploy requirements:

- Runtime: .NET 10.
- Start command: `dotnet BrokerApp.Api.dll` for published output, or provider-specific .NET build/run command.
- Environment variables:
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `ConnectionStrings__BrokerApp=<Neon or hosted Postgres connection string>`
  - `Cors__AllowedOrigins__0=https://lobilend.com`
  - `Cors__AllowedOrigins__1=https://unrivaled-eclair-d5927d.netlify.app`
  - `Frontend__BaseUrl=https://lobilend.com`
  - `Auth__RequireConfirmedEmail=true`
  - `Auth__CookieDomain=<optional parent domain shared by frontend/API>`
  - `Email__Provider=Smtp`
  - `Email__Smtp__Host=smtp.postmarkapp.com`
  - `Email__Smtp__Port=587`
  - `Email__Smtp__FromAddress=no-reply@your-verified-domain.example`
  - `Email__Smtp__FromName=LobiLend`
  - `Email__Smtp__Username=<postmark-server-api-token-or-smtp-access-key>`
  - `Email__Smtp__Password=<postmark-server-api-token-or-smtp-secret-key>`
  - `Email__Smtp__EnableSsl=true`
  - `Email__Smtp__MessageStream=<optional Postmark stream id>`
  - Or use Mailgun HTTP API instead of SMTP:
  - `Email__Provider=Mailgun`
  - `Email__Mailgun__BaseUrl=https://api.mailgun.net`
  - `Email__Mailgun__Domain=lobilend.com`
  - `Email__Mailgun__FromAddress=postmaster@lobilend.com`
  - `Email__Mailgun__FromName=LobiLend`
  - `Email__Mailgun__ApiKey=<mailgun api key>`
  - `Auth__OpenIddict__EncryptionCertificatePath=<production .pfx path>`
  - `Auth__OpenIddict__EncryptionCertificatePassword=<production .pfx password>`
  - `Auth__OpenIddict__SigningCertificatePath=<production .pfx path>`
  - `Auth__OpenIddict__SigningCertificatePassword=<production .pfx password>`
  - Or use `Auth__OpenIddict__EncryptionCertificateBase64` and `Auth__OpenIddict__SigningCertificateBase64` instead of certificate file paths.

For production, do not rely on Development-only seeding. Create a controlled production initialization path before real users are added.

## Authentication Hosting Notes

The app now uses a Cookie BFF-style auth model:

- The frontend calls the API with `credentials: 'include'`.
- The API sets an `HttpOnly` app session cookie for the browser session.
- Unsafe API calls must send the CSRF token from the readable `XSRF-TOKEN` cookie in the `X-XSRF-TOKEN` header.
- Hosted frontend/API origins must be explicitly configured through CORS and must allow credentials.
- Cross-origin hosted cookies require HTTPS and `SameSite=None`; local development uses relaxed cookie settings.

OpenIddict is installed with EF Core stores and a seeded `broker-spa` public client for future Authorization Code + PKCE work. This first auth slice intentionally does not put access or refresh tokens in browser storage.

Production must provide OpenIddict signing/encryption certificates. Development and Testing use development certificates only.

Production open registration must not be enabled without real email delivery. The current development sender logs confirmation, password reset, and user invitation links. The SMTP sender supports Postmark when `Email__Provider=Smtp` is configured; Mailgun HTTP API is enabled with `Email__Provider=Mailgun`. Local testing can use `scripts\Start-Dev.local.example.ps1` for Postmark or `scripts\Start-Dev.local.mailgun.example.ps1` for Mailgun as ignored local secret templates.

Team Lead user creation is organization-scoped. New users receive invitation links and must confirm email plus set their own password; admins should not set passwords for other users.

## Deployment Risks and Decisions

- Existing local SQL Server data will not automatically migrate to PostgreSQL. If any real data matters, export it before switching and build an import step.
- PostgreSQL hosted providers usually require SSL. Neon connection strings generally include SSL settings; keep provider-supplied connection strings intact.
- Automatic `Database.MigrateAsync()` is acceptable for local development, but production should use a deliberate migration step.
- Auth and organization isolation now exist, but public hosting should still wait for production email delivery, certificate configuration, and a second-org isolation smoke test.
- CORS must be explicit once the frontend and API are on different origins.
- Auth email sending supports SMTP/Postmark or Mailgun HTTP API when configured. Workflow email generation is still a prototype action and should not send real email without send authorization and audit behavior.
- File uploads/imports are not implemented; do not choose hosting based on local disk persistence.

## Immediate Follow-Up Slices

1. Run a local PostgreSQL smoke test with `dotnet ef database update` and API startup.
2. Add production API base URL support to the frontend API client.
3. Add CORS configuration driven by environment.
4. Add deployment manifests or provider notes for the chosen stack.
5. Verify SMTP email delivery with the chosen host and domain.
6. Add backup/export guidance for hosted PostgreSQL.
7. Add invite flows, MFA, and admin user management after the Cookie BFF foundation is verified.

## Provider Notes Checked

- Neon pricing should be rechecked before purchasing or upgrading: https://neon.com/pricing
- Render free web services are useful for prototypes but spin down on idle: https://render.com/docs/free
- Render free PostgreSQL is time-limited, so use Neon or a paid database for persistent hosted data: https://render.com/docs/free
- Netlify is a good static frontend candidate for Vite: https://www.netlify.com/pricing/
- Vercel is a good static frontend candidate for Vite: https://vercel.com/pricing
- Railway is a fast API hosting candidate, but pricing should be watched closely: https://railway.com/pricing
- DigitalOcean App Platform is a predictable paid fallback for the API and can host static sites for free: https://docs.digitalocean.com/products/app-platform/details/pricing/
