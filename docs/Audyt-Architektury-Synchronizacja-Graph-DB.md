# Audyt Architektury - Synchronizacja Graph-DB (Etap 1/8)

**Data audytu:** 2024-12-19  
**Gałąź:** `refaktoryzacja`  
**Cel:** Zmapowanie istniejących mechanizmów przed implementacją synchronizacji Graph↔DB  

---

## 1. Analiza wywołań PowerShell

### Serwisy używające ręcznego ConnectWithAccessTokenAsync:
- **UserService**: 2 wystąpienia ✅
  - Lokalizacje: `CreateUserAsync` (linia 267), `ActivateUserAsync` (linia 1351)
  - Wzorzec: bezpośrednie sprawdzenie `if (!await _powerShellService.ConnectWithAccessTokenAsync(apiAccessToken))`
  
- **TeamService**: 5 wystąpień ✅
  - Lokalizacje: `CreateTeamAsync` (323), `UpdateTeamAsync`, `ArchiveTeamAsync` (653), `DeleteTeamAsync` (875), `AddUsersToTeamAsync` (1252), `RemoveUsersFromTeamAsync` (1427)
  - Wzorzec: identyczny jak UserService - sprawdzenie sukcesu połączenia

### Serwisy używające ExecuteWithAutoConnectAsync:
- **ChannelService**: 3 wystąpienia ✅ **KONSEKWENTNE UŻYCIE**
  - `GetTeamChannelsAsync`: linia 149 - pełna synchronizacja z Graph
  - `GetTeamChannelByIdAsync`: linia 264 - pojedynczy kanał
  - `CreateTeamChannelAsync`, `UpdateTeamChannelAsync`: operacje CRUD
  
- **UserService**: 2 wystąpienia w testach ✅
  - `CreateUserAsync` z ExecuteWithAutoConnectAsync
  - `UpdateUserAsync` z ExecuteWithAutoConnectAsync

- **PowerShellService**: Definiuje metodę ✅
  - Implementacja w `PowerShellService.cs` linia 100
  - Obsługuje OBO flow przez TokenManager

### ⚠️ ODKRYCIA KRYTYCZNE:
1. **NIESPÓJNOŚĆ W PODEJŚCIU**: Niektóre serwisy używają ręcznego `ConnectWithAccessTokenAsync`, inne `ExecuteWithAutoConnectAsync`
2. **ChannelService jako WZORZEC**: Jedyny serwis konsekwentnie używający `ExecuteWithAutoConnectAsync`
3. **BEZPOŚREDNIE POŁĄCZENIA W TEAMSERVICE**: Wszystkie operacje CRUD używają ręcznego połączenia

---

## 2. Istniejące mechanizmy synchronizacji

### ✅ ZNALEZIONE - PEŁNA SYNCHRONIZACJA:
#### **ChannelService.MapPsObjectToLocalChannel** ⭐ **WZORZEC DO NAŚLADOWANIA**
```55:128:TeamsManager.Core/Services/ChannelService.cs
private Channel MapPsObjectToLocalChannel(PSObject psChannel, string localTeamId)
{
    var graphChannelId = PSObjectMapper.GetString(psChannel, "id") ?? Guid.NewGuid().ToString();
    var channel = new Channel
    {
        Id = graphChannelId,
        TeamId = localTeamId,
        DisplayName = PSObjectMapper.GetString(psChannel, "displayName") ?? "Nieznany Kanał",
        Description = PSObjectMapper.GetString(psChannel, "description"),
        ChannelType = PSObjectMapper.GetString(psChannel, "membershipType") ?? "Standard",
        // ... pełne mapowanie wszystkich właściwości
    };
    // Walidacja biznesowa i logika
    return channel;
}
```

#### **ChannelService.GetTeamChannelsAsync** ⭐ **KOMPLETNA SYNCHRONIZACJA**
```139:238:TeamsManager.Core/Services/ChannelService.cs
// Synchronizacja kanałów z Graph do lokalnej bazy
var graphChannelIds = new HashSet<string>(channelsFromGraph.Select(c => c.Id));

foreach (var graphChannel in channelsFromGraph)
{
    var localChannel = localChannels.FirstOrDefault(lc => lc.Id == graphChannel.Id);
    if (localChannel == null)
    {
        // DODANIE NOWEGO
        graphChannel.CreatedBy = currentUser;
        await _channelRepository.AddAsync(graphChannel);
    }
    else
    {
        // AKTUALIZACJA ISTNIEJĄCEGO - pełna synchronizacja pól
        bool updated = false;
        if (localChannel.DisplayName != graphChannel.DisplayName) { /* aktualizacja */ }
        // ... wszystkie pola
        if (updated) {
            localChannel.MarkAsModified(currentUser);
            _channelRepository.Update(localChannel);
        }
    }
}

// OZNACZANIE USUNIĘTYCH
foreach (var localChannel in localChannels.Where(lc => lc.Status == ChannelStatus.Active))
{
    if (!graphChannelIds.Contains(localChannel.Id))
    {
        localChannel.Archive($"Kanał usunięty z Microsoft Teams", currentUser);
        _channelRepository.Update(localChannel);
    }
}
```

### ❌ BRAK SYNCHRONIZACJI:
#### **TeamService.GetTeamByIdAsync** ⚠️ **KOMENTARZ O BRAKU IMPLEMENTACJI**
```128:131:TeamsManager.Core/Services/TeamService.cs
var psTeam = await _powerShellTeamService.GetTeamAsync(teamId);
if (psTeam != null)
{
    _logger.LogDebug("Zespół ID: {TeamId} znaleziony w Graph API. Synchronizacja z lokalną bazą (niezaimplementowana).", teamId);
}
```

#### **UserService** ❌ **BRAK PRÓB SYNCHRONIZACJI**
- Żadna metoda nie próbuje synchronizować danych użytkownika z Graph przy pobieraniu
- Operacje CRUD idą tylko w jedną stronę: Local→Graph

---

## 3. Użycie cache

### ⚠️ PODWÓJNE PODEJŚCIE - PROBLEM DO ROZWIĄZANIA:
#### **ChannelService** - MIESZANE UŻYCIE:
```30:31,56:57:TeamsManager.Core/Services/ChannelService.cs
private readonly IMemoryCache _cache;
private readonly IPowerShellCacheService _powerShellCacheService;

private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
{
    return _powerShellCacheService.GetDefaultCacheEntryOptions();
}
```
- **PROBLEM**: Używa IMemoryCache do przechowywania ale IPowerShellCacheService do konfiguracji
- **NIEBEZPIECZEŃSTWO**: Różne mechanizmy invalidacji

### Tylko IMemoryCache:
1. **DepartmentService** ✅ + delegacja do PowerShellCacheService
2. **SubjectService** ✅ + delegacja do PowerShellCacheService  
3. **SchoolYearService** ✅ + delegacja do PowerShellCacheService
4. **TeamTemplateService** ✅ + delegacja do PowerShellCacheService
5. **ApplicationSettingService** ✅ używa tylko IPowerShellCacheService

### Tylko IPowerShellCacheService:
1. **TeamService** ✅ **KONSEKWENTNE UŻYCIE**
2. **UserService** ✅ **KONSEKWENTNE UŻYCIE**
3. **PowerShellCacheService** ✅ **GŁÓWNY SERWIS**

### ✅ DOBRY WZORZEC - Delegacja:
```59:63:TeamsManager.Core/Services/DepartmentService.cs
private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
{
    return _powerShellCacheService.GetDefaultCacheEntryOptions();
}
```

---

## 4. Wzorce transakcyjne

### ❌ NIE ZNALEZIONO SaveChangesAsync w serwisach:
- **BRAK** bezpośrednich wywołań `SaveChangesAsync()` w warstwie serwisów
- **ZNALEZIONO** komentarz: `// SaveChangesAsync na wyższym poziomie` w ChannelService
- **BRAK** implementacji Unit of Work pattern

### ✅ ZNALEZIONO SaveChangesAsync w DbContext:
```531:537:TeamsManager.Data/TeamsManagerDbContext.cs
public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
{
    SetAuditFields();
    return await base.SaveChangesAsync(cancellationToken);
}
```

### Miejsca wymagające transakcji:
1. **Operacje bulk w TeamService** - AddUsers/RemoveUsers
2. **Tworzenie zespołu z członkami** - wieloetapowe operacje
3. **Synchronizacja z Graph** - wiele aktualizacji jednocześnie
4. **ChannelService synchronizacja** - Add/Update/Archive w jednej operacji

---

## 5. PowerShellCacheService - możliwości

### ✅ Istniejące funkcje podstawowe:
```20:60:TeamsManager.Core/Abstractions/Services/PowerShell/IPowerShellCacheService.cs
Task<string?> GetUserIdAsync(string userUpn, bool forceRefresh = false);
void SetUserId(string userUpn, string userId);
bool TryGetValue<T>(string key, out T? value);
void Set<T>(string key, T value, TimeSpan? duration = null);
void Remove(string key);
```

### ✅ Funkcje invalidacji:
```53:85:TeamsManager.Core/Abstractions/Services/PowerShell/IPowerShellCacheService.cs
void InvalidateUserCache(string? userId = null, string? userUpn = null);
void InvalidateTeamCache(string teamId);
void InvalidateAllCache();
void InvalidateChannelsForTeam(string teamId);
void InvalidateChannel(string channelId);
void InvalidateChannelAndTeam(string teamId, string channelId);
void InvalidateDepartment(string departmentId);
```

### ✅ Funkcje P2 (już zaimplementowane!):
```631:741:TeamsManager.Core/Services/PowerShell/PowerShellCacheService.cs
// [P2-OPTIMIZATION] Smart cache warming
public async Task WarmCacheAsync(string cacheKey, Func<Task<object>> dataLoader, TimeSpan? duration = null)

// [P2-OPTIMIZATION] Pagination support
public bool TryGetPagedValue<T>(string baseCacheKey, int pageNumber, int pageSize, out T? value)
public void SetPagedValue<T>(string baseCacheKey, int pageNumber, int pageSize, T value, TimeSpan? duration = null)

// [P2-OPTIMIZATION] Smart pattern-based invalidation
public void InvalidateByPattern(string pattern, string operationName = "PatternInvalidation")

// [P2-OPTIMIZATION] Batch invalidation to reduce cache stampedes
public void BatchInvalidateKeys(IEnumerable<string> cacheKeys, string operationName = "BatchInvalidation")

// [P2-MONITORING] Cache metrics and performance tracking
public CacheMetrics GetCacheMetrics()
private void RecordCacheOperation(string operationName, long durationMs, int itemCount = 1)
```

### ❌ Brakujące funkcje:
- Brak hierarchicznych kluczy cache (team.channels.*)
- Brak automatycznej invalidacji po synchronizacji Graph→DB

---

## 6. Potencjalne konflikty

### 🔴 WYSOKIE RYZYKO:
#### **PowerShellCacheService ma już rozszerzone funkcje P2**
- ❌ **NIE DUPLIKUJ** `BatchInvalidateKeys` - już istnieje
- ❌ **NIE DUPLIKUJ** `GetCacheMetrics` - już zaimplementowane  
- ❌ **NIE DUPLIKUJ** `WarmCacheAsync` - już działa
- ❌ **NIE DUPLIKUJ** `InvalidateByPattern` - już gotowe

#### **ChannelService ma pełną synchronizację Graph→DB**
- ✅ **UŻYJ JAKO WZORZEC** - nie reimplementuj
- ✅ **ROZSZERZ NA INNE SERWISY** - TeamService, UserService

### 🟡 ŚREDNIE RYZYKO:
#### **Mieszane użycie cache** - ChannelService
- **WYMAGA UJEDNOLICENIA** przed refaktoryzacją
- Albo pełny IMemoryCache, albo pełny IPowerShellCacheService

#### **Brak Unit of Work** 
- **MOŻE WYMAGAĆ ZMIAN W KONTROLERACH**
- Obecnie SaveChangesAsync wywoływane na poziomie kontrolera/repozytorium

### 🟢 NISKIE RYZYKO:
#### **Niespójność ExecuteWithAutoConnectAsync vs ConnectWithAccessTokenAsync**
- Łatwe do zunifikowania - użyj ExecuteWithAutoConnectAsync wszędzie

---

## 7. Rekomendacje dla następnych etapów

### ❌ NIE IMPLEMENTUJ (już istnieje):
1. **Mapowania PSObject** - użyj wzorca `MapPsObjectToLocalChannel`
2. **Rozszerzonych funkcji cache** - P2 funkcje już są w PowerShellCacheService  
3. **Batch invalidation** - `BatchInvalidateKeys` już działa
4. **Cache metrics** - `GetCacheMetrics` już zaimplementowane
5. **Pattern invalidation** - `InvalidateByPattern` już istnieje

### ✅ ZAIMPLEMENTUJ (brakuje):
1. **Synchronizację w TeamService** - brak mapowania PSTeam→Team
2. **Synchronizację w UserService** - brak Graph→DB sync
3. **Unit of Work pattern** - obecnie brak transakcji
4. **Spójne użycie ExecuteWithAutoConnectAsync** - część serwisów używa ręcznego ConnectWithAccessTokenAsync
5. **Ujednolicenie cache** - ChannelService ma mixed approach

### ⚠️ ZACHOWAJ OSTROŻNOŚĆ:
1. **ChannelService jako wzorzec** - ma działającą pełną synchronizację Graph↔DB
2. **PowerShellCacheService P2** - nie nadpisuj istniejących funkcji zaawansowanych
3. **Kompatybilność API** - kontrolery mogą wymagać zmian przy dodaniu Unit of Work

---

## 8. Następne kroki

### Etap 2: Implementacja mapowania PSObject→Entity
- Wzoruj się na `MapPsObjectToLocalChannel`
- Stwórz `MapPsObjectToLocalTeam` i `MapPsObjectToLocalUser`
- Dodaj walidację biznesową

### Etap 3: Unit of Work pattern
- Implementuj w DbContext
- Zmień serwisy aby nie wywoływały SaveChanges bezpośrednio
- Dodaj transakcje dla operacji bulk

### Etap 4: Migracja na ExecuteWithAutoConnectAsync
- TeamService: 5 metod do zmigowania
- UserService: 2 metody do zmigowania
- Usunąć ręczne `ConnectWithAccessTokenAsync`

### Etap 5: Ujednolicenie cache
- ChannelService: zdecyduj IMemoryCache vs IPowerShellCacheService
- Dodaj hierarchiczne klucze cache (team.channels.*)

### Etap 6: Synchronizacja automatyczna
- Dodaj okresową synchronizację w tle
- Użyj istniejących funkcji P2 cache do optymalizacji

---

## ⚠️ KRYTYCZNE OSTRZEŻENIA dla dalszych etapów

1. **NIGDY nie duplikuj funkcji P2** - PowerShellCacheService ma pełną implementację zaawansowanych funkcji
2. **ZAWSZE użyj ChannelService jako wzorca** - jedyny serwis z pełną synchronizacją Graph↔DB  
3. **PAMIĘTAJ o funkcjach batch** - `BatchInvalidateKeys`, `GetCacheMetrics` już działają
4. **SPRAWDŹ testy** - mogą pokazać oczekiwane zachowanie synchronizacji
5. **ZADBAJ o kompatybilność** - kontrolery mogą mieć własną logikę transakcyjną

**Następny etap:** Implementacja mapowania PSObject→Entity na wzór ChannelService 🎯 