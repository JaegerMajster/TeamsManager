# Architektura Synchronizacji Graph-DB

## Przegląd
System implementuje dwukierunkową synchronizację między Microsoft Graph a lokalną bazą danych, zapewniając spójność danych oraz wysoką wydajność dzięki centralnemu systemowi cache z inteligentną inwalidacją.

## Komponenty Główne

### 1. IGraphSynchronizer<T> - Interfejs Synchronizacji
**Lokalizacja**: `TeamsManager.Core/Abstractions/Services/Synchronization/IGraphSynchronizer.cs`

Główny interfejs definiujący kontrakt synchronizacji dla różnych typów encji:

```csharp
public interface IGraphSynchronizer<T> where T : class
{
    Task<bool> RequiresSynchronizationAsync(PSObject graphObject, T? existingEntity);
    Task<T> SynchronizeAsync(PSObject graphObject, T? existingEntity);
}
```

**Implementacje**:
- `TeamSynchronizer` - synchronizacja zespołów Graph→DB
- `UserSynchronizer` - synchronizacja użytkowników z ochroną soft-deleted
- `ChannelSynchronizer` - synchronizacja kanałów z klasyfikacją

### 2. IUnitOfWork - Wzorzec Transakcyjności
**Lokalizacja**: `TeamsManager.Core/Abstractions/Data/IUnitOfWork.cs`

Zapewnia transakcyjność operacji obejmujących Graph+DB:

```csharp
public interface IUnitOfWork : IDisposable
{
    Task<int> SaveChangesAsync();
    Task BeginTransactionAsync();
    Task CommitTransactionAsync();
    Task RollbackTransactionAsync();
}
```

**Implementacja**: `EfUnitOfWork` - implementacja bazująca na Entity Framework

### 3. CacheInvalidationService - Zarządzanie Cache
**Lokalizacja**: `TeamsManager.Core/Services/Cache/CacheInvalidationService.cs`

Centralne zarządzanie inwalidacją cache z granuarną kontrolą:

```csharp
public interface ICacheInvalidationService
{
    Task InvalidateForTeamCreatedAsync(Team team);
    Task InvalidateForTeamUpdatedAsync(Team team);
    Task InvalidateForTeamArchivedAsync(Team team);
    Task InvalidateForUserCreatedAsync(User user);
    Task InvalidateForUserUpdatedAsync(User user);
    Task InvalidateBatchAsync(Dictionary<string, List<string>> operationsMap);
    // ... więcej metod
}
```

**Kluczowe funkcje**:
- Granularna inwalidacja (nie globalna)
- Wsparcie operacji masowych (`InvalidateBatchAsync`)
- Strategia kaskadowa inwalidacji
- Automatyczne usuwanie duplikatów kluczy

## Przepływ Danych

### 1. Standardowy Przepływ Pobierania
```
API Request → Service Layer → Cache Check → DB Query → Graph Sync (jeśli potrzebne) → Response
```

**Szczegółowy proces**:
1. **Cache Check**: Sprawdzenie czy dane są w cache
2. **DB Query**: Pobranie z bazy danych jeśli brak w cache
3. **Graph Sync**: Synchronizacja z Graph jeśli dane nieaktualne
4. **Cache Population**: Zapisanie do cache po synchronizacji
5. **Response**: Zwrócenie danych do API

### 2. Przepływ Synchronizacji (Teams jako przykład)
```csharp
// W TeamService.GetTeamByIdAsync()
var cachedTeam = await _powerShellCacheService.TryGetValue<Team>(cacheKey);
if (cachedTeam.Found && !forceRefresh)
    return cachedTeam.Value;

var dbTeam = await _teamRepository.GetByIdAsync(teamId);
var psTeam = await _powerShellService.ExecuteWithAutoConnectAsync(/*...*/);

if (psTeam != null)
{
    var requiresSync = await _teamSynchronizer.RequiresSynchronizationAsync(psTeam, dbTeam);
    if (requiresSync)
    {
        var syncedTeam = await _teamSynchronizer.SynchronizeAsync(psTeam, dbTeam);
        await _unitOfWork.SaveChangesAsync(); // Transakcyjne zapisanie
        await _cacheInvalidationService.InvalidateForTeamUpdatedAsync(syncedTeam);
        return syncedTeam;
    }
}

await _powerShellCacheService.SetAsync(cacheKey, dbTeam);
return dbTeam;
```

### 3. Inwalidacja Cache

#### Granularna Inwalidacja
Zamiast czyścić cały cache, system inwaliduje tylko powiązane klucze:

```csharp
// Przykład: aktualizacja zespołu inwaliduje:
await _cacheInvalidationService.InvalidateForTeamUpdatedAsync(team);

// Wewnętrznie inwaliduje klucze:
// - "Team_Id_{teamId}"
// - "Teams_AllActive" 
// - "Teams_ByOwner_{ownerUpn}"
// - "Teams_Active" (jeśli status = Active)
// - "Teams_Archived" (jeśli status = Archived)
```

#### Operacje Masowe
```csharp
// Dodanie wielu użytkowników do zespołu
var operationsMap = new Dictionary<string, List<string>>
{
    ["TeamMembersAdded"] = new List<string> 
    { 
        $"Team_Id_{teamId}",
        $"Teams_ByOwner_{team.Owner}",
        "Teams_AllActive"
    }
};
await _cacheInvalidationService.InvalidateBatchAsync(operationsMap);
```

## Synchronizatorы

### TeamSynchronizer
**Odpowiedzialność**: Synchronizacja podstawowych właściwości zespołów

**Kluczowe mapowania**:
- `GraphId` ← `psTeam.Id`
- `ExternalId` ← `psTeam.ExternalId` 
- `DisplayName` ← `psTeam.DisplayName`
- `Owner` ← `psTeam.Owner`
- Wykrywanie zmian przez porównanie właściwości

### UserSynchronizer 
**Odpowiedzialność**: Synchronizacja użytkowników z ochroną soft-deleted

**Specjalne funkcje**:
- **Ochrona Soft-Deleted**: Użytkownicy z `IsActive = false` nie są nadpisywani
- **Mapowanie ról**: Graph roles → lokalne `UserRole`
- **Walidacja departamentów**: Sprawdzenie czy dział istnieje

```csharp
public async Task<bool> RequiresSynchronizationAsync(PSObject graphObject, User? existingEntity)
{
    if (existingEntity?.IsActive == false)
        return false; // Nie sync soft-deleted users
        
    // Pozostała logika wykrywania zmian...
}
```

### ChannelSynchronizer
**Odpowiedzialność**: Synchronizacja kanałów z automatyczną klasyfikacją

**Klasyfikacja typów**:
- `Standard` - zwykłe kanały
- `Private` - kanały prywatne  
- `Shared` - kanały udostępnione
- Auto-klasyfikacja na podstawie właściwości Graph

## PowerShell Integration

### ExecuteWithAutoConnectAsync Pattern
Wszystkie operacje PowerShell używają unified pattern:

```csharp
// Stary sposób (deprecated)
await _powerShellService.ConnectWithAccessTokenAsync(token);
var result = await _powerShellService.Teams.GetTeamAsync(teamId);

// Nowy sposób (Etap 3/8+)
var result = await _powerShellService.ExecuteWithAutoConnectAsync(
    token,
    async () => await _powerShellService.Teams.GetTeamAsync(teamId),
    "Pobieranie zespołu z Graph"
);
```

**Korzyści**:
- Automatyczne zarządzanie połączeniem
- Centralne error handling
- Spójne logowanie operacji
- Retry mechanism w ConnectionService

### PowerShellCacheService
**Rozszerzenie z Etapu 6/8** - centralizacja cache dla operacji PowerShell:

```csharp
// Funkcje P2 (Performance & Persistence)
await _powerShellCacheService.SetAsync(key, value, expiry);
var (found, value) = await _powerShellCacheService.TryGetValueAsync<T>(key);
await _powerShellCacheService.InvalidateByPatternAsync("Teams_*");
```

## Historia Operacji

### OperationHistoryService
Każda krytyczna operacja jest logowana:

```csharp
// 1. Inicjalizacja operacji
var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
    OperationType.TeamCreated,
    nameof(Team),
    targetEntityId: team.Id
);

// 2. Aktualizacja statusu przy sukcesie
await _operationHistoryService.UpdateOperationStatusAsync(
    operation.Id,
    OperationStatus.Completed,
    "Zespół pomyślnie utworzony"
);

// 3. Aktualizacja statusu przy błędzie
await _operationHistoryService.UpdateOperationStatusAsync(
    operation.Id,
    OperationStatus.Failed,
    $"Błąd: {ex.Message}",
    ex.StackTrace
);
```

**Logowane operacje**:
- Create/Update/Delete/Archive dla Teams, Users, Channels
- Operacje masowe (bulk operations)
- Synchronizacja z Graph
- Operacje członkostwa zespołów

## Przykłady Użycia

### 1. Pobieranie Zespołu z Auto-Sync
```csharp
// Kontroler
[HttpGet("{id}")]
public async Task<ActionResult<Team>> GetTeam(string id, bool forceRefresh = false)
{
    var team = await _teamService.GetTeamByIdAsync(id, forceRefresh, GetAccessToken());
    return team != null ? Ok(team) : NotFound();
}

// Service (automatyczna synchronizacja)
public async Task<Team?> GetTeamByIdAsync(string teamId, bool forceRefresh, string? apiAccessToken)
{
    // 1. Cache check
    if (!forceRefresh)
    {
        var cached = await _powerShellCacheService.TryGetValueAsync<Team>($"Team_Id_{teamId}");
        if (cached.Found) return cached.Value;
    }

    // 2. DB query
    var dbTeam = await _teamRepository.GetByIdAsync(teamId);
    
    // 3. Graph sync (jeśli token dostępny)
    if (!string.IsNullOrEmpty(apiAccessToken))
    {
        var psTeam = await _powerShellService.ExecuteWithAutoConnectAsync(/*...*/);
        if (psTeam != null && await _teamSynchronizer.RequiresSynchronizationAsync(psTeam, dbTeam))
        {
            dbTeam = await _teamSynchronizer.SynchronizeAsync(psTeam, dbTeam);
            await _unitOfWork.SaveChangesAsync();
        }
    }
    
    // 4. Cache population
    await _powerShellCacheService.SetAsync($"Team_Id_{teamId}", dbTeam);
    return dbTeam;
}
```

### 2. Operacja z Pełną Inwalidacją Cache
```csharp
public async Task<Team?> CreateTeamAsync(CreateTeamRequest request, string apiAccessToken)
{
    // 1. Historia operacji
    var operation = await _operationHistoryService.CreateNewOperationEntryAsync(/*...*/);
    
    try
    {
        // 2. Transakcja Unit of Work
        await _unitOfWork.BeginTransactionAsync();
        
        // 3. Tworzenie w Graph
        var externalTeamId = await _powerShellService.ExecuteWithAutoConnectAsync(
            apiAccessToken,
            async () => await _powerShellService.Teams.CreateTeamAsync(/*...*/),
            "Tworzenie zespołu w Graph"
        );
        
        // 4. Tworzenie w DB
        var newTeam = new Team { ExternalId = externalTeamId, /*...*/ };
        await _teamRepository.AddAsync(newTeam);
        await _unitOfWork.SaveChangesAsync();
        
        // 5. Commit transakcji
        await _unitOfWork.CommitTransactionAsync();
        
        // 6. Inwalidacja cache
        await _cacheInvalidationService.InvalidateForTeamCreatedAsync(newTeam);
        
        // 7. Sukces w historii
        await _operationHistoryService.UpdateOperationStatusAsync(
            operation.Id, OperationStatus.Completed, "Zespół utworzony pomyślnie"
        );
        
        return newTeam;
    }
    catch (Exception ex)
    {
        await _unitOfWork.RollbackTransactionAsync();
        await _operationHistoryService.UpdateOperationStatusAsync(
            operation.Id, OperationStatus.Failed, $"Błąd: {ex.Message}", ex.StackTrace
        );
        throw;
    }
}
```

### 3. Operacja Masowa z Batch Invalidation
```csharp
public async Task<Dictionary<string, bool>> AddUsersToTeamAsync(string teamId, List<string> userUpns, string apiAccessToken)
{
    // 1. Bulk PowerShell operation
    var results = await _powerShellService.ExecuteWithAutoConnectAsync(
        apiAccessToken,
        async () => await _powerShellService.Bulk.AddUsersToTeamBulkAsync(teamId, userUpns),
        $"Dodawanie {userUpns.Count} użytkowników do zespołu {teamId}"
    );
    
    // 2. Batch cache invalidation
    var addedUserIds = results.Where(r => r.Value).Select(r => GetUserIdByUpn(r.Key)).ToList();
    await _cacheInvalidationService.InvalidateForTeamMembersBulkOperationAsync(teamId, addedUserIds);
    
    return results;
}
```

## Metryki i Monitoring

### Kluczowe Metryki
```csharp
// Wydajność
_metrics.RecordSyncDuration(syncTime);
_metrics.RecordCacheHitRate(cacheHits, totalRequests);
_metrics.RecordGraphApiCalls(apiCallCount);

// Błędy  
_metrics.RecordSyncFailures(failureCount);
_metrics.RecordCacheInvalidationErrors(errorCount);

// Biznes
_metrics.RecordTeamsSynced(teamCount);
_metrics.RecordUsersSynced(userCount);
```

### Oczekiwane Wartości Produkcyjne
- **Cache Hit Rate**: > 80%
- **Sync Duration**: < 500ms per entity
- **API Response Time**: < 100ms (z cache), < 1000ms (z sync)
- **Memory Usage**: < 50MB cache per 1000 entities

## Wytyczne Rozwoju

### Dodawanie Nowego Synchronizatora
1. Implementuj `IGraphSynchronizer<T>`
2. Zarejestruj w DI container (`Program.cs`)
3. Dodaj odpowiednie metody do `CacheInvalidationService`
4. Stwórz testy jednostkowe
5. Zaktualizuj dokumentację

### Optymalizacja Wydajności
- Używaj `ExecuteWithAutoConnectAsync` dla wszystkich operacji PowerShell
- Implementuj granularną inwalidację cache zamiast globalnej
- Korzystaj z `InvalidateBatchAsync` dla operacji masowych
- Monitoruj metryki i dostosowuj cache TTL

### Error Handling
- Wszystkie operacje synchronizacji w try-catch
- Logowanie błędów z pełnym stack trace
- Graceful degradation (brak sync ≠ brak działania)
- Transakcyjność dla operacji krytycznych

## Migracja z Poprzednich Wersji

### Stare vs Nowe Wzorce

| Stary Wzorzec | Nowy Wzorzec | Status |
|---------------|--------------|---------|
| `ConnectWithAccessTokenAsync()` + manual calls | `ExecuteWithAutoConnectAsync()` | ✅ Zmigr. |
| Manual cache invalidation | `CacheInvalidationService` | ✅ Zmigr. |
| Direct repository calls | Unit of Work pattern | ✅ Zmigr. |
| Separate Graph/DB operations | Automatic synchronization | ✅ Zmigr. |

### Sprawdzenie Migracji
Wszystkie główne serwisy zostały zmigrane w Etapach 1-7:
- ✅ **TeamService** - pełna synchronizacja + cache invalidation
- ✅ **UserService** - ExecuteWithAutoConnectAsync pattern  
- ✅ **ChannelService** - ChannelSynchronizer integration
- ✅ **PowerShellCacheService** - rozszerzenie funkcji P2

## Podsumowanie Architektury

System synchronizacji Graph-DB zapewnia:

1. **Spójność Danych**: Automatyczna synchronizacja między Graph a lokalną bazą
2. **Wysoką Wydajność**: Inteligentny cache z granularną inwalidacją
3. **Transakcyjność**: Unit of Work dla operacji krytycznych  
4. **Audytowalność**: Kompletna historia wszystkich operacji
5. **Skalowalność**: Wzorce umożliwiające łatwe rozszerzenie o nowe encje
6. **Niezawodność**: Comprehensive error handling i graceful degradation

Architektura jest gotowa na dalszy rozwój i może być łatwo rozszerzona o nowe typy encji czy dodatkowe źródła danych. 