# 🚀 Instrukcje Uruchamiania TeamsManager

## 📋 Przegląd

TeamsManager składa się z dwóch głównych komponentów:
- **API** (TeamsManager.Api) - backend na porcie `https://localhost:7037`
- **UI** (TeamsManager.UI) - frontend WPF

## ⚡ Szybkie Uruchamianie

### Opcja 1: Automatyczne uruchamianie (ZALECANE)
```powershell
# Uruchom cały system (API + UI)
.\start-teamsmanager.ps1

# Tylko API
.\start-teamsmanager.ps1 -ApiOnly

# Tylko UI (jeśli API już działa)
.\start-teamsmanager.ps1 -UiOnly
```

### Opcja 2: Ręczne uruchamianie

#### 1. Uruchom API
```powershell
# Metoda A: Używając skryptu
.\start-api.ps1

# Metoda B: Bezpośrednio
dotnet run --project TeamsManager.Api --launch-profile https
```

#### 2. Uruchom UI
```powershell
dotnet run --project TeamsManager.UI
```

## 🛠️ Zarządzanie Procesami

### Zatrzymywanie
```powershell
# Zatrzymaj wszystkie procesy TeamsManager
.\stop-api.ps1

# Sprawdź status portów
netstat -ano | Select-String ":7037"
```

### Sprawdzanie Statusu
```powershell
# Sprawdź czy API działa
curl -k https://localhost:7037/swagger/index.html

# Sprawdź procesy dotnet
Get-Process -Name "dotnet"
```

## 🔧 Rozwiązywanie Problemów

### Problem: Port 7037 zajęty
```powershell
# Automatyczne rozwiązanie
.\stop-api.ps1
.\start-api.ps1

# Ręczne sprawdzenie
netstat -ano | Select-String ":7037"
# Zabij proces: Stop-Process -Id [PID] -Force
```

### Problem: Certyfikat HTTPS
```powershell
# Sprawdź certyfikat
dotnet dev-certs https --check --trust

# Jeśli potrzeba, zainstaluj ponownie
dotnet dev-certs https --clean
dotnet dev-certs https --trust
```

### Problem: "System Graph API nie jest gotowy"
1. **Sprawdź czy API działa:**
   ```powershell
   curl -k https://localhost:7037/swagger/index.html
   ```

2. **Sprawdź konfigurację Azure AD:**
   - Uruchom UI
   - Przejdź do konfiguracji Azure AD
   - Sprawdź czy wszystkie pola są wypełnione

3. **Sprawdź logi diagnostyczne:**
   - W UI przejdź do menu "Diagnostyka Graph API"
   - Sprawdź szczegółowe logi

## 📁 Struktura Portów

| Komponent | Port | URL |
|-----------|------|-----|
| API HTTPS | 7037 | https://localhost:7037 |
| API HTTP | 5182 | http://localhost:5182 |
| Swagger UI | 7037 | https://localhost:7037/swagger |

## 🔍 Logi i Diagnostyka

### Logi API
- Wyświetlane w konsoli podczas uruchamiania
- Poziom: Information, Warning, Error

### Logi UI
- Wyświetlane w Output window Visual Studio
- Pliki logów w: `%LOCALAPPDATA%\TeamsManager\Logs\`

### Narzędzie Diagnostyczne
- W UI: Menu → "Diagnostyka Graph API"
- Pokazuje szczegółowe informacje o połączeniu z Microsoft Graph

## 🚨 Typowe Błędy

| Błąd | Przyczyna | Rozwiązanie |
|------|-----------|-------------|
| `System.InvalidOperationException` w HTTP | API nie działa | Uruchom API: `.\start-api.ps1` |
| Port 7037 zajęty | Poprzednia instancja API | `.\stop-api.ps1` |
| Błąd certyfikatu HTTPS | Brak zaufanego certyfikatu | `dotnet dev-certs https --trust` |
| "Brak konfiguracji Azure AD" | Niekompletna konfiguracja | Skonfiguruj Azure AD w UI |

## 📞 Wsparcie

Jeśli problemy nadal występują:

1. **Sprawdź logi** - szczególnie komunikaty `[DIAGNOSTIC]`
2. **Uruchom diagnostykę** - narzędzie w UI
3. **Restart systemu** - czasami pomaga przy problemach z portami
4. **Sprawdź dokumentację** - w folderze `docs/`

## 💡 Wskazówki

- **Zawsze uruchamiaj API przed UI**
- **Używaj skryptów PowerShell** - są bardziej niezawodne
- **Sprawdzaj porty** przed uruchamianiem
- **Konfiguruj Azure AD w UI** przed pierwszym użyciem 