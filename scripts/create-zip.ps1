# ══════════════════════════════════════════════════════════════════════════════
# MURO v2 — Proje ZIP Oluşturucu
# Gereksiz dosyaları hariç tutarak temiz bir paylaşım ZIP'i oluşturur.
# Kullanım: pwsh ./scripts/create-zip.ps1
# ══════════════════════════════════════════════════════════════════════════════

$ErrorActionPreference = "Stop"

$projectRoot = Split-Path -Parent (Split-Path -Parent $MyInvocation.MyCommand.Path)
$timestamp = Get-Date -Format "yyyyMMdd_HHmm"
$zipName = "muro-v2_$timestamp.zip"
$outputPath = Join-Path $env:USERPROFILE "Desktop\$zipName"
$tempDir = Join-Path $env:TEMP "muro-v2-export"

# Hariç tutulacak klasörler
$excludeDirs = @(
    "node_modules",
    "bin",
    "obj",
    ".next",
    ".vs",
    ".vscode",
    ".idea",
    ".git",
    ".gemini",
    "out",
    ".turbo"
)

# Hariç tutulacak dosya uzantıları
$excludeExtensions = @(
    ".log",
    ".suo",
    ".user",
    ".cache"
)

# Hariç tutulacak dosya desenleri
$excludePatterns = @(
    "build_*.txt",
    "test_*.txt",
    "migration_*.txt",
    "db_update.txt",
    "ef_update.txt",
    "errors.log",
    "api_build.log",
    "build.log",
    "*_out.txt"
)

Write-Host ""
Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║       MURO v2 — Temiz ZIP Oluşturucu            ║" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""

# Önceki temp klasörünü temizle
if (Test-Path $tempDir) {
    Remove-Item $tempDir -Recurse -Force
}

Write-Host "[1/4] Dosyalar kopyalanıyor..." -ForegroundColor Yellow

# Dosyaları filtreli kopyala
$files = Get-ChildItem -Path $projectRoot -Recurse -File | Where-Object {
    $relativePath = $_.FullName.Substring($projectRoot.Length + 1)
    $pathParts = $relativePath.Split([IO.Path]::DirectorySeparatorChar)
    
    # Klasör kontrolü
    $inExcludedDir = $false
    foreach ($part in $pathParts) {
        if ($excludeDirs -contains $part) {
            $inExcludedDir = $true
            break
        }
    }
    if ($inExcludedDir) { return $false }
    
    # Uzantı kontrolü
    if ($excludeExtensions -contains $_.Extension) { return $false }
    
    # Desen kontrolü
    foreach ($pattern in $excludePatterns) {
        if ($_.Name -like $pattern) { return $false }
    }
    
    return $true
}

$fileCount = 0
foreach ($file in $files) {
    $relativePath = $file.FullName.Substring($projectRoot.Length + 1)
    $destPath = Join-Path $tempDir $relativePath
    $destDir = Split-Path $destPath -Parent
    
    if (-not (Test-Path $destDir)) {
        New-Item -ItemType Directory -Path $destDir -Force | Out-Null
    }
    
    Copy-Item $file.FullName -Destination $destPath -Force
    $fileCount++
}

Write-Host "   $fileCount dosya kopyalandı." -ForegroundColor Green

# ZIP boyutunu hesapla
Write-Host "[2/4] ZIP oluşturuluyor..." -ForegroundColor Yellow

# Eski ZIP varsa sil
if (Test-Path $outputPath) {
    Remove-Item $outputPath -Force
}

Compress-Archive -Path "$tempDir\*" -DestinationPath $outputPath -CompressionLevel Optimal

$zipSize = (Get-Item $outputPath).Length
$zipSizeMB = [math]::Round($zipSize / 1MB, 1)

Write-Host "   ZIP boyutu: $zipSizeMB MB" -ForegroundColor Green

# Temizlik
Write-Host "[3/4] Geçici dosyalar temizleniyor..." -ForegroundColor Yellow
Remove-Item $tempDir -Recurse -Force
Write-Host "   Temizlik tamamlandı." -ForegroundColor Green

# Sonuç
Write-Host ""
Write-Host "[4/4] TAMAMLANDI!" -ForegroundColor Green
Write-Host ""
Write-Host "╔══════════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║  ZIP konumu: Masaüstü/$zipName" -ForegroundColor Cyan
Write-Host "║  Boyut: $zipSizeMB MB" -ForegroundColor Cyan
Write-Host "║  Dosya sayısı: $fileCount" -ForegroundColor Cyan
Write-Host "╚══════════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "Bu ZIP'i arkadaşına Google Drive, WeTransfer" -ForegroundColor White
Write-Host "veya USB ile gönderebilirsin." -ForegroundColor White
Write-Host ""
