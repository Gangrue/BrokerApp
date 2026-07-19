# LobiLend Deployment

This is the working deployment path for the current prototype.

## Target Shape

- Frontend: Netlify static site at `https://lobilend.com`.
- Netlify fallback subdomain: `https://unrivaled-eclair-d5927d.netlify.app`.
- Backend API: Docker-hosted ASP.NET Core service at `https://api.lobilend.com`.
- Database: existing Neon PostgreSQL database.
- Email: Mailgun HTTP API using `lobilend.com`.

The frontend and API are intentionally separate origins. The API uses secure
cookies and CORS with credentials.

## Frontend Hosting

Use Netlify for the Vite app.

Netlify settings:

```text
Base directory: frontend
Build command: npm run build
Publish directory: frontend/dist
```

This repo also includes `netlify.toml`, so Netlify can infer those settings from
the repository root.

Frontend environment variable:

```text
VITE_API_BASE_URL=https://api.lobilend.com
```

Current Netlify domains:

```text
lobilend.com
unrivaled-eclair-d5927d.netlify.app
```

Keep the Netlify subdomain available while DNS propagates. The API CORS config
allows both origins during the transition.

If Netlify DNS is authoritative for `lobilend.com`, add API DNS records in
Netlify DNS, not GoDaddy. If GoDaddy remains authoritative, keep DNS records in
GoDaddy.

## Backend Hosting

Use Render first for the API. The repository includes both `Dockerfile.api` and
`render.yaml`.

Render path:

1. Push these deployment files to GitHub.
2. In Render, create a new Blueprint from this repository, or create a Web
   Service manually.
3. If using the Blueprint, Render will read `render.yaml` and prompt for secret
   values marked `sync: false`.

Container settings:

```text
Dockerfile: Dockerfile.api
Port: Render-provided PORT, defaulting to 10000
Health check path: /healthz
Manual API check: https://api.lobilend.com/api/v1/auth/csrf
```

Backend environment variables:

```text
ASPNETCORE_ENVIRONMENT=Production
ConnectionStrings__BrokerApp=<Neon PostgreSQL connection string>
Cors__AllowedOrigins__0=https://lobilend.com
Cors__AllowedOrigins__1=https://unrivaled-eclair-d5927d.netlify.app
Frontend__BaseUrl=https://lobilend.com
Auth__RequireConfirmedEmail=true
Email__Provider=Mailgun
Email__Mailgun__BaseUrl=https://api.mailgun.net
Email__Mailgun__Domain=lobilend.com
Email__Mailgun__FromAddress=postmaster@lobilend.com
Email__Mailgun__FromName=LobiLend
Email__Mailgun__ApiKey=<Mailgun API key>
Auth__OpenIddict__EncryptionCertificateBase64=<base64 pfx>
Auth__OpenIddict__EncryptionCertificatePassword=<pfx password>
Auth__OpenIddict__SigningCertificateBase64=<base64 pfx>
Auth__OpenIddict__SigningCertificatePassword=<pfx password>
```

Do not commit the Neon connection string, Mailgun API key, or certificate values.

After the backend deploys, add:

```text
api.lobilend.com
```

In the active DNS provider, add the CNAME record your backend host gives you for
`api`. For Render this typically points `api` to the service's `*.onrender.com`
hostname or to the exact DNS target Render displays.

Leave `Auth__CookieDomain` unset unless a specific host requires it. The API can
set a host-only session cookie for `api.lobilend.com`, and browser requests from
`https://lobilend.com` will still send it to the API because the frontend client
uses `credentials: 'include'`.

## OpenIddict Certificates

Production requires persistent signing and encryption certificates. Generate
two PFX certificates locally, convert them to base64, and store the values in
the backend host environment variables.

Helper script:

```powershell
powershell.exe -ExecutionPolicy Bypass -File scripts\New-OpenIddictCertificates.ps1 -Password "<strong-password>"
```

The script prints the four `Auth__OpenIddict__*` environment variables for the
API host. The generated `.local-certs` directory is ignored by git.

Manual PowerShell pattern:

```powershell
$password = ConvertTo-SecureString "<strong-password>" -AsPlainText -Force

$signing = New-SelfSignedCertificate -Subject "CN=LobiLend OpenIddict Signing" -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec Signature
Export-PfxCertificate -Cert $signing -FilePath .\signing.pfx -Password $password
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\signing.pfx"))

$encryption = New-SelfSignedCertificate -Subject "CN=LobiLend OpenIddict Encryption" -CertStoreLocation Cert:\CurrentUser\My -KeyExportPolicy Exportable -KeySpec KeyExchange
Export-PfxCertificate -Cert $encryption -FilePath .\encryption.pfx -Password $password
[Convert]::ToBase64String([IO.File]::ReadAllBytes(".\encryption.pfx"))
```

Use the same password in the corresponding `Auth__OpenIddict__*Password`
variables.

## Deployment Order

1. Confirm the Neon database has current migrations. The current shared Neon DB
   has been used locally, but a fresh DB needs `dotnet ef database update`.
2. Deploy backend API with Neon, Mailgun, and OpenIddict certificate env vars.
3. Add `api.lobilend.com` to the backend host and wait for TLS.
4. Add the `api` DNS record in the active DNS provider.
5. Confirm `https://api.lobilend.com/healthz` returns `{ "status": "ok" }`.
6. Confirm `https://api.lobilend.com/api/v1/auth/csrf` returns JSON.
7. Deploy frontend with `VITE_API_BASE_URL=https://api.lobilend.com`.
8. Confirm `https://lobilend.com` loads the SPA and can register/log in.
9. Register a fresh account and confirm the Mailgun email arrives.
10. Log in, load dashboard data, and create one test intake file.

## Render Health Check Note

Do not use `/api/v1/auth/csrf` as Render's internal health check. That endpoint
intentionally creates a secure antiforgery cookie. Render health checks hit the
container over internal HTTP, which makes ASP.NET reject secure cookie creation.
Use `/healthz` for health checks and reserve `/api/v1/auth/csrf` for external
HTTPS browser/API validation.

## GoDaddy / DNS Checklist

First determine who is authoritative for `lobilend.com`:

- If GoDaddy nameservers still show on the domain, manage DNS in GoDaddy.
- If Netlify nameservers show on the domain, manage DNS in Netlify DNS.

GoDaddy DNS mode:

```text
Type   Name   Value
A      @      75.2.60.5
CNAME  www    unrivaled-eclair-d5927d.netlify.app
CNAME  api    <backend host DNS target>
```

Netlify DNS mode:

```text
Type   Name   Value
NETLIFY managed records for lobilend.com
CNAME  api    <backend host DNS target>
```

Mailgun DNS records must also exist in the active DNS provider. Copy the exact
TXT, MX, and CNAME records Mailgun shows for `lobilend.com`.

## Notes

- `https://lobilend.com` is currently the app domain. If a separate marketing
  site is added later, move the app to `https://app.lobilend.com` and update
  `Cors__AllowedOrigins__0`, `Frontend__BaseUrl`, and Netlify custom domains.
- Database migrations are automatic only in Development. For Production, run
  migrations deliberately as a deployment step before serving real traffic.
