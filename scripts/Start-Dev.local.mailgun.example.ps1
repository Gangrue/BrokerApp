# Copy this file to scripts\Start-Dev.local.ps1 and fill in local-only secrets.
# Start-Dev.ps1 automatically loads scripts\Start-Dev.local.ps1 before starting
# the API and frontend. The .local.ps1 file is ignored by git.

# Mailgun HTTP API for auth emails.
$env:Email__Provider = "Mailgun"
$env:Email__Mailgun__BaseUrl = "https://api.mailgun.net"
$env:Email__Mailgun__Domain = "lobilend.com"
$env:Email__Mailgun__FromAddress = "postmaster@lobilend.com"
$env:Email__Mailgun__FromName = "LobiLend"
$env:Email__Mailgun__ApiKey = "<mailgun-api-key>"

# Keep these aligned with the local frontend when testing email links.
$env:Frontend__BaseUrl = "http://127.0.0.1:5173"
$env:Cors__AllowedOrigins__0 = "http://127.0.0.1:5173"
