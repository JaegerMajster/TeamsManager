# PowerShellService - Rozszerzenie funkcjonalności

## Status: 🔄 W trakcie refaktoryzacji (Etap 3/7 ukończony)

Aktualna implementacja `PowerShellService` jest funkcjonalna, ale brakuje jej kilku ważnych funkcjonalności typu "read-only" oraz zaawansowanych operacji zarządzania. Poniżej lista zadań do implementacji.

## 🎯 **Status refaktoryzacji PowerShell Services**

### ✅ **Ukończone etapy:**
- **Etap 1/7**: Hierarchia wyjątków PowerShell
  - ✅ `PowerShellException` (bazowy)
  - ✅ `PowerShellConnectionException` 
  - ✅ `PowerShellCommandExecutionException`
  - ✅ `PowerShellExceptionBuilder`

- **Etap 2/7**: Rozwiązanie Captive Dependency
  - ✅ Refaktoryzacja `PowerShellConnectionService`
  - ✅ Użycie `IServiceScopeFactory` zamiast bezpośrednich serwisów scoped
  - ✅ Thread-safe zarządzanie scope'ami

- **Etap 3/7**: Ulepszenie obsługi błędów i mapowania
  - ✅ `PSObjectMapper` - centralizacja mapowania PSObject
  - ✅ `PSParameterValidator` - walidacja i sanitacja parametrów
  - ✅ Ulepszona obsługa błędów w `PowerShellService`
  - ✅ Refaktoryzacja mapowania w `ChannelService`
  - ✅ Ochrona przed PowerShell injection

### 🔄 **Następne etapy:**
- **Etap 4/7**: Wprowadzenie fabryki PSObjects
- **Etap 5/7**: Centralizacja zarządzania sesjami
- **Etap 6/7**: Optymalizacja cache i bulk operations
- **Etap 7/7**: Monitoring i diagnostyka

---

## 🛡️ **Nowe komponenty architektoniczne (po Etapie 3/7)**

### 📦 **PSObjectMapper** (`TeamsManager.Core/Helpers/PowerShell/PSObjectMapper.cs`)
Bezpieczne mapowanie właściwości PSObject na typy .NET:
- `GetString()` - bezpieczne pobieranie stringów z sanitacją
- `GetInt32()` / `GetInt64()` - typowane mapowanie liczb
- `GetBoolean()` - mapowanie bool z obsługą różnych formatów
- `GetDateTime()` - obsługa dat z różnych źródeł
- `LogProperties()` - debugging PSObject

### 🔐 **PSParameterValidator** (`TeamsManager.Core/Helpers/PowerShell/PSParameterValidator.cs`)
Walidacja i sanitacja parametrów przed PowerShell:
- `ValidateAndSanitizeString()` - escape injection chars (`'`, `` ` ``, `$`)
- `ValidateEmail()` - regex walidacja adresów email
- `ValidateGuid()` - walidacja identyfikatorów GUID
- `CreateSafeParameters()` - bezpieczne słowniki parametrów

---

## 📝 **Aktualizacja priorytetów po Etapie 3/7**

W kontekście ulepszeń z Etapu 3, niektóre zadania zyskały wyższą jakość implementacyjną:

### **Wysokiej jakości (zaimplementowane wzorce z Etapu 3):**
- Wszystkie nowe metody powinny używać `PSObjectMapper` zamiast bezpośredniego `.Value?.ToString()`
- Walidacja parametrów przez `PSParameterValidator` przed każdym wywołaniem PowerShell
- Rzucanie granularnych wyjątków zamiast zwracania `null`

---

## 📊 **1. Pobieranie informacji o zespołach**

### 1.1 Pobieranie pojedynczego zespołu
- [ ] **Zadanie**: Dodać metodę `GetTeamAsync(string teamId)`
- [ ] **Cmdlet**: `Get-Team -GroupId $teamId`
- [ ] **Zwraca**: `PSObject?` z informacjami o zespole
- [ ] **Cel**: Synchronizacja danych zespołu między lokalną bazą a Teams

### 1.2 Pobieranie wszystkich zespołów
- [ ] **Zadanie**: Dodać metodę `GetAllTeamsAsync()`
- [ ] **Cmdlet**: `Get-Team`
- [ ] **Zwraca**: `Collection<PSObject>?` z listą wszystkich zespołów
- [ ] **Cel**: Audyt i raportowanie wszystkich zespołów w organizacji

### 1.3 Pobieranie zespołów właściciela
- [ ] **Zadanie**: Dodać metodę `GetTeamsByOwnerAsync(string ownerUpn)`
- [ ] **Cmdlet**: `Get-Team | Where-Object { $_.Owner -eq $ownerUpn }`
- [ ] **Zwraca**: `Collection<PSObject>?` z zespołami właściciela
- [ ] **Cel**: Dashboard właściciela zespołów

---

## 👥 **2. Zarządzanie członkami zespołu**

### 2.1 Pobieranie członków zespołu
- [ ] **Zadanie**: Dodać metodę `GetTeamMembersAsync(string teamId)`
- [ ] **Cmdlet**: `Get-TeamUser -GroupId $teamId`
- [ ] **Zwraca**: `Collection<PSObject>?` z listą członków
- [ ] **Cel**: Synchronizacja członków zespołu

### 2.2 Pobieranie konkretnego członka
- [ ] **Zadanie**: Dodać metodę `GetTeamMemberAsync(string teamId, string userUpn)`
- [ ] **Cmdlet**: `Get-TeamUser -GroupId $teamId -User $userUpn`
- [ ] **Zwraca**: `PSObject?` z informacjami o członku
- [ ] **Cel**: Weryfikacja członkostwa i ról

### 2.3 Zmiana roli członka
- [ ] **Zadanie**: Dodać metodę `UpdateTeamMemberRoleAsync(string teamId, string userUpn, string newRole)`
- [ ] **Cmdlet**: `Add-TeamUser -GroupId $teamId -User $userUpn -Role $newRole` (powtórne dodanie zmienia rolę)
- [ ] **Zwraca**: `bool` - sukces operacji
- [ ] **Cel**: Elastyczne zarządzanie rolami w zespole

---

## 🔍 **3. Pobieranie informacji o użytkownikach M365**

### 3.1 Pobieranie pojedynczego użytkownika
- [ ] **Zadanie**: Dodać metodę `GetM365UserAsync(string userUpn)`
- [ ] **Cmdlet**: `Get-AzureADUser -ObjectId $userUpn`
- [ ] **Zwraca**: `PSObject?` z informacjami o użytkowniku
- [ ] **Cel**: Synchronizacja danych użytkownika

### 3.2 Wyszukiwanie użytkowników
- [ ] **Zadanie**: Dodać metodę `SearchM365UsersAsync(string searchTerm)`
- [ ] **Cmdlet**: `Get-AzureADUser -SearchString $searchTerm`
- [ ] **Zwraca**: `Collection<PSObject>?` z wynikami wyszukiwania
- [ ] **Cel**: Wyszukiwanie użytkowników do dodania do zespołów

### 3.3 Pobieranie wszystkich użytkowników działu
- [ ] **Zadanie**: Dodać metodę `GetUsersByDepartmentAsync(string department)`
- [ ] **Cmdlet**: `Get-AzureADUser -Filter "department eq '$department'"`
- [ ] **Zwraca**: `Collection<PSObject>?` z użytkownikami działu
- [ ] **Cel**: Zarządzanie użytkownikami na poziomie działu

---

## 🎫 **4. Zarządzanie licencjami**

### 4.1 Przypisywanie licencji
- [ ] **Zadanie**: Dodać metodę `AssignLicenseToUserAsync(string userUpn, string licenseSkuId)`
- [ ] **Cmdlet**: `Set-AzureADUserLicense -ObjectId $userUpn -AssignedLicenses @{AddLicenses=@($licenseSkuId)}`
- [ ] **Zwraca**: `bool` - sukces operacji
- [ ] **Cel**: Automatyczne przypisywanie licencji Teams

### 4.2 Usuwanie licencji
- [ ] **Zadanie**: Dodać metodę `RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId)`
- [ ] **Cmdlet**: `Set-AzureADUserLicense -ObjectId $userUpn -AssignedLicenses @{RemoveLicenses=@($licenseSkuId)}`
- [ ] **Zwraca**: `bool` - sukces operacji
- [ ] **Cel**: Zarządzanie kosztami licencji

### 4.3 Pobieranie licencji użytkownika
- [ ] **Zadanie**: Dodać metodę `GetUserLicensesAsync(string userUpn)`
- [ ] **Cmdlet**: `Get-AzureADUserLicenseDetail -ObjectId $userUpn`
- [ ] **Zwraca**: `Collection<PSObject>?` z licencjami użytkownika
- [ ] **Cel**: Audyt licencji

### 4.4 Pobieranie dostępnych licencji
- [ ] **Zadanie**: Dodać metodę `GetAvailableLicensesAsync()`
- [ ] **Cmdlet**: `Get-AzureADSubscribedSku`
- [ ] **Zwraca**: `Collection<PSObject>?` z dostępnymi SKU licencji
- [ ] **Cel**: Zarządzanie pulą licencji

---

## 🔌 **5. Rozszerzenie połączeń**

### 5.1 Połączenie z Azure AD
- [ ] **Zadanie**: Dodać metodę `ConnectToAzureADAsync(string username, string password)`
- [ ] **Cmdlet**: `Connect-AzureAD -Credential $credential`
- [ ] **Zwraca**: `bool` - sukces połączenia
- [ ] **Cel**: Operacje na użytkownikach Azure AD

### 5.2 Połączenie z Exchange Online
- [ ] **Zadanie**: Dodać metodę `ConnectToExchangeOnlineAsync(string username, string password)`
- [ ] **Cmdlet**: `Connect-ExchangeOnline -Credential $credential`
- [ ] **Zwraca**: `bool` - sukces połączenia
- [ ] **Cel**: Zarządzanie skrzynkami pocztowymi Teams

### 5.3 Status połączeń
- [ ] **Zadanie**: Dodać właściwości `IsAzureADConnected`, `IsExchangeConnected`
- [ ] **Cel**: Monitoring statusu wszystkich połączeń

---

## 📈 **6. Raportowanie i monitoring**

### 6.1 Raport wykorzystania zespołu
- [ ] **Zadanie**: Dodać metodę `GetTeamUsageReportAsync(string teamId, DateTime? startDate = null, DateTime? endDate = null)`
- [ ] **Cmdlet**: `Get-TeamsUserActivityReport` lub Graph API
- [ ] **Zwraca**: `Collection<PSObject>?` z danymi wykorzystania
- [ ] **Cel**: Monitoring aktywności zespołów

### 6.2 Raport aktywności użytkownika
- [ ] **Zadanie**: Dodać metodę `GetUserActivityReportAsync(string userUpn, DateTime? startDate = null, DateTime? endDate = null)`
- [ ] **Cmdlet**: Teams reporting cmdlets
- [ ] **Zwraca**: `Collection<PSObject>?` z aktywnością użytkownika
- [ ] **Cel**: Monitoring aktywności indywidualnych użytkowników

### 6.3 Raport statusu zespołów
- [ ] **Zadanie**: Dodać metodę `GetTeamsHealthReportAsync()`
- [ ] **Zwraca**: `Collection<PSObject>?` ze statusem wszystkich zespołów
- [ ] **Cel**: Monitoring zdrowia ekosystemu Teams

---

## 🛠️ **7. Narzędzia diagnostyczne**

### 7.1 Test połączenia
- [ ] **Zadanie**: Dodać metodę `TestConnectionAsync()`
- [ ] **Cmdlet**: `Get-CsTenant` lub podobny
- [ ] **Zwraca**: `bool` - status połączenia
- [ ] **Cel**: Diagnostyka problemów z połączeniem

### 7.2 Walidacja praw dostępu
- [ ] **Zadanie**: Dodać metodę `ValidatePermissionsAsync()`
- [ ] **Zwraca**: `Dictionary<string, bool>` z prawami dostępu
- [ ] **Cel**: Weryfikacja uprawnień administratora

### 7.3 Synchronizacja danych
- [ ] **Zadanie**: Dodać metodę `SyncTeamDataAsync(string teamId)`
- [ ] **Cel**: Synchronizacja między lokalną bazą a Teams
- [ ] **Zwraca**: `bool` - sukces synchronizacji

---

## 📋 **8. Zaawansowane operacje**

### 8.1 Klonowanie zespołu
- [ ] **Zadanie**: Dodać metodę `CloneTeamAsync(string sourceTeamId, string newDisplayName, string newDescription, string newOwnerUpn)`
- [ ] **Cel**: Szybkie tworzenie zespołów na podstawie istniejących

### 8.2 Backup ustawień zespołu
- [ ] **Zadanie**: Dodać metodę `BackupTeamSettingsAsync(string teamId)`
- [ ] **Zwraca**: `PSObject?` z ustawieniami zespołu
- [ ] **Cel**: Backup przed wprowadzaniem zmian

### 8.3 Masowe operacje
- [ ] **Zadanie**: Dodać metodę `BulkAddUsersToTeamAsync(string teamId, List<string> userUpns, string role)`
- [ ] **Cel**: Efektywne dodawanie wielu użytkowników

---

## 🎯 **Priorytety implementacji**

### **Wysoki priorytet (Phase 1)**
1. Pobieranie informacji o zespołach (zadania 1.1-1.3)
2. Pobieranie członków zespołu (zadania 2.1-2.2)
3. Pobieranie użytkowników M365 (zadania 3.1-3.2)

### **Średni priorytet (Phase 2)**
1. Zmiana ról członków (zadanie 2.3)
2. Podstawowe zarządzanie licencjami (zadania 4.1-4.3)
3. Narzędzia diagnostyczne (zadania 7.1-7.2)

### **Niski priorytet (Phase 3)**
1. Raportowanie i monitoring (zadania 6.1-6.3)
2. Rozszerzenie połączeń (zadania 5.1-5.2)
3. Zaawansowane operacje (zadania 8.1-8.3)

---

## 📝 **Uwagi implementacyjne**

### 🔧 **Obowiązkowe wzorce (po Etapie 3/7):**
1. **PSObjectMapper**: Użyj `PSObjectMapper.GetString()`, `GetInt32()`, etc. zamiast `.Value?.ToString()`
2. **PSParameterValidator**: Wszystkie parametry przez `ValidateAndSanitizeString()` przed PowerShell
3. **Granularne wyjątki**: Rzucaj `PowerShellConnectionException`, `PowerShellCommandExecutionException` 
4. **Logging**: Użyj `PSObjectMapper.LogProperties()` dla debugging PSObject

### 🛡️ **Bezpieczeństwo:**
1. **SecureString**: Wszystkie hasła przez `SecureString`
2. **Injection protection**: Escape `'`, `` ` ``, `$` w parametrach
3. **Guid validation**: Waliduj identyfikatory przez `PSParameterValidator.ValidateGuid()`
4. **Email validation**: Waliduj adresy przez `PSParameterValidator.ValidateEmail()`

### 🧪 **Testing:**
1. **Unit tests**: Każda nowa metoda w `PowerShellServiceTests.cs`
2. **Injection tests**: Testuj ochronę przed PowerShell injection
3. **Error handling**: Testuj wszystkie przypadki błędów

### ⚡ **Performance:**
1. **Cache**: Rozważyć cache'owanie wyników dla często odpytywanych danych
2. **Bulk operations**: Optymalizacja operacji masowych
3. **Connection pooling**: Efektywne zarządzanie połączeniami PowerShell

---

**Data utworzenia**: 01.06.2025  
**Ostatnia aktualizacja**: 05 czerwca 2025, 10:49  
**Status**: 🔄 W trakcie refaktoryzacji (Etap 3/7 ukończony) - gotowa do Etapu 4/7

### 📈 **Postęp refaktoryzacji:**
- **Ukończone**: 3/7 etapów (43%)
- **Nowe komponenty**: 2 (PSObjectMapper, PSParameterValidator)
- **Bezpieczeństwo**: ✅ PowerShell injection protection
- **Type safety**: ✅ Granularne mapowanie PSObject
- **Error handling**: ✅ Granularne wyjątki PowerShell 