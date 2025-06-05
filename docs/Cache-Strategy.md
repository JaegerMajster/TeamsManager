# Strategia Cache i Inwalidacji

## Przegląd
System cache w TeamsManager został przeprojektowany w Etapach 6-7/8 refaktoryzacji, przechodząc od prostego IMemoryCache do centralnego systemu z inteligentną inwalidacją i funkcjami P2 (Performance & Persistence).

## Architektura Cache

### Poprzednia Architektura (przed Etapem 6/8)
```
Service → IMemoryCache (bezpośrednio) → Manual invalidation w każdym serwisie
```

**Problemy**:
- Rozproszona logika cache w każdym serwisie
- Brak centrnej kontroli nad inwalidacją  
- Trudność w monitoringu wykorzystania cache
- Ryzyko niespójności danych

### Nowa Architektura (Etap 6-7/8)
```
Service → PowerShellCacheService → CacheInvalidationService → IMemoryCache
```

**Korzyści**:
- Centralizacja zarządzania cache
- Granularna i inteligentna inwalidacja
- Funkcje P2 (Performance & Persistence)
- Kompleksowy monitoring i metryki

## PowerShellCacheService - Etap 6/8

### Lokalizacja
- **Interface**: `TeamsManager.Core/Abstractions/Services/Cache/IPowerShellCacheService.cs`
- **Implementation**: `TeamsManager.Core/Services/PowerShell/PowerShellCacheService.cs`

### Funkcje Podstawowe (P1)
```csharp
public interface IPowerShellCacheService
{
    // Podstawowe operacje cache
    Task<T?> GetAsync<T>(string key) where T : class;
    Task SetAsync<T>(string key, T value, TimeSpan? expiry = null) where T : class;
    Task RemoveAsync(string key);
    Task ClearAsync();
    
    // Sprawdzanie obecności
    Task<bool> ExistsAsync(string key);
    (bool Found, T? Value) TryGetValue<T>(string key) where T : class;
}
```

### Funkcje P2 (Performance & Persistence)
```csharp
public interface IPowerShellCacheService
{
    // Wsadowe operacje (Performance)
    Task SetBatchAsync<T>(Dictionary<string, T> items, TimeSpan? expiry = null) where T : class;
    Task<Dictionary<string, T?>> GetBatchAsync<T>(IEnumerable<string> keys) where T : class;
    Task RemoveBatchAsync(IEnumerable<string> keys);
    
    // Inwalidacja wzorcowa (Performance)
    Task InvalidateByPatternAsync(string pattern);
    Task<IEnumerable<string>> GetKeysByPatternAsync(string pattern);
    
    // Statystyki cache (Performance)
    Task<CacheStatistics> GetStatisticsAsync();
    Task ResetStatisticsAsync();
    
    // Globalną inwalidację (Legacy support)
    void InvalidateAllCache();
}
```

### Przykład Użycia
```csharp
// Przed Etapem 6/8 (TeamService)
_memoryCache.Set(cacheKey, teams, TimeSpan.FromMinutes(15));
var cachedTeams = _memoryCache.Get<IEnumerable<Team>>(cacheKey);

// Po Etapie 6/8 (centralizacja)
await _powerShellCacheService.SetAsync(cacheKey, teams, TimeSpan.FromMinutes(15));
var (found, cachedTeams) = _powerShellCacheService.TryGetValue<IEnumerable<Team>>(cacheKey);
```

## CacheInvalidationService - Etap 7/8

### Lokalizacja
- **Interface**: `TeamsManager.Core/Abstractions/Services/Cache/ICacheInvalidationService.cs`
- **Implementation**: `TeamsManager.Core/Services/Cache/CacheInvalidationService.cs`

### Systematyczna Inwalidacja

#### Operacje na Zespołach
```csharp
public interface ICacheInvalidationService
{
    // Operacje CRUD zespołów
    Task InvalidateForTeamCreatedAsync(Team team);
    Task InvalidateForTeamUpdatedAsync(Team team);
    Task InvalidateForTeamArchivedAsync(Team team);
    Task InvalidateForTeamRestoredAsync(Team team);
    Task InvalidateForTeamDeletedAsync(Team team);
    
    // Operacje członkostwa
    Task InvalidateForTeamMemberAddedAsync(string teamId, string userId);
    Task InvalidateForTeamMemberRemovedAsync(string teamId, string userId);
    Task InvalidateForTeamMembersBulkOperationAsync(string teamId, List<string> userIds);
}
```

#### Operacje na Użytkownikach
```csharp
public interface ICacheInvalidationService
{
    // Operacje CRUD użytkowników
    Task InvalidateForUserCreatedAsync(User user);
    Task InvalidateForUserUpdatedAsync(User user);
    Task InvalidateForUserActivatedAsync(User user);
    Task InvalidateForUserDeactivatedAsync(User user);
    
    // Przypisania ról i departamentów
    Task InvalidateForUserRoleAssignmentAsync(string userId, UserRole newRole, UserRole? oldRole = null);
    Task InvalidateForUserDepartmentAssignmentAsync(string userId, string newDepartmentId, string? oldDepartmentId = null);
}
```

#### Operacje Masowe
```csharp
public interface ICacheInvalidationService
{
    // Batch invalidation dla wydajności
    Task InvalidateBatchAsync(Dictionary<string, List<string>> operationsMap);
    
    // Globalna inwalidacja (emergency)
    Task InvalidateAllCacheAsync();
}
```

### Strategia Kaskadowa Inwalidacji

#### Przykład: Aktualizacja Zespołu
```csharp
public async Task InvalidateForTeamUpdatedAsync(Team team)
{
    var keysToInvalidate = new List<string>
    {
        // 1. Konkretny zespół
        $"Team_Id_{team.Id}",
        
        // 2. Listy globalne
        "Teams_AllActive",
        
        // 3. Listy właściciela 
        $"Teams_ByOwner_{team.Owner}",
        
        // 4. Listy według statusu
        team.Status == TeamStatus.Active ? "Teams_Active" : "Teams_Archived"
    };
    
    await PerformBatchInvalidationAsync(keysToInvalidate, $"Team Updated: {team.Id}");
}
```

#### Przykład: Dodanie Członka Zespołu
```csharp
public async Task InvalidateForTeamMemberAddedAsync(string teamId, string userId)
{
    var keysToInvalidate = new List<string>
    {
        // 1. Zespół i jego członkowie
        $"Team_Id_{teamId}",
        $"Team_Members_{teamId}",
        
        // 2. Listy zespołów użytkownika
        $"User_Teams_{userId}",
        $"User_Id_{userId}", // może zawierać listy zespołów
        
        // 3. Globalne listy zespołów (mogą zawierać liczby członków)
        "Teams_AllActive",
        "Teams_Active"
    };
    
    await PerformBatchInvalidationAsync(keysToInvalidate, $"Team Member Added: User {userId} to Team {teamId}");
}
```

### Automatyczne Deduplikacja Kluczy
```csharp
private async Task PerformBatchInvalidationAsync(List<string> keys, string operationDescription)
{
    // Automatyczne usuwanie duplikatów
    var uniqueKeys = keys.Distinct().ToList();
    
    _logger.LogDebug("Inwalidacja cache dla operacji: {Operation}. Klucze do inwalidacji: {KeyCount} unique z {TotalKeys} total",
        operationDescription, uniqueKeys.Count, keys.Count);
    
    // Delegacja do PowerShellCacheService
    await _cacheService.BatchInvalidateKeys(uniqueKeys, operationDescription);
}
```

## Wzorce Nazw Kluczy Cache

### Konwencje Nazewnictwa
```csharp
// Pojedyncze encje
"Team_Id_{teamId}"
"User_Id_{userId}"
"Channel_Id_{channelId}"

// Listy według kryteriów
"Teams_AllActive"
"Teams_Active" 
"Teams_Archived"
"Teams_ByOwner_{ownerUpn}"
"Users_ByRole_{role}"
"Users_ByDepartment_{departmentId}"

// Relacje
"Team_Members_{teamId}"
"User_Teams_{userId}"
"Channel_Messages_{channelId}"

// Agregacje i statystyki
"Stats_TeamsCount"
"Stats_ActiveUsersCount"
"Dashboard_RecentOperations"
```

### Hierarchia Invalidacji
```
Teams_AllActive
├── Teams_Active (podzbiór)
├── Teams_Archived (podzbiór)
├── Teams_ByOwner_{ownerUpn} (przecięcie)
└── Team_Id_{teamId} (pojedynczy)
    └── Team_Members_{teamId} (relacja)
        └── User_Teams_{userId} (przeciwna relacja)
```

## Strategia TTL (Time To Live)

### Różne TTL dla Różnych Typów Danych
```csharp
public static class CacheTTL
{
    // Dane często zmieniające się
    public static readonly TimeSpan ShortTerm = TimeSpan.FromMinutes(5);   // np. aktywni użytkownicy
    
    // Dane standardowe
    public static readonly TimeSpan Medium = TimeSpan.FromMinutes(15);     // np. zespoły, użytkownicy
    
    // Dane rzadko zmieniające się  
    public static readonly TimeSpan LongTerm = TimeSpan.FromHours(1);      // np. departamenty, szkoły
    
    // Dane bardzo stabilne
    public static readonly TimeSpan Extended = TimeSpan.FromHours(4);      // np. szablony zespołów
    
    // Dane sesyjne/tymczasowe
    public static readonly TimeSpan Session = TimeSpan.FromMinutes(30);    // np. tokeny, połączenia PS
}

// Użycie w serwisach
await _powerShellCacheService.SetAsync(cacheKey, teams, CacheTTL.Medium);
await _powerShellCacheService.SetAsync(statsKey, statistics, CacheTTL.ShortTerm);
```

### Algorytm Doboru TTL
```csharp
private TimeSpan GetTTLForEntity<T>() where T : class
{
    return typeof(T).Name switch
    {
        nameof(Team) => CacheTTL.Medium,
        nameof(User) => CacheTTL.Medium,
        nameof(Channel) => CacheTTL.Medium,
        nameof(Department) => CacheTTL.LongTerm,
        nameof(SchoolType) => CacheTTL.Extended,
        nameof(TeamTemplate) => CacheTTL.Extended,
        _ => CacheTTL.Medium // Domyślny
    };
}
```

## Monitoring i Metryki Cache

### CacheStatistics Model
```csharp
public class CacheStatistics
{
    public int TotalKeys { get; set; }
    public long TotalMemoryUsage { get; set; }
    public int HitCount { get; set; }
    public int MissCount { get; set; }
    public double HitRate => TotalRequests > 0 ? (double)HitCount / TotalRequests * 100 : 0;
    public int TotalRequests => HitCount + MissCount;
    public DateTime LastResetTime { get; set; }
    public TimeSpan UptimeSpan { get; set; }
    public Dictionary<string, int> KeyPatternUsage { get; set; } = new();
}
```

### Dashboard Monitoring
```csharp
// W PowerShellCacheService
public async Task<CacheStatistics> GetStatisticsAsync()
{
    return new CacheStatistics
    {
        TotalKeys = _memoryCache.Count,
        TotalMemoryUsage = GC.GetTotalMemory(false),
        HitCount = _hitCount,
        MissCount = _missCount,
        UptimeSpan = DateTime.UtcNow - _startTime,
        KeyPatternUsage = _keyPatternStats.ToDictionary(kvp => kvp.Key, kvp => kvp.Value)
    };
}

// Użycie w kontrolerze diagnostycznym
[HttpGet("cache-stats")]
public async Task<ActionResult<CacheStatistics>> GetCacheStatistics()
{
    var stats = await _powerShellCacheService.GetStatisticsAsync();
    return Ok(stats);
}
```

### Alerty i Thresholdy
```csharp
public class CacheHealthCheck : IHealthCheck
{
    private readonly IPowerShellCacheService _cacheService;
    
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var stats = await _cacheService.GetStatisticsAsync();
        
        // Sprawdzenie Hit Rate
        if (stats.HitRate < 70)
        {
            return HealthCheckResult.Degraded($"Cache hit rate is low: {stats.HitRate:F1}%");
        }
        
        // Sprawdzenie Memory Usage
        if (stats.TotalMemoryUsage > 100 * 1024 * 1024) // 100MB
        {
            return HealthCheckResult.Degraded($"Cache memory usage is high: {stats.TotalMemoryUsage / (1024 * 1024)}MB");
        }
        
        return HealthCheckResult.Healthy($"Cache healthy. Hit rate: {stats.HitRate:F1}%, Memory: {stats.TotalMemoryUsage / (1024 * 1024)}MB");
    }
}
```

## Performance Patterns

### Batch Operations dla Wydajności
```csharp
// Zamiast wielu pojedynczych wywołań:
foreach (var team in teams)
{
    await _powerShellCacheService.SetAsync($"Team_Id_{team.Id}", team);
}

// Używaj batch operations:
var batchItems = teams.ToDictionary(t => $"Team_Id_{t.Id}", t => t);
await _powerShellCacheService.SetBatchAsync(batchItems, CacheTTL.Medium);
```

### Conditional Invalidation
```csharp
public async Task InvalidateForTeamUpdatedAsync(Team team)
{
    // Inwaliduj tylko jeśli rzeczywiście potrzebne
    var keysToInvalidate = new List<string> { $"Team_Id_{team.Id}" };
    
    // Conditionally add keys based on changes
    if (team.Status == TeamStatus.Active)
        keysToInvalidate.Add("Teams_Active");
    
    if (team.Status == TeamStatus.Archived)
        keysToInvalidate.Add("Teams_Archived");
    
    // Zawsze inwaliduj globalne listy
    keysToInvalidate.Add("Teams_AllActive");
    
    await PerformBatchInvalidationAsync(keysToInvalidate, $"Team Updated: {team.Id}");
}
```

### Lazy Cache Population
```csharp
public async Task<IEnumerable<Team>> GetAllTeamsAsync(bool forceRefresh = false)
{
    const string cacheKey = "Teams_AllActive";
    
    // Sprawdź cache jeśli nie force refresh
    if (!forceRefresh)
    {
        var (found, cachedTeams) = _powerShellCacheService.TryGetValue<IEnumerable<Team>>(cacheKey);
        if (found && cachedTeams != null)
        {
            _logger.LogDebug("Cache hit for {CacheKey}", cacheKey);
            return cachedTeams;
        }
    }
    
    // Pobierz z bazy danych
    _logger.LogDebug("Cache miss for {CacheKey}, fetching from database", cacheKey);
    var teams = await _teamRepository.FindAsync(t => t.IsActive);
    
    // Populate cache asynchronously (fire-and-forget)
    _ = Task.Run(async () =>
    {
        try
        {
            await _powerShellCacheService.SetAsync(cacheKey, teams, CacheTTL.Medium);
            _logger.LogDebug("Cache populated for {CacheKey}", cacheKey);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to populate cache for {CacheKey}", cacheKey);
        }
    });
    
    return teams;
}
```

## Migracja do Nowej Strategii Cache

### Before/After Comparison

#### Przed Etapem 6/8
```csharp
// TeamService.cs - rozproszona logika cache
private void InvalidateCache(string? teamId = null, string? ownerUpn = null, /* 5 more params */)
{
    if (invalidateAll)
    {
        _memoryCache.Clear(); // Nuclear option!
        return;
    }
    
    // Manual invalidation of each key
    _memoryCache.Remove($"Team_Id_{teamId}");
    _memoryCache.Remove("Teams_AllActive");
    if (ownerUpn != null)
        _memoryCache.Remove($"Teams_ByOwner_{ownerUpn}");
    // ... 10 more lines of manual key removal
}
```

#### Po Etapach 6-7/8  
```csharp
// Centralizacja w CacheInvalidationService
await _cacheInvalidationService.InvalidateForTeamUpdatedAsync(team);

// Wewnętrznie: inteligentna inwalidacja z pełnym loggingiem
private async Task PerformBatchInvalidationAsync(List<string> keys, string operationDescription)
{
    var uniqueKeys = keys.Distinct().ToList();
    await _cacheService.BatchInvalidateKeys(uniqueKeys, operationDescription);
}
```

### Migration Checklist

- ✅ **TeamService** - zmigrowali z IMemoryCache na PowerShellCacheService (Etap 6/8)
- ✅ **UserService** - używa IPowerShellCacheService
- ✅ **ChannelService** - zmigrowali w Etapie 6/8  
- ✅ **CacheInvalidationService** - implementacja w Etapie 7/8
- ✅ **Wszystkie serwisy** - integracja z CacheInvalidationService (Etap 7/8)

## Best Practices i Wzorce

### 1. Cache-Aside Pattern
```csharp
public async Task<T?> GetAsync<T>(string key, Func<Task<T?>> factory, TimeSpan? ttl = null) where T : class
{
    // 1. Try cache first
    var (found, cached) = _powerShellCacheService.TryGetValue<T>(key);
    if (found && cached != null)
        return cached;
    
    // 2. Fetch from source
    var value = await factory();
    if (value != null)
    {
        // 3. Populate cache
        await _powerShellCacheService.SetAsync(key, value, ttl ?? CacheTTL.Medium);
    }
    
    return value;
}
```

### 2. Write-Through Pattern (dla krytycznych operacji)
```csharp
public async Task<Team> CreateTeamAsync(Team team)
{
    // 1. Write to database first
    await _teamRepository.AddAsync(team);
    await _unitOfWork.SaveChangesAsync();
    
    // 2. Update cache immediately
    await _powerShellCacheService.SetAsync($"Team_Id_{team.Id}", team, CacheTTL.Medium);
    
    // 3. Invalidate related caches
    await _cacheInvalidationService.InvalidateForTeamCreatedAsync(team);
    
    return team;
}
```

### 3. Graceful Degradation
```csharp
public async Task<IEnumerable<Team>> GetTeamsWithFallback()
{
    try
    {
        // Try cache first
        var (found, cached) = _powerShellCacheService.TryGetValue<IEnumerable<Team>>("Teams_AllActive");
        if (found && cached != null)
            return cached;
    }
    catch (Exception ex)
    {
        _logger.LogWarning(ex, "Cache operation failed, falling back to database");
    }
    
    // Fallback to database
    return await _teamRepository.GetAllActiveAsync();
}
```

### 4. Eventual Consistency
```csharp
// Akceptujemy że cache może być chwilowo nieaktualny
// ale zapewniamy mechanizmy refresh na żądanie
public async Task<Team?> GetTeamByIdAsync(string teamId, bool forceRefresh = false)
{
    if (forceRefresh)
    {
        // Force refresh bypasses cache completely
        await _powerShellCacheService.RemoveAsync($"Team_Id_{teamId}");
    }
    
    return await GetAsync($"Team_Id_{teamId}", 
        async () => await _teamRepository.GetByIdAsync(teamId));
}
```

## Podsumowanie

Strategia cache została gruntownie przeprojektowana w Etapach 6-7/8, zapewniając:

1. **Centralizację**: Jeden punkt kontroli dla wszystkich operacji cache
2. **Inteligencję**: Granularna inwalidacja zamiast globalnej  
3. **Wydajność**: Batch operations i optymalne TTL
4. **Monitoring**: Kompletne metryki i health checks
5. **Skalowalność**: Łatwe rozszerzenie o nowe wzorce kluczy
6. **Niezawodność**: Graceful degradation i fallback mechanisms

System jest gotowy na produkcję i zapewnia wysoką wydajność przy zachowaniu spójności danych. 