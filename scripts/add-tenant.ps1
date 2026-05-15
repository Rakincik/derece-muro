<#
.SYNOPSIS
    Add a new tenant to MURO LMS — creates DB, runs migrations, seeds admin user.

.PARAMETER Name
    Institution name (e.g., "ABC KPSS Kursu")

.PARAMETER Code
    Short code (e.g., "abckpss") — used in DB name

.PARAMETER Subdomain
    Subdomain prefix (e.g., "abckpss" → abckpss.muro.com.tr)

.PARAMETER ServerGroup
    Target server group. Default: "srv-1"

.PARAMETER DbHost
    Database host. Default: "localhost"

.PARAMETER DbPort
    Database port. Default: 5432

.PARAMETER DbUser
    Database user. Default: "muro_user"

.PARAMETER DbPass
    Database password. Default: "muro_pass_2024"

.EXAMPLE
    .\add-tenant.ps1 -Name "ABC KPSS" -Code "abckpss" -Subdomain "abckpss"
#>
param(
    [Parameter(Mandatory)] [string] $Name,
    [Parameter(Mandatory)] [string] $Code,
    [Parameter(Mandatory)] [string] $Subdomain,
    [string] $ServerGroup = "srv-1",
    [string] $DbHost = "localhost",
    [int]    $DbPort = 5432,
    [string] $DbUser = "muro_user",
    [string] $DbPass = "muro_pass_2024"
)

$ErrorActionPreference = "Stop"
$dbName = "muro_$($Code.ToLower())"
$connStr = "Host=$DbHost;Port=$DbPort;Database=$dbName;Username=$DbUser;Password=$DbPass"

Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   MURO LMS — Tenant Provisioning           ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Kurum:     $Name" -ForegroundColor White
Write-Host "  Kod:       $Code" -ForegroundColor White
Write-Host "  Subdomain: $Subdomain" -ForegroundColor White
Write-Host "  DB:        $dbName" -ForegroundColor White
Write-Host "  Sunucu:    $ServerGroup" -ForegroundColor White
Write-Host ""

# Step 1: Create database
Write-Host "[1/4] Veritabanı oluşturuluyor: $dbName ..." -ForegroundColor Yellow
try {
    $checkDb = psql -h $DbHost -p $DbPort -U $DbUser -tAc "SELECT 1 FROM pg_database WHERE datname='$dbName'" 2>$null
    if ($checkDb -eq "1") {
        Write-Host "  ⚠ Veritabanı zaten mevcut, atlıyorum." -ForegroundColor DarkYellow
    } else {
        psql -h $DbHost -p $DbPort -U $DbUser -c "CREATE DATABASE $dbName OWNER $DbUser" 2>$null
        Write-Host "  ✅ Veritabanı oluşturuldu." -ForegroundColor Green
    }
} catch {
    Write-Host "  ❌ DB oluşturulamadı: $_" -ForegroundColor Red
    exit 1
}

# Step 2: Run migrations
Write-Host "[2/4] Migration uygulanıyor..." -ForegroundColor Yellow
$apiDir = Join-Path $PSScriptRoot "..\src\MURO.API"
$infraDir = Join-Path $PSScriptRoot "..\src\MURO.Infrastructure"

Push-Location $apiDir
try {
    $env:ConnectionStrings__DefaultConnection = $connStr
    dotnet ef database update --project $infraDir --startup-project . 2>&1 | Out-Null
    Write-Host "  ✅ Migration tamamlandı." -ForegroundColor Green
} catch {
    Write-Host "  ❌ Migration hatası: $_" -ForegroundColor Red
    exit 1
} finally {
    Pop-Location
    Remove-Item Env:\ConnectionStrings__DefaultConnection -ErrorAction SilentlyContinue
}

# Step 3: Register tenant in master DB
Write-Host "[3/4] Master DB'ye tenant kaydediliyor..." -ForegroundColor Yellow
$tenantId = [guid]::NewGuid().ToString()
$now = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")

$insertSql = @"
INSERT INTO "Tenants" ("Id", "Name", "Code", "Subdomain", "ConnectionString", "ServerGroup", "IsActive", "CreatedAt", "PrimaryColor")
VALUES ('$tenantId', '$Name', '$Code', '$Subdomain', '$connStr', '$ServerGroup', true, '$now', '#1B3B6F')
ON CONFLICT ("Id") DO NOTHING;
"@

try {
    $masterConn = "Host=$DbHost;Port=$DbPort;Database=muro_dev;Username=$DbUser;Password=$DbPass"
    psql "$masterConn" -c $insertSql 2>$null
    Write-Host "  ✅ Tenant kaydedildi: $tenantId" -ForegroundColor Green
} catch {
    Write-Host "  ⚠ Master DB kaydı atlandı: $_" -ForegroundColor DarkYellow
}

# Step 4: Summary
Write-Host ""
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   ✅ Kurulum Tamamlandı!                   ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host ""
Write-Host "  Tenant ID:   $tenantId" -ForegroundColor White
Write-Host "  URL:         https://$Subdomain.muro.com.tr" -ForegroundColor White
Write-Host "  DB:          $dbName" -ForegroundColor White
Write-Host "  Conn:        $connStr" -ForegroundColor DarkGray
Write-Host ""
Write-Host "  Sonraki adım: Admin kullanıcı oluşturun." -ForegroundColor Cyan
