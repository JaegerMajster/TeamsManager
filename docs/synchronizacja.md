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

## Integracja PowerShell

### Wzorzec ExecuteWithAutoConnectAsync
Wszystkie operacje PowerShell używają ujednoliconego wzorca:

```csharp
// Stary sposób (przestarzały)
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
- Centralne obsługiwanie błędów
- Spójne logowanie operacji
- Mechanizm ponawiania w ConnectionService

## Wzorce Transakcyjne

### UnitOfWork w Synchronizacji
System używa wzorca UnitOfWork do zapewnienia spójności transakcyjnej między operacjami Graph i bazą danych:

```csharp
// Typowy przepływ synchronizacji
await _unitOfWork.BeginTransactionAsync();
try
{
    // 1. Synchronizacja z Graph
    var updatedEntity = await _synchronizer.SynchronizeAsync(graphData, dbEntity);
    
    // 2. Zapisanie do DB
    await _repository.UpdateAsync(updatedEntity);
    
    // 3. Commit transakcji
    await _unitOfWork.CommitTransactionAsync();
    
    // 4. Inwalidacja cache (po commicie)
    await _cacheInvalidationService.InvalidateForEntityUpdatedAsync(updatedEntity);
}
catch (Exception)
{
    await _unitOfWork.RollbackTransactionAsync();
    throw;
}
```

## Monitoring i Diagnostyka

### Logowanie Synchronizacji
System implementuje szczegółowe logowanie operacji synchronizacji:

```csharp
_logger.LogInformation("Rozpoczęto synchronizację {EntityType} dla {EntityId}", 
    typeof(T).Name, entityId);
    
_logger.LogDebug("Wykryto zmiany w {PropertyCount} właściwościach: {Properties}",
    changedProperties.Count, string.Join(", ", changedProperties));
    
_logger.LogInformation("Synchronizacja {EntityType} zakończona w {Duration}ms",
    typeof(T).Name, stopwatch.ElapsedMilliseconds);
```

### Metryki Wydajności
Każda operacja synchronizacji jest mierzona i raportowana:

```csharp
using var activity = _activitySource.StartActivity($"Synchronize_{typeof(T).Name}");
activity?.SetTag("entity.id", entityId);
activity?.SetTag("sync.required", requiresSync.ToString());

// Wykonanie synchronizacji...

activity?.SetTag("sync.duration_ms", stopwatch.ElapsedMilliseconds);
activity?.SetStatus(ActivityStatusCode.Ok);
```

## Rozszerzalność

### Dodawanie Nowych Synchronizatorów
System jest zaprojektowany do łatwego dodawania nowych typów synchronizacji:

1. **Implementacja interfejsu**:
```csharp
public class CustomEntitySynchronizer : IGraphSynchronizer<CustomEntity>
{
    public async Task<bool> RequiresSynchronizationAsync(PSObject graphObject, CustomEntity? existingEntity)
    {
        // Logika wykrywania zmian
    }

    public async Task<CustomEntity> SynchronizeAsync(PSObject graphObject, CustomEntity? existingEntity)
    {
        // Logika synchronizacji
    }
}
```

2. **Rejestracja w DI**:
```csharp
services.AddScoped<IGraphSynchronizer<CustomEntity>, CustomEntitySynchronizer>();
```

3. **Rozszerzenie CacheInvalidationService**:
```csharp
Task InvalidateForCustomEntityUpdatedAsync(CustomEntity entity);
```

### Konfiguracja Strategii Cache
System pozwala na konfigurację strategii cache per typ encji:

```csharp
public class CacheConfiguration
{
    public TimeSpan DefaultExpiry { get; set; } = TimeSpan.FromMinutes(15);
    public Dictionary<Type, TimeSpan> TypeSpecificExpiry { get; set; } = new();
    public bool EnableBatchInvalidation { get; set; } = true;
    public int MaxBatchSize { get; set; } = 100;
}
```

## Najlepsze Praktyki

### 1. Zarządzanie Konfliktami
- Zawsze sprawdzaj `IsActive` przed synchronizacją użytkowników
- Użyj optimistic locking dla krytycznych operacji
- Implementuj proper error handling z retry logic

### 2. Optymalizacja Wydajności
- Używaj operacji batch dla multiple entities
- Implementuj granularną inwalidację cache
- Monitoruj wielkość cache i częstotliwość hit/miss

### 3. Bezpieczeństwo
- Waliduj wszystkie dane pochodzące z Graph
- Implementuj rate limiting dla Graph API calls
- Używaj secure token storage i rotation 