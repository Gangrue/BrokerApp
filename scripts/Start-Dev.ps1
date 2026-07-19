param(
    [switch]$NoRestore
)

$ErrorActionPreference = "Stop"

$root = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$apiProject = Join-Path $root "backend\BrokerApp.Api\BrokerApp.Api.csproj"
$frontendDir = Join-Path $root "frontend"
$localSettings = Join-Path $root "scripts\Start-Dev.local.ps1"
$script:devProcesses = @()
$script:isStopping = $false

function Stop-DevProcessTree {
    param(
        [System.Diagnostics.Process]$Process,
        [string]$Name
    )

    if ($null -eq $Process -or $Process.HasExited) {
        return
    }

    Write-Host "Stopping $Name (PID $($Process.Id))..." -ForegroundColor Yellow
    & taskkill.exe /PID $Process.Id /T /F | Out-Null
}

function Stop-AllDevProcesses {
    foreach ($entry in $script:devProcesses) {
        Stop-DevProcessTree -Process $entry.Process -Name $entry.Name
    }
}

function Start-DevProcess {
    param(
        [string]$Name,
        [string]$FilePath,
        [string]$Arguments,
        [string]$WorkingDirectory
    )

    Write-Host "Starting $Name..." -ForegroundColor Cyan

    $process = Start-Process `
        -FilePath $FilePath `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -NoNewWindow `
        -PassThru

    $script:devProcesses += [pscustomobject]@{
        Name = $Name
        Process = $process
    }

    return $process
}

if (-not (Test-Path -LiteralPath $apiProject)) {
    throw "API project not found: $apiProject"
}

if (-not (Test-Path -LiteralPath (Join-Path $frontendDir "package.json"))) {
    throw "Frontend package.json not found: $frontendDir"
}

if (Test-Path -LiteralPath $localSettings) {
    Write-Host "Loading local development settings from scripts\Start-Dev.local.ps1" -ForegroundColor DarkCyan
    . $localSettings
}

$dotnetArguments = "run"
if ($NoRestore) {
    $dotnetArguments += " --no-restore"
}
$dotnetArguments += " --project `"$apiProject`" --launch-profile http"

$cancelHandler = [ConsoleCancelEventHandler]{
    param($sender, $eventArgs)

    $eventArgs.Cancel = $true

    if (-not $script:isStopping) {
        $script:isStopping = $true
        Write-Host ""
        Write-Host "Ctrl+C received. Stopping development servers..." -ForegroundColor Yellow
        Stop-AllDevProcesses
    }
}

[Console]::add_CancelKeyPress($cancelHandler)

try {
    Write-Host "Starting LobiLend development servers..." -ForegroundColor Cyan
    Write-Host "API:      http://127.0.0.1:5108" -ForegroundColor DarkCyan
    Write-Host "Frontend: http://127.0.0.1:5173" -ForegroundColor DarkCyan
    Write-Host "Press Ctrl+C in this window to stop both servers." -ForegroundColor DarkGray
    Write-Host ""

    Start-DevProcess -Name "API" -FilePath "dotnet" -Arguments $dotnetArguments -WorkingDirectory $root | Out-Null
    Start-DevProcess -Name "Frontend" -FilePath "npm.cmd" -Arguments "run dev -- --host 127.0.0.1" -WorkingDirectory $frontendDir | Out-Null

    while (-not $script:isStopping) {
        foreach ($entry in $script:devProcesses) {
            if ($entry.Process.HasExited) {
                $script:isStopping = $true
                Write-Host "$($entry.Name) exited with code $($entry.Process.ExitCode). Stopping remaining servers..." -ForegroundColor Yellow
                Stop-AllDevProcesses
                exit $entry.Process.ExitCode
            }
        }

        Start-Sleep -Milliseconds 500
    }
}
finally {
    [Console]::remove_CancelKeyPress($cancelHandler)
    Stop-AllDevProcesses
}
