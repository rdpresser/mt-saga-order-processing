param(
    [Parameter(Mandatory = $false)]
    [ValidateSet('smoke', 'load')]
    [string]$Scenario = 'smoke',

    [Parameter(Mandatory = $false)]
    [string]$BaseUrl
)

$ErrorActionPreference = 'Stop'

function Import-DotEnv {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    if (-not (Test-Path $Path)) {
        return @{}
    }

    $values = @{}
    foreach ($line in Get-Content -Path $Path) {
        $trimmed = $line.Trim()
        if ([string]::IsNullOrWhiteSpace($trimmed) -or $trimmed.StartsWith('#')) {
            continue
        }

        $separatorIndex = $trimmed.IndexOf('=')
        if ($separatorIndex -lt 1) {
            continue
        }

        $key = $trimmed.Substring(0, $separatorIndex).Trim()
        $value = $trimmed.Substring($separatorIndex + 1).Trim()
        if ([string]::IsNullOrWhiteSpace($key)) {
            continue
        }

        $values[$key] = $value
        if (-not (Test-Path "Env:$key")) {
            Set-Item -Path "Env:$key" -Value $value
        }
    }

    return $values
}

$dotenvPath = Join-Path -Path $PSScriptRoot -ChildPath '.env'
$dotenv = Import-DotEnv -Path $dotenvPath

$resolvedBaseUrl = if (-not [string]::IsNullOrWhiteSpace($BaseUrl)) {
    $BaseUrl
}
elseif ($dotenv.ContainsKey('BASE_URL') -and -not [string]::IsNullOrWhiteSpace($dotenv['BASE_URL'])) {
    $dotenv['BASE_URL']
}
elseif (-not [string]::IsNullOrWhiteSpace($env:BASE_URL)) {
    $env:BASE_URL
}
else {
    'http://localhost:5214'
}

$k6Command = Get-Command k6 -ErrorAction SilentlyContinue
if (-not $k6Command) {
    throw "k6 is not installed or not available in PATH."
}

$scriptPath = Join-Path -Path $PSScriptRoot -ChildPath "$Scenario.js"
if (-not (Test-Path $scriptPath)) {
    throw "Scenario script '$scriptPath' not found."
}

Write-Host "Running k6 scenario '$Scenario' against '$resolvedBaseUrl'..." -ForegroundColor Cyan

$env:BASE_URL = $resolvedBaseUrl
& k6 run $scriptPath

if ($LASTEXITCODE -ne 0) {
    throw "k6 run failed with exit code $LASTEXITCODE."
}

Write-Host "k6 scenario '$Scenario' completed successfully." -ForegroundColor Green
