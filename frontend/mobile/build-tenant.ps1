<#
.SYNOPSIS
    Build a white-label APK for a specific tenant.
    Modifies capacitor.config.ts, syncs, and builds the Android project.

.PARAMETER Tenant
    Tenant code (must match a file in tenants/ folder, e.g., "abckpss")

.PARAMETER Release
    If set, builds a release APK. Otherwise, builds debug.

.EXAMPLE
    .\build-tenant.ps1 -Tenant abckpss
    .\build-tenant.ps1 -Tenant abckpss -Release
#>
param(
    [Parameter(Mandatory)] [string] $Tenant,
    [switch] $Release
)

$ErrorActionPreference = "Stop"
$mobileDir = $PSScriptRoot
$configPath = Join-Path $mobileDir "tenants\$Tenant.json"

if (-not (Test-Path $configPath)) {
    Write-Host "❌ Tenant config bulunamadı: $configPath" -ForegroundColor Red
    exit 1
}

$config = Get-Content $configPath | ConvertFrom-Json

Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Cyan
Write-Host "║   MURO Mobile — White-Label Build          ║" -ForegroundColor Cyan
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Cyan
Write-Host ""
Write-Host "  Kurum:     $($config.appName)" -ForegroundColor White
Write-Host "  Bundle ID: $($config.appId)" -ForegroundColor White
Write-Host "  Server:    $($config.serverUrl)" -ForegroundColor White
Write-Host ""

# Step 1: Update capacitor.config.ts
Write-Host "[1/4] Capacitor config güncelleniyor..." -ForegroundColor Yellow

$capConfig = @"
import type { CapacitorConfig } from '@capacitor/cli';

const config: CapacitorConfig = {
  appId: '$($config.appId)',
  appName: '$($config.appName)',
  webDir: 'www',
  server: {
    url: '$($config.serverUrl)',
    cleartext: $( if ($config.serverUrl -match "^http://") { "true" } else { "false" } )
  },
  plugins: {
    SplashScreen: {
      launchShowDuration: 2000,
      launchAutoHide: true,
      backgroundColor: '$($config.splashBackgroundColor)',
      showSpinner: false
    },
    StatusBar: {
      style: 'DARK',
      backgroundColor: '$($config.statusBarColor)'
    },
    PushNotifications: {
      presentationOptions: ['badge', 'sound', 'alert']
    }
  },
  android: {
    allowMixedContent: $( if ($config.serverUrl -match "^http://") { "true" } else { "false" } ),
    backgroundColor: '$($config.splashBackgroundColor)'
  }
};

export default config;
"@

Set-Content -Path (Join-Path $mobileDir "capacitor.config.ts") -Value $capConfig -Encoding UTF8
Write-Host "  ✅ Config güncellendi." -ForegroundColor Green

# Step 2: Update Android strings.xml (app name)
Write-Host "[2/4] Android app adı güncelleniyor..." -ForegroundColor Yellow
$stringsPath = Join-Path $mobileDir "android\app\src\main\res\values\strings.xml"
if (Test-Path $stringsPath) {
    $stringsXml = Get-Content $stringsPath -Raw
    $stringsXml = $stringsXml -replace '(?<=<string name="app_name">).*?(?=</string>)', $config.appName
    $stringsXml = $stringsXml -replace '(?<=<string name="title_activity_main">).*?(?=</string>)', $config.appName
    Set-Content -Path $stringsPath -Value $stringsXml -Encoding UTF8
    Write-Host "  ✅ App adı: $($config.appName)" -ForegroundColor Green
}

# Step 3: Sync Capacitor
Write-Host "[3/4] Capacitor sync..." -ForegroundColor Yellow
Push-Location $mobileDir
npx cap sync android 2>&1 | Out-Null
Pop-Location
Write-Host "  ✅ Sync tamamlandı." -ForegroundColor Green

# Step 4: Build APK
Write-Host "[4/4] APK build ediliyor..." -ForegroundColor Yellow
$gradlew = Join-Path $mobileDir "android\gradlew.bat"

if ($Release) {
    & $gradlew -p (Join-Path $mobileDir "android") assembleRelease 2>&1 | Out-Null
    $apkSource = Join-Path $mobileDir "android\app\build\outputs\apk\release\app-release.apk"
}
else {
    & $gradlew -p (Join-Path $mobileDir "android") assembleDebug 2>&1 | Out-Null
    $apkSource = Join-Path $mobileDir "android\app\build\outputs\apk\debug\app-debug.apk"
}

# Copy APK to output
$outputDir = Join-Path $mobileDir "output"
New-Item -ItemType Directory -Force -Path $outputDir | Out-Null
$apkDest = Join-Path $outputDir "$Tenant.apk"

if (Test-Path $apkSource) {
    Copy-Item $apkSource $apkDest -Force
    $size = [math]::Round((Get-Item $apkDest).Length / 1MB, 1)
    Write-Host "  ✅ APK hazır: $apkDest ($size MB)" -ForegroundColor Green
}
else {
    Write-Host "  ❌ APK bulunamadı. Gradle build loglarını kontrol edin." -ForegroundColor Red
    exit 1
}

# Summary
Write-Host ""
Write-Host "╔════════════════════════════════════════════╗" -ForegroundColor Green
Write-Host "║   ✅ Build Tamamlandı!                      ║" -ForegroundColor Green
Write-Host "╚════════════════════════════════════════════╝" -ForegroundColor Green
Write-Host "  APK: $apkDest" -ForegroundColor White
Write-Host "  Boyut: $size MB" -ForegroundColor White
Write-Host ""
Write-Host "  Test: adb install $apkDest" -ForegroundColor Cyan
