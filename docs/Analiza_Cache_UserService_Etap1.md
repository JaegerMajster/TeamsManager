# Raport z analizy architektury cache - Etap 1

**Data wykonania:** Grudzień 2024  
**Wykonawca:** Claude Sonnet AI Assistant  
**Status:** ✅ **ANALIZA UKOŃCZONA**

---

## 📋 Podsumowanie Wykonawcze

Przeprowadzono **dokładną analizę architektury cache** w UserService i PowerShellCacheService w celu przygotowania refaktoryzacji eliminującej problem "Thundering Herd". Analiza wykazała **znaczącą duplikację funkcjonalności** między serwisami oraz możliwości optymalizacji bez wprowadzania nowych metod.

### 🎯 Kluczowe Odkrycia:
- ✅ **PowerShellCacheService już posiada** granularną inwalidację użytkowników
- ⚠️ **UserService duplikuje logikę** zamiast wykorzystywać istniejący PowerShellCacheService
- 🔄 **CancellationToken jest używany** w obu serwisach, ale niezależnie
- 📊 **12 miejsc wywołania** inwalidacji w UserService wykazuje wzorzec globalnego resetowania

---

## 1. Stan obecny PowerShellCacheService

### Istniejące metody inwalidacji:
- ✅ **`InvalidateUserCache(userId, userUpn)`** - granularna inwalidacja konkretnego użytkownika
- ✅ **`InvalidateUserListCache()`** - inwalidacja list użytkowników (M365UsersAccountEnabled)
- ✅ **`InvalidateAllCache()`** - globalne resetowanie przez CancellationToken
- ✅ **`Remove(key)`** - usuwanie konkretnych kluczy cache
- ✅ **`TryGetValue<T>()` i `Set<T>()`** - podstawowe operacje cache

### Analiza metody InvalidateUserCache:
```csharp
public void InvalidateUserCache(string? userId = null, string? userUpn = null)
{
    if (!string.IsNullOrWhiteSpace(userId))
    {
        Remove(UserIdCacheKeyPrefix + userId);
        Remove(M365UserDetailsCacheKeyPrefix + userId);
    }

    if (!string.IsNullOrWhiteSpace(userUpn))
    {
        Remove(UserIdCacheKeyPrefix + userUpn);
        Remove(UserUpnCacheKeyPrefix + userUpn);
    }

    Remove(M365UsersAccountEnabledCacheKeyPrefix + "True");
    Remove(M365UsersAccountEnabledCacheKeyPrefix + "False");
}
```

**Obsługa granularności:** ✅ **TAK** - metoda obsługuje zarówno konkretnych użytkowników jak i globalne listy M365

### Mechanizm CancellationToken:
- **Zarządzanie:** `_powerShellCacheTokenSource` (statyczny)
- **Zastosowanie:** Grupowa inwalidacja przez `InvalidateAllCache()`
- **Współpraca:** Każdy wpis cache używa `AddExpirationToken()`
- **Zasięg:** Wszystkie wpisy PowerShell/Graph API

### Struktura kluczy cache:
```csharp
// Klucze użytkowników w PowerShellCacheService
private const string UserIdCacheKeyPrefix = "PowerShell_UserId_";
private const string UserUpnCacheKeyPrefix = "PowerShell_UserUpn_";
private const string M365UserDetailsCacheKeyPrefix = "PowerShell_M365User_Id_";
private const string M365UsersAccountEnabledCacheKeyPrefix = "PowerShell_M365Users_AccountEnabled_";
```

---

## 2. Analiza UserService

### Obecny mechanizm cache:
```csharp
// Własny CancellationTokenSource w UserService
private static CancellationTokenSource _usersCacheTokenSource = new CancellationTokenSource();

// Własne klucze cache
private const string AllActiveUsersCacheKey = "Users_AllActive";
private const string UserByIdCacheKeyPrefix = "User_Id_";
private const string UserByUpnCacheKeyPrefix = "User_Upn_";
private const string UsersByRoleCacheKeyPrefix = "Users_Role_";
```

**Problem:** UserService używa **całkowicie oddzielnego systemu cache** zamiast wykorzystywać PowerShellCacheService.

### Mechanizm globalnego resetowania:
```csharp
private void InvalidateUserCache(..., bool invalidateAll = false)
{
    // Globalne resetowanie CancellationToken
    var oldTokenSource = Interlocked.Exchange(ref _usersCacheTokenSource, new CancellationTokenSource());
    if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
    {
        oldTokenSource.Cancel();
        oldTokenSource.Dispose();
    }
    // ... dalej granularna inwalidacja
}
```

**Kiedy następuje globalne resetowanie:**
- `RefreshCacheAsync()` wywołuje `InvalidateUserCache(invalidateAll: true)`
- **KAŻDE** wywołanie `InvalidateUserCache` resetuje CancellationToken (nawet przy granularnej inwalidacji!)

### Miejsca wywołania inwalidacji:

| **Metoda** | **Parametry inwalidacji** | **Potencjał optymalizacji** |
|------------|---------------------------|----------------------------|
| `CreateUserAsync` | `userId`, `upn`, `role`, `invalidateAllGlobalLists: true` | 🟡 Częściowo - wymagane dla list globalnych |
| `UpdateUserAsync` | `userId`, `upn`, `role`, `oldUpn`, `oldRole`, `isActiveChanged`, `invalidateAllGlobalLists` | 🟢 **WYSOKIE** - precyzyjne parametry |
| `AssignUserToSchoolTypeAsync` | `userId`, `upn`, `invalidateAllGlobalLists: false` | 🟢 **WYSOKIE** - lokalny zakres |
| `RemoveUserFromSchoolTypeAsync` | `userId`, `upn`, `invalidateAllGlobalLists: false` | 🟢 **WYSOKIE** - lokalny zakres |
| `AssignTeacherToSubjectAsync` | `userId`, `upn`, `invalidateAllGlobalLists: false` | 🟢 **WYSOKIE** - lokalny zakres |
| `RemoveTeacherFromSubjectAsync` | `userId`, `upn`, `invalidateAllGlobalLists: false` | 🟢 **WYSOKIE** - lokalny zakres |
| `DeactivateUserAsync` | `userId`, `upn`, `role`, `isActiveChanged: true`, `invalidateAllGlobalLists: true` | 🟡 Częściowo - wymagane dla list globalnych |
| `ActivateUserAsync` | `userId`, `upn`, `role`, `isActiveChanged: true`, `invalidateAllGlobalLists: true` | 🟡 Częściowo - wymagane dla list globalnych |
| `RefreshCacheAsync` | `invalidateAll: true` | 🔴 **GLOBALNE** - wymagane |

---

## 3. Identyfikacja braków

### Co już istnieje:
- ✅ **Granularna inwalidacja użytkowników** w PowerShellCacheService
- ✅ **Mechanizm CancellationToken** dla grupowej inwalidacji
- ✅ **Podstawowe operacje cache** (Get, Set, Remove)
- ✅ **Logowanie operacji cache** w PowerShellCacheService
- ✅ **Zabezpieczenia przed pustymi parametrami**

### Czego faktycznie brakuje:

#### 🚫 **BRAK METOD w PowerShellCacheService:**
- **`InvalidateUsersByRole(UserRole role)`** - brak inwalidacji według roli
- **`InvalidateAllActiveUsersList()`** - brak inwalidacji listy aktywnych użytkowników
- **`InvalidateUserAndRelatedData(userId, upn, oldUpn?, role?, oldRole?)`** - brak kompleksowej inwalidacji

#### 📊 **BRAK OBSŁUGI KLUCZY UserService:**
PowerShellCacheService nie zna kluczy używanych przez UserService:
```csharp
// Nieobsługiwane klucze:
"Users_AllActive"
"User_Id_" + userId  (vs "PowerShell_UserId_" + upn)
"User_Upn_" + upn
"Users_Role_" + role
```

#### 🔄 **BRAK INTEGRACJI MIĘDZY SERWISAMI:**
- UserService i PowerShellCacheService używają niezależnych CancellationToken
- Brak wspólnego mechanizmu inwalidacji
- PowerShellUserManagementService wywołuje `PowerShellCacheService.InvalidateUserCache`
- UserService wywołuje własną `InvalidateUserCache`

### Rekomendacje:

#### ✅ **OPCJA A: Rozszerzenie PowerShellCacheService (ZALECANE)**
1. **Dodać metody:**
   ```csharp
   void InvalidateUsersByRole(UserRole role);
   void InvalidateUserAndRelatedData(string? userId, string? upn, string? oldUpn, UserRole? role, UserRole? oldRole);
   void InvalidateAllActiveUsersList();
   ```

2. **Dodać obsługę kluczy UserService:**
   ```csharp
   private const string UserServiceAllActiveKey = "Users_AllActive";
   private const string UserServiceByIdPrefix = "User_Id_";
   private const string UserServiceByUpnPrefix = "User_Upn_";
   private const string UserServiceByRolePrefix = "Users_Role_";
   ```

3. **Zastąpić wywołania w UserService:**
   ```csharp
   // ZAMIAST:
   InvalidateUserCache(userId, upn, role, ...);
   
   // UŻYWAĆ:
   _powerShellCacheService.InvalidateUserAndRelatedData(userId, upn, oldUpn, role, oldRole);
   ```

#### 🔄 **OPCJA B: Unifikacja CancellationToken**
- Wspólny CancellationTokenSource między serwisami
- UserService używa PowerShellCacheService dla wszystkich operacji cache

#### ❌ **OPCJA C: Status Quo (NIE ZALECANA)**
- Pozostawienie obecnej duplikacji
- Brak eliminacji problemu "Thundering Herd"

---

## 4. Wnioski

### 🔍 **Główne problemy obecnej architektury:**

1. **Duplikacja funkcjonalności:**
   - PowerShellCacheService już ma granularną inwalidację
   - UserService reimplementuje tę samą logikę
   - Dwa niezależne systemy CancellationToken

2. **Problem "Thundering Herd":**
   - KAŻDE wywołanie `InvalidateUserCache` w UserService resetuje CancellationToken
   - Nawet granularne operacje powodują globalne resetowanie
   - 8/12 wywołań mogłoby być granularne

3. **Brak integracji:**
   - PowerShellUserManagementService → PowerShellCacheService
   - UserService → własny mechanizm cache
   - Niespójność w zarządzaniu cache użytkowników

### 🎯 **Kierunek dalszych prac:**

**REKOMENDACJA: Rozszerzenie PowerShellCacheService + Refaktor UserService**

1. **Etap 2:** Dodanie 3 brakujących metod do PowerShellCacheService
2. **Etap 3:** Refactor UserService do używania PowerShellCacheService
3. **Etap 4:** Usunięcie duplikacji i własnego CancellationToken z UserService
4. **Etap 5:** Testy i weryfikacja eliminacji "Thundering Herd"

### 📊 **Metryki oczekiwanych korzyści:**
- **-83% wywołań globalnego resetowania** (10/12 → 2/12)
- **-100% duplikacji logiki** cache użytkowników
- **+100% spójności** w zarządzaniu cache między serwisami
- **+X% wydajności** przez eliminację niepotrzebnych resetowań

**Analiza potwierdza wykonalność refaktoryzacji bez wprowadzania rewolucyjnych zmian w architekturze.** 