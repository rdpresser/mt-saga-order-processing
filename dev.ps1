param(
    [Parameter(Position = 0)]
    [ValidateSet("up", "down", "build", "test", "clean", "migrate")]
    [string]$Command = "help"
)

$AppHostProject = Join-Path $PSScriptRoot "src/MT.Saga.AppHost.Aspire/MT.Saga.AppHost.Aspire.csproj"
$InfrastructureProject = Join-Path $PSScriptRoot "src/MT.Saga.OrderProcessing.Infrastructure/MT.Saga.OrderProcessing.Infrastructure.csproj"
$OrderServiceProject = Join-Path $PSScriptRoot "src/Services/MT.Saga.OrderProcessing.OrderService/MT.Saga.OrderProcessing.OrderService.csproj"
$DevArtifactsPath = Join-Path $PSScriptRoot ".dev"
$AppHostPidPath = Join-Path $DevArtifactsPath "apphost.pid"
$AppHostStdOutPath = Join-Path $DevArtifactsPath "apphost.stdout.log"
$AppHostStdErrPath = Join-Path $DevArtifactsPath "apphost.stderr.log"

function Get-AppHostProcess {
    if (-not (Test-Path $AppHostPidPath)) {
        return $null
    }

    $rawPid = (Get-Content $AppHostPidPath -Raw).Trim()
    if (-not [int]::TryParse($rawPid, [ref]$null)) {
        Remove-Item $AppHostPidPath -Force -ErrorAction SilentlyContinue
        return $null
    }

    try {
        return Get-Process -Id ([int]$rawPid) -ErrorAction Stop
    }
    catch {
        Remove-Item $AppHostPidPath -Force -ErrorAction SilentlyContinue
        return $null
    }
}

function Start-AppHost {
    $existingProcess = Get-AppHostProcess
    if ($null -ne $existingProcess) {
        Write-Host "Aspire AppHost is already running (PID $($existingProcess.Id))." -ForegroundColor Yellow
        return
    }

    New-Item -ItemType Directory -Path $DevArtifactsPath -Force | Out-Null

    $process = Start-Process dotnet -ArgumentList @("run", "--project", $AppHostProject) -WorkingDirectory $PSScriptRoot -RedirectStandardOutput $AppHostStdOutPath -RedirectStandardError $AppHostStdErrPath -PassThru
    Set-Content -Path $AppHostPidPath -Value $process.Id

    Write-Host "Aspire AppHost started (PID $($process.Id))." -ForegroundColor Green
    Write-Host "stdout: $AppHostStdOutPath" -ForegroundColor DarkGray
    Write-Host "stderr: $AppHostStdErrPath" -ForegroundColor DarkGray
}

function Stop-AppHost {
    $existingProcess = Get-AppHostProcess
    if ($null -eq $existingProcess) {
        Write-Host "Aspire AppHost is not running." -ForegroundColor Yellow
        return
    }

    Stop-Process -Id $existingProcess.Id -Force
    Remove-Item $AppHostPidPath -Force -ErrorAction SilentlyContinue
    Write-Host "Aspire AppHost stopped (PID $($existingProcess.Id))." -ForegroundColor Green
}

switch ($Command) {
    "up" {
        Write-Host "Starting Aspire AppHost..." -ForegroundColor Cyan
        Start-AppHost
    }
    "down" {
        Write-Host "Stopping Aspire AppHost..." -ForegroundColor Cyan
        Stop-AppHost
    }
    "build" {
        Write-Host "Building..." -ForegroundColor Cyan
        dotnet build --configuration Release
    }
    "test" {
        Write-Host "Running tests..." -ForegroundColor Cyan
        Write-Host "Test infrastructure is provisioned by the test suite via Testcontainers." -ForegroundColor DarkGray
        dotnet test --configuration Release --logger "console;verbosity=normal"
    }
    "clean" {
        Write-Host "Cleaning..." -ForegroundColor Cyan
        Stop-AppHost
        dotnet clean
    }
    "migrate" {
        Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan
        dotnet ef database update --project $InfrastructureProject --startup-project $OrderServiceProject --context OrderSagaDbContext
    }
    default {
        Write-Host ""
        Write-Host "Usage: ./dev.ps1 <command>" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Commands:"
        Write-Host "  up      Start local orchestration through the Aspire AppHost"
        Write-Host "  down    Stop the Aspire AppHost"
        Write-Host "  build   Build the application"
        Write-Host "  test    Run tests (Testcontainers provisions dependencies as needed)"
        Write-Host "  migrate Apply EF Core migrations for saga/outbox context"
        Write-Host "  clean   Stop the AppHost and clean build outputs"
        Write-Host ""
    }
}
