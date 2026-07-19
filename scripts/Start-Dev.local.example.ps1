# Copy this file to scripts\Start-Dev.local.ps1 and fill in local-only secrets.
# Start-Dev.ps1 automatically loads scripts\Start-Dev.local.ps1 before starting
# the API and frontend. The .local.ps1 file is ignored by git.

# Postmark SMTP for auth emails.
# Use a Postmark Server API Token as both username and password, or use a
# stream-specific SMTP Access Key / Secret Key pair.
$env:Email__Provider = "Smtp"
$env:Email__Smtp__Host = "smtp.postmarkapp.com"
$env:Email__Smtp__Port = "587"
$env:Email__Smtp__FromAddress = "no-reply@your-verified-domain.example"
$env:Email__Smtp__FromName = "LobiLend"
$env:Email__Smtp__Username = "<postmark-server-api-token-or-smtp-access-key>"
$env:Email__Smtp__Password = "<postmark-server-api-token-or-smtp-secret-key>"
$env:Email__Smtp__EnableSsl = "true"
# Optional. Leave empty to use Postmark's default outbound transactional stream.
$env:Email__Smtp__MessageStream = ""

# Keep these aligned with the local frontend when testing email links.
$env:Frontend__BaseUrl = "http://127.0.0.1:5173"
$env:Cors__AllowedOrigins__0 = "http://127.0.0.1:5173"
