# Plan Refaktoryzacji 008: Eliminacja "Thundering Herd" w pozostałych serwisach

**Data utworzenia:** Grudzień 2024  
**Status:** 🟡 W TRAKCIE REALIZACJI  
**Cel:** Eliminacja problemu "Thundering Herd" w 4 pozostałych serwisach

---

## 📋 **PODSUMOWANIE ZAGROŻONYCH SERWISÓW**

| **Serwis** | **Zagrożenie** | **Miejsca wywołania** | **Priorytet** |
|------------|----------------|----------------------|---------------|
| TeamService.cs | ⚠️ WYSOKIE | 13 globalnych resetów | 🔥 KRYTYCZNY |
| SchoolTypeService.cs | ⚠️ WYSOKIE | 6 globalnych resetów | 🔴 WYSOKI |
| ApplicationSettingService.cs | ⚠️ WYSOKIE | 5 globalnych resetów | 🔴 WYSOKI |
| UserService.cs | ⚠️ CZĘŚCIOWY | 12 wywołań + duplikacja | 🟡 ŚREDNI |

---

## 🎯 **ETAPY REFAKTORYZACJI**

### **ETAP 0: Przygotowanie infrastruktury** ✅ **UKOŃCZONY**
- [x] **Task 0.1:** Rozszerzenie PowerShellCacheService o metody dla TeamService ✅
- [x] **Task 0.2:** Rozszerzenie PowerShellCacheService o metody dla SchoolTypeService ✅ 
- [x] **Task 0.3:** Rozszerzenie PowerShellCacheService o metody dla ApplicationSettingService ✅
- [x] **Task 0.4:** Rozszerzenie interfejsu IPowerShellCacheService ✅
- [x] **Task 0.5:** Weryfikacja kompilacji i testów bazowych ✅

### **ETAP 1: Refaktoryzacja TeamService.cs** ✅ **UKOŃCZONY**
- [x] **Task 1.1:** Usunięcie własnego CancellationTokenSource z TeamService ✅
- [x] **Task 1.2:** Refaktor metody InvalidateCache() - implementacja logiki granularnej ✅
- [x] **Task 1.3:** Aktualizacja wszystkich wywołań _cache na _powerShellCacheService ✅
- [x] **Task 1.4:** Usunięcie metody GetDefaultCacheEntryOptions() - delegacja do PowerShellCacheService ✅
- [x] **Task 1.5:** Naprawa testów TeamServiceTests i weryfikacja kompilacji ✅

### **ETAP 2: Refaktoryzacja SchoolTypeService.cs** ✅ **UKOŃCZONY**
- [x] **Task 2.1:** Usunięcie własnego CancellationTokenSource z SchoolTypeService ✅
- [x] **Task 2.2:** Refaktor metody InvalidateCache() - implementacja logiki granularnej ✅
- [x] **Task 2.3:** Aktualizacja wszystkich wywołań _cache na _powerShellCacheService ✅
- [x] **Task 2.4:** Usunięcie metody GetDefaultCacheEntryOptions() - delegacja do PowerShellCacheService ✅
- [x] **Task 2.5:** Naprawiono wszystkie błędy kompilacji w testach SchoolTypeServiceTests ✅
- [x] **Task 2.6:** 83% redukcja globalnych resetów cache (5/6 → 1/6) ✅

### **ETAP 3: Refaktoryzacja ApplicationSettingService.cs** 🔴 WYSOKI
- [ ] **Task 3.1:** Usunięcie własnego CancellationTokenSource z ApplicationSettingService
- [ ] **Task 3.2:** Refaktor metody InvalidateSettingCache() - implementacja logiki granularnej
- [ ] **Task 3.3:** Aktualizacja wszystkich wywołań InvalidateSettingCache (5 miejsc)  
- [ ] **Task 3.4:** Usunięcie metody GetDefaultCacheEntryOptions() - delegacja do PowerShellCacheService
- [ ] **Task 3.5:** Testy funkcjonalne i weryfikacja eliminacji "Thundering Herd"

### **ETAP 4: Finalizacja UserService.cs** 🟡 ŚREDNI
- [ ] **Task 4.1:** Usunięcie pozostałego własnego CancellationTokenSource z UserService
- [ ] **Task 4.2:** Eliminacja duplikacji metod inwalidacji 
- [ ] **Task 4.3:** Pełna delegacja wszystkich operacji cache do PowerShellCacheService
- [ ] **Task 4.4:** Cleanup kodu i usunięcie nieużywanych elementów
- [ ] **Task 4.5:** Testy funkcjonalne i weryfikacja kompletnej integracji

### **ETAP 5: Weryfikacja i dokumentacja**
- [ ] **Task 5.1:** Testy integracyjne całej aplikacji
- [ ] **Task 5.2:** Weryfikacja eliminacji "Thundering Herd" we wszystkich serwisach  
- [ ] **Task 5.3:** Testy wydajności cache
- [ ] **Task 5.4:** Dokumentacja zmian i metryki
- [ ] **Task 5.5:** Przygotowanie raportu końcowego Refaktoryzacja008.md

---

## 🔧 **SZCZEGÓŁOWE SPECYFIKACJE ETAPÓW**

### **ETAP 0: Metody do dodania w PowerShellCacheService**

#### **Dla TeamService:**
```csharp
// Granularna inwalidacja zespołów
void InvalidateTeamById(string teamId);
void InvalidateTeamsByOwner(string ownerUpn);  
void InvalidateTeamsByStatus(TeamStatus status);
void InvalidateAllActiveTeamsList();
void InvalidateArchivedTeamsList();
void InvalidateTeamSpecificByStatus();
```

#### **Dla SchoolTypeService:**
```csharp  
// Granularna inwalidacja typów szkół
void InvalidateSchoolTypeById(string schoolTypeId);
void InvalidateAllActiveSchoolTypesList();
```

#### **Dla ApplicationSettingService:**
```csharp
// Granularna inwalidacja ustawień
void InvalidateSettingByKey(string key);
void InvalidateSettingsByCategory(string category);  
void InvalidateAllActiveSettingsList();
```

### **WZORCOWA METODA InvalidateCache (na podstawie SubjectService):**

```csharp
private void InvalidateCache(..., bool invalidateAll = false)
{
    if (invalidateAll)
    {
        // TYLKO dla RefreshCacheAsync()
        _powerShellCacheService.InvalidateAllCache();
        return;
    }

    // GRANULARNA inwalidacja przez PowerShellCacheService
    _powerShellCacheService.InvalidateAllActive[Entity]List();
    
    if (!string.IsNullOrWhiteSpace(entityId))
    {
        _powerShellCacheService.Invalidate[Entity]ById(entityId);
    }
    
    // Dodatkowa logika granularna według potrzeb
}
```

---

## 📊 **METRYKI OCZEKIWANYCH REZULTATÓW**

| **Serwis** | **Przed** | **Po refaktorze** | **Poprawa** |
|------------|-----------|-------------------|-------------|
| TeamService | 13/13 = globalne | 1/13 = globalne | **-92%** |
| SchoolTypeService | 6/6 = globalne | 1/6 = globalne | **-83%** |  
| ApplicationSettingService | 5/5 = globalne | 1/5 = globalne | **-80%** |
| UserService | Duplikacja logiki | Pełna delegacja | **-100%** |

**Łączne korzyści:**
- ✅ **97% redukcja globalnych resetów cache**
- ✅ **Unifikacja mechanizmu cache** 
- ✅ **Eliminacja duplikacji w UserService**
- ✅ **Spójność architektoniczna**

---

## ⚠️ **RYZYKA I MITIGATION**

| **Ryzyko** | **Prawdopodobieństwo** | **Mitigation** |
|------------|------------------------|----------------|
| Regresja funkcjonalna | Średnie | Testy po każdym etapie |
| Problemy z cache | Niskie | Wzorce z innych serwisów |
| Konflikty merge | Niskie | Praca na gałęzi refaktoryzacji |

---

## 🚀 **STATUS REALIZACJI**
- **Utworzono:** ⏰ [DATA_UTWORZENIA]
- **Rozpoczęto:** ⏰ [DATA_ROZPOCZECIA]  
- **Ostatnia aktualizacja:** ⏰ [DATA_AKTUALIZACJI]

**Postęp ogólny:** 0/25 tasków ukończonych (0%)

**Postęp etapowy:**
- ETAP 0: 0/5 tasków (0%) 
- ETAP 1: 0/5 tasków (0%)
- ETAP 2: 0/5 tasków (0%) 
- ETAP 3: 0/5 tasków (0%)
- ETAP 4: 0/5 tasków (0%)
- ETAP 5: 0/5 tasków (0%) 