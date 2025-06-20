#!/usr/bin/env pwsh

# TeamsManager API Stop Script
# Zatrzymuje wszystkie instancje API na porcie 7037

Write-Host "🛑 TeamsManager API Stop" -ForegroundColor Red
Write-Host "========================" -ForegroundColor Red

# Sprawdź czy port 7037 jest zajęty
$portCheck = netstat -ano | Select-String ":7037"

if (-not $portCheck) {
    Write-Host "✅ Port 7037 jest wolny - API nie działa" -ForegroundColor Green
    exit 0
}

Write-Host "🔍 Znaleziono procesy na porcie 7037:" -ForegroundColor Yellow
$portCheck

# Zatrzymaj wszystkie procesy na porcie 7037
$processIds = @()
foreach ($line in $portCheck) {
    $processId = $line.ToString().Split()[-1]
    if ($processId -and $processId -match '^\d+$') {
        $processIds += $processId
    }
}

$processIds = $processIds | Select-Object -Unique

foreach ($processId in $processIds) {
    try {
        $process = Get-Process -Id $processId -ErrorAction SilentlyContinue
        if ($process) {
            Write-Host "🔪 Zatrzymuję proces: PID $processId ($($process.ProcessName))" -ForegroundColor Red
            Stop-Process -Id $processId -Force
        }
    } catch {
        Write-Host "⚠️  Nie można zatrzymać procesu PID $processId`: $($_.Exception.Message)" -ForegroundColor Yellow
    }
}

# Zatrzymaj PowerShell Jobs związane z TeamsManager
$jobs = Get-Job -ErrorAction SilentlyContinue | Where-Object { 
    $_.Command -like "*TeamsManager*" -or $_.Command -like "*dotnet run*" 
}

if ($jobs) {
    Write-Host "🔪 Zatrzymuję PowerShell Jobs:" -ForegroundColor Red
    foreach ($job in $jobs) {
        Write-Host "   Job ID: $($job.Id) - $($job.State)" -ForegroundColor Yellow
        Stop-Job -Id $job.Id -ErrorAction SilentlyContinue
        Remove-Job -Id $job.Id -Force -ErrorAction SilentlyContinue
    }
}

# Sprawdź wynik
Start-Sleep -Seconds 2
$finalCheck = netstat -ano | Select-String ":7037"

if (-not $finalCheck) {
    Write-Host "✅ Port 7037 jest teraz wolny!" -ForegroundColor Green
} else {
    Write-Host "⚠️  Nadal są procesy na porcie 7037:" -ForegroundColor Yellow
    $finalCheck
    Write-Host "   Może być potrzebny restart systemu lub zabicie procesów ręcznie" -ForegroundColor Yellow
} 