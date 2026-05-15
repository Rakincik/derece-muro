<#
.SYNOPSIS
    Apply EF Core migrations to ALL tenant databases.
    Reads tenant list from master DB and runs `dotnet ef database update` for each.

.PARAMETER DbHost
    Master database host. Default: "localhost"

.PARAMETER DbPort
    Master database port. Default: 5432

.PARAMETER DbUser
    Database user. Default: "muro_user"

.PARAMETER DbPass
    Database password. Default: "muro_pass_2024"

.EXAMPLE
    .\migrate-all.ps1
#>
param(
    [string] $DbHost = "localhost",
    [int]    $DbPort = 5432,
    [string] $DbUser = "muro_user",
    [string] $DbPass = "muro_pass_2024",
    [string] $MasterDb = "muro_dev"
)

$ErrorActionPreference = "Stop"
$apiDir = Join-Path $PSScriptRoot "..\src\MURO.API"
$infraDir = Join-Path $PSScriptRoot "..\src\MURO.Infrastructure"

Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   MURO LMS — Migrate All Tenants           ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Step 1: Migrate master DB first
Write-Host "[Master DB] $MasterDb" -ForegroundColor Yellow
Push-Location $apiDir
try {
    $env:ConnectionStrings__DefaultConnection = "Host=$DbHost;Port=$DbPort;Database=$MasterDb;Username=$DbUser;Password=$DbPass"
    dotnet ef database update --project $infraDir --startup-project . 2>&1 | Out-Null
    Write-Host "  ✅ Master DB güncellendi." -ForegroundColor Green
} catch {
    Write-Host "  ❌ Master DB hatası: $_" -ForegroundColor Red
} finally {
    Pop-Location
}

# Step 2: Get all tenant connection strings
Write-Host ""
Write-Host "Tenant listesi alınıyor..." -ForegroundColor Yellow

$masterConn = "Host=$DbHost;Port=$DbPort;Database=$MasterDb;Username=$DbUser;Password=$DbPass"
$tenants = psql "$masterConn" -tAc "SELECT ""Name"" || '|' || ""ConnectionString"" FROM ""Tenants"" WHERE ""IsActive"" = true AND ""ConnectionString"" IS NOT NULL" 2>$null

if (-not $tenants) {
    Write-Host "  Aktif tenant bulunamadı (veya ConnectionString null)." -ForegroundColor DarkYellow
    Write-Host "  Sadece master DB güncellendi." -ForegroundColor DarkYellow
    exit 0
}

$tenantList = $tenants -split "`n" | Where-Object { $_ -match "\|" }
$total = $tenantList.Count
$success = 0
$failed = 0

Write-Host "  $total tenant bulundu." -ForegroundColor White
Write-Host ""

# Step 3: Migrate each tenant
foreach ($entry in $tenantList) {
    $parts = $entry -split "\|", 2
    $name = $parts[0].Trim()
    $connStr = $parts[1].Trim()

    Write-Host "[$($success + $failed + 1)/$total] $name" -ForegroundColor Yellow -NoNewline

    Push-Location $apiDir
    try {
        $env:ConnectionStrings__DefaultConnection = $connStr
        dotnet ef database update --project $infraDir --startup-project . 2>&1 | Out-Null
        Write-Host " ✅" -ForegroundColor Green
        $success++
    } catch {
        Write-Host " ❌ $_" -ForegroundColor Red
        $failed++
    } finally {
        Pop-Location
    }
}

# Summary
Write-Host ""
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "║   Migration Tamamlandı                     ║" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor $(if ($failed -eq 0) { "Green" } else { "Yellow" })
Write-Host "  Başarılı: $success / $total" -ForegroundColor Green
if ($failed -gt 0) {
    Write-Host "  Başarısız: $failed / $total" -ForegroundColor Red
}

Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
