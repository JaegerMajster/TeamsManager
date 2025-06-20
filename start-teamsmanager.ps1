#!/usr/bin/env pwsh

# TeamsManager Complete System Start
# Uruchamia API + UI w odpowiedniej kolejności

param(
    [switch]$ApiOnly,
    [switch]$UiOnly,
    [switch]$SkipApiCheck
)

Write-Host "🚀 TeamsManager Complete System" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

# Sprawdź czy jesteśmy w odpowiednim katalogu
if (-not (Test-Path "TeamsManager.Api/TeamsManager.Api.csproj") -or -not (Test-Path "TeamsManager.UI/TeamsManager.UI.csproj")) {
    Write-Host "❌ Nie znaleziono projektów TeamsManager" -ForegroundColor Red
    Write-Host "   Uruchom skrypt z głównego katalogu TeamsManager" -ForegroundColor Yellow
    exit 1
}

if ($UiOnly) {
    Write-Host "🖥️  Uruchamiam tylko UI..." -ForegroundColor Cyan
    dotnet run --project TeamsManager.UI
    exit 0
}

# Krok 1: Uruchom API
if (-not $SkipApiCheck) {
    Write-Host "📡 Krok 1: Sprawdzanie API..." -ForegroundColor Cyan
    
    $portCheck = netstat -ano | Select-String ":7037"
    if ($portCheck) {
        Write-Host "✅ API już działa na porcie 7037" -ForegroundColor Green
    } else {
        Write-Host "🔧 Uruchamiam API..." -ForegroundColor Yellow
        
        # Uruchom skrypt start-api.ps1 jeśli istnieje
        if (Test-Path "start-api.ps1") {
            & .\start-api.ps1
        } else {
            # Fallback - uruchom bezpośrednio
            $job = Start-Job -ScriptBlock {
                Set-Location $args[0]
                dotnet run --project TeamsManager.Api --launch-profile https
            } -ArgumentList (Get-Location)
            
            Write-Host "⏳ Czekam na uruchomienie API..." -ForegroundColor Yellow
            $timeout = 30
            $elapsed = 0
            
            do {
                Start-Sleep -Seconds 1
                $elapsed++
                $portCheck = netstat -ano | Select-String ":7037"
                
                if ($portCheck) {
                    Write-Host "✅ API uruchomione!" -ForegroundColor Green
                    break
                }
                
                Write-Host "." -NoNewline -ForegroundColor Yellow
                
            } while ($elapsed -lt $timeout)
            
            if ($elapsed -ge $timeout) {
                Write-Host ""
                Write-Host "❌ API nie uruchomiło się w wyznaczonym czasie" -ForegroundColor Red
                Remove-Job -Id $job.Id -Force
                exit 1
            }
        }
    }
}

if ($ApiOnly) {
    Write-Host "📡 Uruchomiono tylko API" -ForegroundColor Green
    Write-Host "🌐 Swagger: https://localhost:7037/swagger" -ForegroundColor Cyan
    exit 0
}

# Krok 2: Sprawdź połączenie z API
Write-Host ""
Write-Host "🔗 Krok 2: Testowanie połączenia z API..." -ForegroundColor Cyan

try {
    $response = Invoke-WebRequest -Uri "https://localhost:7037/swagger/index.html" -SkipCertificateCheck -TimeoutSec 10
    if ($response.StatusCode -eq 200) {
        Write-Host "✅ API odpowiada poprawnie!" -ForegroundColor Green
    } else {
        Write-Host "⚠️  API odpowiada z kodem: $($response.StatusCode)" -ForegroundColor Yellow
    }
} catch {
    Write-Host "❌ Nie można połączyć się z API: $($_.Exception.Message)" -ForegroundColor Red
    Write-Host "   UI może mieć problemy z połączeniem" -ForegroundColor Yellow
}

# Krok 3: Uruchom UI
Write-Host ""
Write-Host "🖥️  Krok 3: Uruchamiam UI..." -ForegroundColor Cyan
Write-Host "   UI będzie łączyć się z API pod: https://localhost:7037" -ForegroundColor Gray

try {
    dotnet run --project TeamsManager.UI
} catch {
    Write-Host "❌ Błąd podczas uruchamiania UI: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 