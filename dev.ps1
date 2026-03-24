param(
    [Parameter(Position = 0)]
    [ValidateSet("up", "down", "build", "test", "clean", "migrate")]
    [string]$Command = "help"
)

switch ($Command) {
    "up" {
        Write-Host "Starting infrastructure..." -ForegroundColor Cyan
        docker compose up -d --wait
    }
    "down" {
        Write-Host "Stopping infrastructure..." -ForegroundColor Cyan
        docker compose down -v
    }
    "build" {
        Write-Host "Building..." -ForegroundColor Cyan
        dotnet build --configuration Release
    }
    "test" {
        Write-Host "Starting infrastructure..." -ForegroundColor Cyan
        docker compose up -d --wait
        Write-Host "Running tests..." -ForegroundColor Cyan
        dotnet test --configuration Release --logger "console;verbosity=normal"
    }
    "clean" {
        Write-Host "Cleaning..." -ForegroundColor Cyan
        docker compose down -v
        dotnet clean
    }
    "migrate" {
        Write-Host "Applying EF Core migrations..." -ForegroundColor Cyan
        dotnet ef database update --project .\src\MT.Saga.OrderProcessing.Infrastructure\MT.Saga.OrderProcessing.Infrastructure.csproj --startup-project .\src\Services\MT.Saga.OrderProcessing.OrderService\MT.Saga.OrderProcessing.OrderService.csproj --context OrderSagaDbContext
    }
    default {
        Write-Host ""
        Write-Host "Usage: .\dev.ps1 <command>" -ForegroundColor Yellow
        Write-Host ""
        Write-Host "Commands:"
        Write-Host "  up      Start infrastructure (RabbitMQ + PostgreSQL)"
        Write-Host "  down    Stop infrastructure"
        Write-Host "  build   Build the application"
        Write-Host "  test    Start infrastructure and run tests"
        Write-Host "  migrate Apply EF Core migrations for saga/outbox context"
        Write-Host "  clean   Stop infrastructure and clean build"
        Write-Host ""
    }
}
