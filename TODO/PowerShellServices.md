# PowerShellService - Rozszerzenie funkcjonalności

## Status: 🔄 W planach

Aktualna implementacja `PowerShellService` jest funkcjonalna, ale brakuje jej kilku ważnych funkcjonalności typu "read-only" oraz zaawansowanych operacji zarządzania. Poniżej lista zadań do implementacji.

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

1. **Bezpieczeństwo**: Wszystkie nowe metody muszą używać `SecureString` dla haseł
2. **Logging**: Konsekwentne logowanie na poziomach Debug/Info/Warning/Error
3. **Error handling**: Obsługa błędów PowerShell i wyjątków .NET
4. **Testing**: Każda nowa metoda wymaga testów jednostkowych w `PowerShellServiceTests.cs`
5. **Cache**: Rozważyć cache'owanie wyników dla często odpytywanych danych (lista zespołów, użytkownicy)
6. **Performance**: Operacje masowe powinny być optymalizowane pod kątem wydajności

---

**Data utworzenia**: 01.06.2025  
**Ostatnia aktualizacja**: 01.06.2025  
**Status**: ✅ Lista zadań kompletna - gotowa do implementacji 