param(
    [int[]]$Ports = @(5108, 5173)
)

$ErrorActionPreference = "Stop"

foreach ($port in $Ports) {
    $connections = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    $processIds = $connections |
        Where-Object { $_.OwningProcess -gt 0 } |
        Select-Object -ExpandProperty OwningProcess -Unique

    if (-not $processIds) {
        Write-Host "No listening process found on port $port." -ForegroundColor DarkGray
        continue
    }

    foreach ($processId in $processIds) {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        $processName = if ($process) { $process.ProcessName } else { "PID $processId" }

        Write-Host "Stopping $processName on port $port (PID $processId)..." -ForegroundColor Yellow
        & taskkill.exe /PID $processId /T /F | Out-Null
    }
}

Write-Host "Development servers stopped." -ForegroundColor Green
