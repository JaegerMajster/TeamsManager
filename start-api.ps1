#!/usr/bin/env pwsh

# TeamsManager API Auto-Start Script
# Automatycznie uruchamia API na porcie 7037 (HTTPS)

Write-Host "🚀 TeamsManager API Auto-Start" -ForegroundColor Green
Write-Host "================================" -ForegroundColor Green

# Sprawdź czy port 7037 jest już zajęty
$portCheck = netstat -ano | Select-String ":7037"
if ($portCheck) {
    Write-Host "⚠️  Port 7037 jest już zajęty:" -ForegroundColor Yellow
    $portCheck
    
    # Zapytaj czy zabić istniejący proces
    $response = Read-Host "Czy chcesz zabić istniejący proces? (y/N)"
    if ($response -eq 'y' -or $response -eq 'Y') {
        $processId = ($portCheck | Select-Object -First 1).ToString().Split()[-1]
        Write-Host "🔪 Zabijam proces PID: $processId" -ForegroundColor Red
        Stop-Process -Id $processId -Force -ErrorAction SilentlyContinue
        Start-Sleep -Seconds 2
    } else {
        Write-Host "❌ Anulowano uruchamianie API" -ForegroundColor Red
        exit 1
    }
}

# Sprawdź czy jesteśmy w odpowiednim katalogu
if (-not (Test-Path "TeamsManager.Api/TeamsManager.Api.csproj")) {
    Write-Host "❌ Nie znaleziono TeamsManager.Api.csproj" -ForegroundColor Red
    Write-Host "   Uruchom skrypt z głównego katalogu TeamsManager" -ForegroundColor Yellow
    exit 1
}

# Uruchom API z profilem HTTPS
Write-Host "🔧 Uruchamiam TeamsManager.Api z profilem HTTPS..." -ForegroundColor Cyan
Write-Host "   URL: https://localhost:7037" -ForegroundColor Cyan
Write-Host "   Swagger: https://localhost:7037/swagger" -ForegroundColor Cyan
Write-Host "" -ForegroundColor Cyan

try {
    # Uruchom w tle jako Job
    $job = Start-Job -ScriptBlock {
        Set-Location $args[0]
        dotnet run --project TeamsManager.Api --launch-profile https
    } -ArgumentList (Get-Location)
    
    Write-Host "✅ API uruchomione jako Job ID: $($job.Id)" -ForegroundColor Green
    
    # Czekaj na uruchomienie
    Write-Host "⏳ Czekam na uruchomienie API..." -ForegroundColor Yellow
    $timeout = 30
    $elapsed = 0
    
    do {
        Start-Sleep -Seconds 1
        $elapsed++
        $portCheck = netstat -ano | Select-String ":7037"
        
        if ($portCheck) {
            Write-Host "✅ API działa na porcie 7037!" -ForegroundColor Green
            Write-Host "🌐 Swagger UI: https://localhost:7037/swagger" -ForegroundColor Cyan
            
            # Test połączenia
            try {
                $response = Invoke-WebRequest -Uri "https://localhost:7037/swagger/index.html" -SkipCertificateCheck -TimeoutSec 5
                if ($response.StatusCode -eq 200) {
                    Write-Host "✅ API odpowiada poprawnie!" -ForegroundColor Green
                } else {
                    Write-Host "⚠️  API odpowiada z kodem: $($response.StatusCode)" -ForegroundColor Yellow
                }
            } catch {
                Write-Host "⚠️  Nie można przetestować API: $($_.Exception.Message)" -ForegroundColor Yellow
            }
            
            break
        }
        
        Write-Host "." -NoNewline -ForegroundColor Yellow
        
    } while ($elapsed -lt $timeout)
    
    if ($elapsed -ge $timeout) {
        Write-Host ""
        Write-Host "❌ Timeout! API nie uruchomiło się w ciągu $timeout sekund" -ForegroundColor Red
        
        # Sprawdź Job
        $jobState = Get-Job -Id $job.Id
        Write-Host "   Job State: $($jobState.State)" -ForegroundColor Yellow
        
        if ($jobState.State -eq "Failed") {
            Write-Host "   Job Output:" -ForegroundColor Yellow
            Receive-Job -Id $job.Id
        }
        
        Remove-Job -Id $job.Id -Force
        exit 1
    }
    
    Write-Host ""
    Write-Host "🎉 TeamsManager API uruchomione pomyślnie!" -ForegroundColor Green
    Write-Host "   Job ID: $($job.Id) - użyj 'Get-Job' aby sprawdzić status" -ForegroundColor Cyan
    Write-Host "   Aby zatrzymać: Stop-Job $($job.Id); Remove-Job $($job.Id)" -ForegroundColor Cyan
    
} catch {
    Write-Host "❌ Błąd podczas uruchamiania API: $($_.Exception.Message)" -ForegroundColor Red
    exit 1
} 