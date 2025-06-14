# Plan Refaktoryzacji PowerShell → Graph API

## 📋 **Analiza Obecnego Stanu**

### **🔍 Zidentyfikowane Komponenty PowerShell**

#### **1. Serwisy PowerShell (Core)**
- `PowerShellConnectionService` - zarządzanie połączeniem i runspace
- `PowerShellTeamManagementService` - operacje na zespołach Teams
- `PowerShellUserManagementService` - zarządzanie użytkownikami
- `PowerShellBulkOperationsService` - operacje masowe
- `PowerShellCacheService` - cache dla PowerShell
- `PowerShellUserResolverService` - rozwiązywanie ID użytkowników
- `PowerShellService` - fasada główna

#### **2. Interfejsy PowerShell**
- `IPowerShellConnectionService`
- `IPowerShellTeamManagementService` 
- `IPowerShellUserManagementService`
- `IPowerShellBulkOperationsService`
- `IPowerShellCacheService`
- `IPowerShellUserResolverService`
- `IPowerShellService`

#### **3. Modele PowerShell**
- `PowerShellDiagnosticInfo`
- `PowerShellPermissionInfo`
- `PowerShellModuleStatus`
- `PowerShellModuleInstallationResult`
- `PowerShellConnectionTestResult`
- `ConnectionHealthInfo`

#### **4. Wyjątki PowerShell**
- `PowerShellConnectionException`
- `PowerShellCommandExecutionException`

#### **5. Helpery PowerShell**
- `PowerShellCommandBuilder`
- `PSParameterValidator`
- `PSObjectMapper`

#### **6. Serwisy Używające PowerShell**
- `ChannelService` - używa `IPowerShellService`
- `GraphAdminNotificationService` - używa `IPowerShellService`
- `OrganizationalUnitService` - używa `IPowerShellCacheService`

#### **7. UI/API Integracje**
- `TeamsManagerApiService` - endpointy diagnostyczne PowerShell
- `TeamsManagerMonitoringService` - monitoring PowerShell
- `DiagnosticsController` - API endpointy PowerShell

### **🎯 Istniejące Komponenty Graph API**

#### **Gotowe do Wykorzystania:**
- `IModernHttpService` - HTTP client z resilience dla Graph API
- `TokenManager` - zarządzanie tokenami OBO flow
- `GraphUserProfileService` - przykład implementacji Graph API
- `IConfidentialClientApplication` - MSAL dla Graph API
- Konfiguracja HttpClient z resilience patterns

---

## 🚀 **TASKI REFAKTORYZACJI**

### **ETAP 1: Przygotowanie Infrastruktury Graph API** ⏱️ **2 dni**

#### **1.1 Stworzenie Interfejsów Graph**
- [ ] **TASK 1.1.1:** Utworzyć folder `TeamsManager.Core/Abstractions/Services/Graph/`
- [ ] **TASK 1.1.2:** Utworzyć `IGraphTeamManagementService.cs`
- [ ] **TASK 1.1.3:** Utworzyć `IGraphUserManagementService.cs`
- [ ] **TASK 1.1.4:** Utworzyć `IGraphBulkOperationsService.cs`
- [ ] **TASK 1.1.5:** Utworzyć `IGraphConnectionService.cs`
- [ ] **TASK 1.1.6:** Utworzyć `IGraphCacheService.cs`
- [ ] **TASK 1.1.7:** Utworzyć `IGraphService.cs` (fasada)

#### **1.2 Stworzenie Modeli Graph**
- [ ] **TASK 1.2.1:** Utworzyć folder `TeamsManager.Core/Models/Graph/`
- [ ] **TASK 1.2.2:** Utworzyć `GraphDiagnosticInfo.cs`
- [ ] **TASK 1.2.3:** Utworzyć `GraphPermissionInfo.cs`
- [ ] **TASK 1.2.4:** Utworzyć `GraphConnectionTestResult.cs`
- [ ] **TASK 1.2.5:** Utworzyć `GraphOperationResult.cs`
- [ ] **TASK 1.2.6:** Utworzyć `GraphTeam.cs`
- [ ] **TASK 1.2.7:** Utworzyć `GraphUser.cs`
- [ ] **TASK 1.2.8:** Utworzyć `GraphChannel.cs`
- [ ] **TASK 1.2.9:** Utworzyć `GraphBulkResult.cs`

#### **1.3 Rozszerzenie ModernHttpService**
- [ ] **TASK 1.3.1:** Dodać metody Teams API do `IModernHttpService`
- [ ] **TASK 1.3.2:** Dodać metody Users API do `IModernHttpService`
- [ ] **TASK 1.3.3:** Dodać metody Groups API do `IModernHttpService`
- [ ] **TASK 1.3.4:** Implementować batch operations w `ModernHttpService`

#### **1.4 Stworzenie Graph Exceptions**
- [ ] **TASK 1.4.1:** Utworzyć folder `TeamsManager.Core/Exceptions/Graph/`
- [ ] **TASK 1.4.2:** Utworzyć `GraphConnectionException.cs`
- [ ] **TASK 1.4.3:** Utworzyć `GraphApiException.cs`
- [ ] **TASK 1.4.4:** Utworzyć `GraphBulkOperationException.cs`

---

### **ETAP 2: Implementacja Graph Services** ⏱️ **4 dni**

#### **2.1 GraphConnectionService**
- [ ] **TASK 2.1.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphConnectionService.cs`
- [ ] **TASK 2.1.2:** Implementować zarządzanie tokenami Graph API
- [ ] **TASK 2.1.3:** Implementować diagnostykę połączenia Graph
- [ ] **TASK 2.1.4:** Implementować walidację uprawnień Graph
- [ ] **TASK 2.1.5:** Implementować health check Graph API
- [ ] **TASK 2.1.6:** Napisać testy jednostkowe dla `GraphConnectionService`

#### **2.2 GraphTeamManagementService**
- [ ] **TASK 2.2.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphTeamManagementService.cs`
- [ ] **TASK 2.2.2:** Implementować `POST /v1.0/teams` - tworzenie zespołów
- [ ] **TASK 2.2.3:** Implementować `PATCH /v1.0/teams/{id}` - aktualizacja zespołów
- [ ] **TASK 2.2.4:** Implementować `GET /v1.0/teams` - pobieranie zespołów
- [ ] **TASK 2.2.5:** Implementować `POST /v1.0/teams/{id}/channels` - tworzenie kanałów
- [ ] **TASK 2.2.6:** Implementować `POST /v1.0/teams/{id}/members` - dodawanie członków
- [ ] **TASK 2.2.7:** Implementować `DELETE /v1.0/teams/{id}/members/{userId}` - usuwanie członków
- [ ] **TASK 2.2.8:** Napisać testy jednostkowe dla `GraphTeamManagementService`

#### **2.3 GraphUserManagementService**
- [ ] **TASK 2.3.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphUserManagementService.cs`
- [ ] **TASK 2.3.2:** Implementować `POST /v1.0/users` - tworzenie użytkowników
- [ ] **TASK 2.3.3:** Implementować `PATCH /v1.0/users/{id}` - aktualizacja użytkowników
- [ ] **TASK 2.3.4:** Implementować `GET /v1.0/users` - pobieranie użytkowników
- [ ] **TASK 2.3.5:** Implementować `POST /v1.0/users/{id}/assignLicense` - przypisywanie licencji
- [ ] **TASK 2.3.6:** Implementować `POST /v1.0/users/{id}/revokeSignInSessions` - wylogowanie
- [ ] **TASK 2.3.7:** Napisać testy jednostkowe dla `GraphUserManagementService`

#### **2.4 GraphBulkOperationsService**
- [ ] **TASK 2.4.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphBulkOperationsService.cs`
- [ ] **TASK 2.4.2:** Implementować batch requests Graph API (`POST /v1.0/$batch`)
- [ ] **TASK 2.4.3:** Implementować parallel processing z rate limiting
- [ ] **TASK 2.4.4:** Implementować retry logic dla bulk operations
- [ ] **TASK 2.4.5:** Implementować progress reporting
- [ ] **TASK 2.4.6:** Napisać testy jednostkowe dla `GraphBulkOperationsService`

#### **2.5 GraphCacheService**
- [ ] **TASK 2.5.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphCacheService.cs`
- [ ] **TASK 2.5.2:** Implementować cache dla Graph API responses
- [ ] **TASK 2.5.3:** Implementować User ID resolution cache
- [ ] **TASK 2.5.4:** Implementować Team/Group metadata cache
- [ ] **TASK 2.5.5:** Implementować TTL management
- [ ] **TASK 2.5.6:** Napisać testy jednostkowe dla `GraphCacheService`

#### **2.6 GraphService (Fasada)**
- [ ] **TASK 2.6.1:** Utworzyć `TeamsManager.Core/Services/Graph/GraphService.cs`
- [ ] **TASK 2.6.2:** Implementować fasadę łączącą wszystkie Graph services
- [ ] **TASK 2.6.3:** Napisać testy jednostkowe dla `GraphService`

---

### **ETAP 3: Migracja Serwisów Domenowych** ⏱️ **3 dni**

#### **3.1 Aktualizacja ChannelService**
- [ ] **TASK 3.1.1:** Zastąpić `IPowerShellService` → `IGraphService` w `ChannelService.cs`
- [ ] **TASK 3.1.2:** Zaktualizować metody synchronizacji z Graph API
- [ ] **TASK 3.1.3:** Zmigrować cache logic na Graph
- [ ] **TASK 3.1.4:** Przetestować `ChannelService` z Graph API

#### **3.2 Aktualizacja GraphAdminNotificationService**
- [ ] **TASK 3.2.1:** Zastąpić PowerShell calls → Graph API calls w `GraphAdminNotificationService.cs`
- [ ] **TASK 3.2.2:** Użyć `IModernHttpService` dla Mail API
- [ ] **TASK 3.2.3:** Przetestować `GraphAdminNotificationService` z Graph API

#### **3.3 Aktualizacja OrganizationalUnitService**
- [ ] **TASK 3.3.1:** Zastąpić `IPowerShellCacheService` → `IGraphCacheService` w `OrganizationalUnitService.cs`
- [ ] **TASK 3.3.2:** Zaktualizować cache keys i logic
- [ ] **TASK 3.3.3:** Przetestować `OrganizationalUnitService` z Graph cache

---

### **ETAP 4: Migracja API Controllers** ⏱️ **2 dni**

#### **4.1 Aktualizacja DiagnosticsController**
- [ ] **TASK 4.1.1:** Zastąpić PowerShell diagnostics → Graph diagnostics w `DiagnosticsController.cs`
- [ ] **TASK 4.1.2:** Utworzyć endpoint `GET /api/diagnostics/graph/status`
- [ ] **TASK 4.1.3:** Utworzyć endpoint `POST /api/diagnostics/graph/test`
- [ ] **TASK 4.1.4:** Utworzyć endpoint `GET /api/diagnostics/graph/permissions`
- [ ] **TASK 4.1.5:** Przetestować nowe endpointy diagnostyczne

#### **4.2 Aktualizacja TeamLifecycleController**
- [ ] **TASK 4.2.1:** Zastąpić PowerShell operations → Graph operations w `TeamLifecycleController.cs`
- [ ] **TASK 4.2.2:** Zachować istniejące endpointy API (kompatybilność wsteczna)
- [ ] **TASK 4.2.3:** Przetestować wszystkie endpointy lifecycle

#### **4.3 Aktualizacja BulkUserManagementController**
- [ ] **TASK 4.3.1:** Zastąpić PowerShell bulk ops → Graph bulk ops w `BulkUserManagementController.cs`
- [ ] **TASK 4.3.2:** Wykorzystać Graph batch API
- [ ] **TASK 4.3.3:** Przetestować bulk operations

---

### **ETAP 5: Migracja UI Services** ⏱️ **1 dzień**

#### **5.1 Aktualizacja TeamsManagerApiService**
- [ ] **TASK 5.1.1:** Zaktualizować interfejsy diagnostyczne w `TeamsManagerApiService.cs`
- [ ] **TASK 5.1.2:** Dodać nowe metody Graph API
- [ ] **TASK 5.1.3:** Usunąć PowerShell-specific methods

#### **5.2 Aktualizacja MonitoringServices**
- [ ] **TASK 5.2.1:** Zastąpić PowerShell metrics → Graph metrics w `TeamsManagerMonitoringService.cs`
- [ ] **TASK 5.2.2:** Zaktualizować health checks w `MonitoringDataService.cs`

#### **5.3 Aktualizacja ViewModels**
- [ ] **TASK 5.3.1:** Zaktualizować button actions w `TeamsManagerHealthWidgetViewModel.cs`
- [ ] **TASK 5.3.2:** Dodać Graph-specific notifications w `TeamsManagerMetricsWidgetViewModel.cs`

---

### **ETAP 6: Aktualizacja Dependency Injection** ⏱️ **0.5 dnia**

#### **6.1 Nowa Rejestracja Serwisów**
- [ ] **TASK 6.1.1:** Utworzyć `TeamsManager.Core/Extensions/GraphServiceExtensions.cs`
- [ ] **TASK 6.1.2:** Implementować `AddGraphServices()` extension method
- [ ] **TASK 6.1.3:** Zarejestrować wszystkie Graph services w DI

#### **6.2 Aktualizacja Program.cs**
- [ ] **TASK 6.2.1:** Zastąpić `AddPowerShellServices()` → `AddGraphServices()` w `TeamsManager.Api/Program.cs`
- [ ] **TASK 6.2.2:** Zastąpić `AddPowerShellServices()` → `AddGraphServices()` w `TeamsManager.UI/App.xaml.cs`
- [ ] **TASK 6.2.3:** Zachować istniejące HttpClient configurations

---

### **ETAP 7: Testy i Walidacja** ⏱️ **3 dni**

#### **7.1 Testy Jednostkowe Graph Services**
- [ ] **TASK 7.1.1:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphConnectionServiceTests.cs`
- [ ] **TASK 7.1.2:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphTeamManagementServiceTests.cs`
- [ ] **TASK 7.1.3:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphUserManagementServiceTests.cs`
- [ ] **TASK 7.1.4:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphBulkOperationsServiceTests.cs`
- [ ] **TASK 7.1.5:** Utworzyć `TeamsManager.Tests/Services/Graph/GraphCacheServiceTests.cs`

#### **7.2 Testy Integracyjne**
- [ ] **TASK 7.2.1:** Napisać testy z prawdziwym Graph API (dev tenant)
- [ ] **TASK 7.2.2:** Napisać testy batch operations
- [ ] **TASK 7.2.3:** Napisać testy rate limiting
- [ ] **TASK 7.2.4:** Napisać testy error handling

#### **7.3 Testy UI**
- [ ] **TASK 7.3.1:** Przetestować nowe funkcje diagnostyczne w UI
- [ ] **TASK 7.3.2:** Przetestować monitoring widgets
- [ ] **TASK 7.3.3:** Przetestować manual testing window

#### **7.4 Testy Performance**
- [ ] **TASK 7.4.1:** Zmierzyć performance przed migracją (baseline)
- [ ] **TASK 7.4.2:** Zmierzyć performance po migracji
- [ ] **TASK 7.4.3:** Porównać wyniki i zoptymalizować jeśli potrzeba

---

### **ETAP 8: Sprzątanie Kodu** ⏱️ **1 dzień**

#### **8.1 Usunięcie PowerShell Components**
- [ ] **TASK 8.1.1:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellConnectionService.cs`
- [ ] **TASK 8.1.2:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellTeamManagementService.cs`
- [ ] **TASK 8.1.3:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellUserManagementService.cs`
- [ ] **TASK 8.1.4:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellBulkOperationsService.cs`
- [ ] **TASK 8.1.5:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellCacheService.cs`
- [ ] **TASK 8.1.6:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellUserResolverService.cs`
- [ ] **TASK 8.1.7:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellService.cs`
- [ ] **TASK 8.1.8:** Usunąć `TeamsManager.Core/Services/PowerShell/PowerShellServiceBase.cs`
- [ ] **TASK 8.1.9:** Usunąć cały folder `TeamsManager.Core/Abstractions/Services/PowerShell/`
- [ ] **TASK 8.1.10:** Usunąć cały folder `TeamsManager.Core/Exceptions/PowerShell/`
- [ ] **TASK 8.1.11:** Usunąć cały folder `TeamsManager.Core/Helpers/PowerShell/`
- [ ] **TASK 8.1.12:** Usunąć `TeamsManager.Core/Extensions/PowerShellServiceExtensions.cs`

#### **8.2 Aktualizacja Dependencies**
- [ ] **TASK 8.2.1:** Usunąć `System.Management.Automation` z wszystkich .csproj
- [ ] **TASK 8.2.2:** Usunąć `Microsoft.PowerShell.SDK` z wszystkich .csproj
- [ ] **TASK 8.2.3:** Usunąć inne PowerShell-related packages
- [ ] **TASK 8.2.4:** Sprawdzić czy aplikacja kompiluje się bez PowerShell dependencies

#### **8.3 Aktualizacja Dokumentacji**
- [ ] **TASK 8.3.1:** Zaktualizować README.md
- [ ] **TASK 8.3.2:** Zaktualizować docs/ folder
- [ ] **TASK 8.3.3:** Zaktualizować komentarze XML w kodzie
- [ ] **TASK 8.3.4:** Utworzyć migration guide dla deweloperów

#### **8.4 Finalne Testy**
- [ ] **TASK 8.4.1:** Uruchomić wszystkie testy jednostkowe
- [ ] **TASK 8.4.2:** Uruchomić wszystkie testy integracyjne
- [ ] **TASK 8.4.3:** Przetestować aplikację end-to-end
- [ ] **TASK 8.4.4:** Sprawdzić czy wszystkie funkcje działają poprawnie

---

## 📊 **Mapowanie Funkcjonalności**

### **PowerShell → Graph API Mapping**

| PowerShell Method | Graph API Endpoint | HTTP Method | Status |
|-------------------|-------------------|-------------|---------|
| `New-Team` | `/v1.0/teams` | POST | ⏳ |
| `Set-Team` | `/v1.0/teams/{id}` | PATCH | ⏳ |
| `Get-Team` | `/v1.0/teams` | GET | ⏳ |
| `Add-TeamUser` | `/v1.0/teams/{id}/members` | POST | ⏳ |
| `Remove-TeamUser` | `/v1.0/teams/{id}/members/{userId}` | DELETE | ⏳ |
| `New-TeamChannel` | `/v1.0/teams/{id}/channels` | POST | ⏳ |
| `New-MgUser` | `/v1.0/users` | POST | ⏳ |
| `Update-MgUser` | `/v1.0/users/{id}` | PATCH | ⏳ |
| `Get-MgUser` | `/v1.0/users` | GET | ⏳ |
| `Set-MgUserLicense` | `/v1.0/users/{id}/assignLicense` | POST | ⏳ |

### **Batch Operations Mapping**

| PowerShell Bulk | Graph Batch | Endpoint | Status |
|------------------|-------------|----------|---------|
| `ForEach-Object -Parallel` | `POST /v1.0/$batch` | Batch API | ⏳ |
| Multiple `Add-TeamUser` | Batch members add | `/v1.0/$batch` | ⏳ |
| Multiple `Remove-TeamUser` | Batch members remove | `/v1.0/$batch` | ⏳ |

---

## ⚠️ **Potencjalne Wyzwania i Rozwiązania**

### **1. Rate Limiting**
**Problem:** Graph API ma limity 10,000 requests/10min
**Rozwiązanie:** 
- Implementacja exponential backoff
- Batch operations gdzie możliwe
- Request queuing z throttling

### **2. Permissions**
**Problem:** Graph API wymaga specific permissions
**Rozwiązanie:**
- Audit obecnych PowerShell permissions
- Mapowanie na Graph scopes
- Aktualizacja app registration

### **3. Error Handling**
**Problem:** Różne error patterns Graph vs PowerShell
**Rozwiązanie:**
- Unified exception handling
- Retry logic dla transient errors
- Detailed error logging

### **4. Performance**
**Problem:** HTTP calls mogą być wolniejsze niż PowerShell
**Rozwiązanie:**
- Aggressive caching
- Parallel requests
- Connection pooling

### **5. Testing**
**Problem:** Testy z prawdziwym Graph API
**Rozwiązanie:**
- Mock Graph responses
- Dev tenant dla integration tests
- Circuit breaker patterns

---

## 🎯 **Kryteria Sukcesu**

### **Funkcjonalne:**
- [ ] Wszystkie istniejące funkcje działają z Graph API
- [ ] Performance nie gorsze niż PowerShell
- [ ] Error handling na tym samym poziomie
- [ ] Monitoring i diagnostyka działają

### **Techniczne:**
- [ ] Zero PowerShell dependencies
- [ ] Wszystkie testy przechodzą
- [ ] Kod kompiluje się bez ostrzeżeń
- [ ] Memory usage nie wzrósł znacząco

### **Operacyjne:**
- [ ] Deployment bez downtime
- [ ] Rollback plan gotowy
- [ ] Dokumentacja zaktualizowana
- [ ] Team przeszkolony

---

## 📅 **Timeline i Progress Tracking**

| Etap | Czas | Zależności | Postęp | Status |
|------|------|------------|---------|---------|
| **ETAP 1** | 2 dni | - | 0/16 tasków | ⏳ Oczekuje |
| **ETAP 2** | 4 dni | ETAP 1 | 0/23 tasków | ⏳ Oczekuje |
| **ETAP 3** | 3 dni | ETAP 2 | 0/7 tasków | ⏳ Oczekuje |
| **ETAP 4** | 2 dni | ETAP 3 | 0/8 tasków | ⏳ Oczekuje |
| **ETAP 5** | 1 dzień | ETAP 4 | 0/5 tasków | ⏳ Oczekuje |
| **ETAP 6** | 0.5 dnia | ETAP 5 | 0/3 taski | ⏳ Oczekuje |
| **ETAP 7** | 3 dni | ETAP 6 | 0/12 tasków | ⏳ Oczekuje |
| **ETAP 8** | 1 dzień | ETAP 7 | 0/16 tasków | ⏳ Oczekuje |

**Całkowity postęp: 0/90 tasków (0%)**
**Całkowity czas: 16.5 dnia (3.5 tygodnia)**

---

## 🔄 **Plan Rollback**

### **Jeśli Refaktoryzacja Nie Powiedzie Się:**

1. **Przywrócenie PowerShell Services**
   - [ ] Git revert do ostatniego working commit
   - [ ] Przywrócenie PowerShell dependencies
   - [ ] Przywrócenie DI configuration

2. **Hybrid Approach**
   - [ ] Zachowanie PowerShell dla krytycznych operacji
   - [ ] Graph API dla nowych funkcji
   - [ ] Stopniowa migracja

3. **Fallback Strategy**
   - [ ] Feature flags dla Graph vs PowerShell
   - [ ] Runtime switching
   - [ ] A/B testing approach

---

## 📝 **Notatki Implementacyjne**

### **Zachowanie Kompatybilności:**
- Wszystkie publiczne API endpoints pozostają bez zmian
- UI zachowuje tę samą funkcjonalność
- Database schema bez zmian
- Configuration format bez zmian

### **Monitoring Migracji:**
- Metryki performance przed/po
- Error rates monitoring
- User experience metrics
- Resource usage tracking

### **Dokumentacja:**
- Update wszystkich README
- API documentation refresh
- Architecture diagrams update
- Troubleshooting guides

---

**Status:** 📋 **PLAN GOTOWY DO REALIZACJI**
**Ostatnia aktualizacja:** 2024-12-19
**Autor:** AI Assistant
**Review:** Wymagany przed rozpoczęciem implementacji

---

## 📈 **Daily Progress Tracking**

### **Dzień 1:**
- [ ] Rozpoczęcie ETAP 1
- [ ] Ukończenie tasków 1.1.1 - 1.1.7
- [ ] Ukończenie tasków 1.2.1 - 1.2.9

### **Dzień 2:**
- [ ] Ukończenie ETAP 1
- [ ] Rozpoczęcie ETAP 2
- [ ] Ukończenie tasków 2.1.1 - 2.1.6

### **Dzień 3:**
- [ ] Ukończenie tasków 2.2.1 - 2.2.8

### **Dzień 4:**
- [ ] Ukończenie tasków 2.3.1 - 2.3.7

### **Dzień 5:**
- [ ] Ukończenie tasków 2.4.1 - 2.4.6

### **Dzień 6:**
- [ ] Ukończenie ETAP 2
- [ ] Rozpoczęcie ETAP 3

**...i tak dalej dla każdego dnia** 