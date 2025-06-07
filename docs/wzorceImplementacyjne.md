# Wzorce Implementacyjne TeamsManager - Dokument Referencyjny

**Wersja:** 1.0  
**Data:** 2025-06-07  
**Cel:** Centralizacja wzorców projektowych i implementacyjnych używanych w aplikacji TeamsManager

## Spis Treści

1. [Wzorzec Zarządzania Kontekstem Użytkownika](#1-wzorzec-zarządzania-kontekstem-użytkownika)
2. [Wzorzec Cacheowania Wielopoziomowego](#2-wzorzec-cacheowania-wielopoziomowego)  
3. [Wzorzec Thread-Safety w Orkiestratorach](#3-wzorzec-thread-safety-w-orkiestratorach)
4. [Wzorzec Auditowania i Śledzenia Operacji](#4-wzorzec-auditowania-i-śledzenia-operacji)
5. [Wzorzec Obsługi Błędów i Odpowiedzi API](#5-wzorzec-obsługi-błędów-i-odpowiedzi-api)
6. [Wzorzec Walidacji Argumentów w Konstruktorach](#6-wzorzec-walidacji-argumentów-w-konstruktorach)
7. [Wzorzec Lazy Loading i Inicjalizacji](#7-wzorzec-lazy-loading-i-inicjalizacji)
8. [Wzorzec Repository z Cache](#8-wzorzec-repository-z-cache)
9. [Wzorzec Circuit Breaker](#9-wzorzec-circuit-breaker)
10. [Wzorzec Unit of Work](#10-wzorzec-unit-of-work)
11. [Wzorzec Powiadomień i Notyfikacji](#11-wzorzec-powiadomień-i-notyfikacji)
12. [Wzorzec Batch Processing](#12-wzorzec-batch-processing)
13. [Instrukcje natury ogólnej](#13-instrukcje-ogolne)

---
s
## 1. Wzorzec Zarządzania Kontekstem Użytkownika

### Opis
Centralny wzorzec zarządzania tożsamością użytkownika w całej aplikacji z fallback na wartości systemowe.

### Implementacja
```csharp
// Interface
public interface ICurrentUserService
{
    string? GetCurrentUserUpn();
    void SetCurrentUserUpn(string? upn);
    string? GetCurrentUserId();
    bool IsAuthenticated => !string.IsNullOrWhiteSpace(GetCurrentUserUpn());
}

// Implementacja z priorytetami
public string? GetCurrentUserUpn()
{
    // Priorytet 1: HTTP Context (JWT claims)
    if (_httpContextAccessor?.HttpContext?.User?.Identity?.IsAuthenticated == true)
    {
        var upnClaim = _httpContextAccessor.HttpContext.User.FindFirst("preferred_username")?.Value ??
                       _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Name)?.Value ??
                       _httpContextAccessor.HttpContext.User.FindFirst(ClaimTypes.Upn)?.Value;
        if (!string.IsNullOrEmpty(upnClaim)) return upnClaim;
    }
    
    // Priorytet 2: Ręcznie ustawiony (testy/UI)
    if (_manualUserUpn != null) return _manualUserUpn;
    
    // Priorytet 3: Domyślna wartość systemowa
    return _defaultUserUpn; // "system@teamsmanager.local"
}
```

### Przykłady użycia w kodzie
- **TeamsManager.Core.Services.UserContext.CurrentUserService** - główna implementacja
- **TeamsManager.Tests.Infrastructure.Services.TestCurrentUserService** - implementacja testowa
- **TeamsManager.Core.Services.OperationHistoryService** - wykorzystanie w audycie
- **TeamsManager.Core.Services.PowerShell.PowerShellConnectionService** - scoped access

### Wartości domyślne
```csharp
public const string SystemUser = "system";
public const string SystemActivityUpdate = "system_activity_update";
public const string SystemBulkOperation = "system_bulk_operation";
public const string SystemMigration = "system_migration";
public const string SystemUsageStats = "system_usage_stats";
```

### Kiedy stosować
- Każdy serwis wymagający informacji o użytkowniku
- Operacje auditowania
- Mechanizmy autoryzacji
- Logowanie operacji

---

## 2. Wzorzec Cacheowania Wielopoziomowego

### Opis
Złożony system cache z hierarchią poziomów: MemoryCache (serwisy) → PowerShellCacheService (współdzielony) z automatyczną inwalidacją.

### Implementacja
```csharp
// Definicje kluczy cache
private const string AllActiveUsersCacheKey = "Users_AllActive";
private const string UserByIdCacheKeyPrefix = "User_Id_";
private const string UserByUpnCacheKeyPrefix = "User_Upn_";

// Opcje cache z tokenem unieważniania
public MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
{
    return new MemoryCacheEntryOptions()
        .SetAbsoluteExpiration(_defaultCacheDuration) // 15-30 minut
        .AddExpirationToken(new CancellationChangeToken(_cacheTokenSource.Token));
}

// Pattern try-get-or-load
public async Task<List<User>> GetAllActiveUsersAsync(bool forceRefresh = false)
{
    if (!forceRefresh && _cache.TryGetValue(AllActiveUsersCacheKey, out List<User>? cachedUsers))
    {
        return cachedUsers!;
    }
    
    var users = await _userRepository.GetAllActiveAsync();
    _cache.Set(AllActiveUsersCacheKey, users, GetDefaultCacheEntryOptions());
    return users;
}
```

### Struktura kluczy cache
```csharp
// Użytkownicy
"Users_AllActive"
"User_Id_{userId}"
"User_Upn_{userUpn}"
"Users_Role_{roleType}"

// Zespoły  
"Teams_AllActive"
"Team_Id_{teamId}"
"Teams_ByOwner_{ownerUpn}"

// PowerShell/Graph
"PowerShell_GraphContext"
"PowerShell_UserId_{userUpn}"
"PowerShell_Team_{teamId}"
```

### Inwalidacja kaskadowa
```csharp
public class CascadeInvalidationStrategy
{
    private readonly Dictionary<string, List<CascadeRule>> _cascadeRules = new()
    {
        ["User.Deactivated"] = new List<CascadeRule>
        {
            new("Department", user => new[] { $"Department_UsersIn_Id_{user.DepartmentId}" }),
            new("Teams", user => user.TeamMemberships?.Select(tm => $"Team_Members_{tm.TeamId}").ToArray())
        }
    };
}
```

### Przykłady implementacji
- **TeamsManager.Core.Services.PowerShell.PowerShellCacheService** - centralny cache
- **TeamsManager.Core.Services.UserService** - cache serwisu domenowego  
- **TeamsManager.Core.Services.Cache.CacheInvalidationService** - inwalidacja kaskadowa
- **TeamsManager.Core.Services.DepartmentService** - cache z metrykami

### Kiedy stosować
- Często odpytywane dane (listy użytkowników, zespołów)
- Drogie operacje PowerShell/Graph API
- Hierarchiczne struktury danych (działy → użytkownicy)

---

## 3. Wzorzec Thread-Safety w Orkiestratorach

### Opis
Standardowy wzorzec orkiestratorów z kontrolą współbieżności, zarządzaniem procesami i bezpiecznym anulowaniem.

### Implementacja
```csharp
public class ExampleOrchestrator : IExampleOrchestrator
{
    private readonly SemaphoreSlim _processSemaphore;
    private readonly ConcurrentDictionary<string, ProcessStatus> _activeProcesses;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _cancellationTokens;

    public ExampleOrchestrator(/* dependencies */)
    {
        _processSemaphore = new SemaphoreSlim(2, 2); // Limit równoległych procesów
        _activeProcesses = new ConcurrentDictionary<string, ProcessStatus>();
        _cancellationTokens = new ConcurrentDictionary<string, CancellationTokenSource>();
    }

    public async Task<BulkOperationResult> ProcessAsync(/* parameters */)
    {
        var processId = Guid.NewGuid().ToString();
        var cts = new CancellationTokenSource();
        
        // Rejestracja procesu
        _cancellationTokens[processId] = cts;
        _activeProcesses[processId] = new ProcessStatus 
        { 
            ProcessId = processId,
            Status = "Running",
            StartedAt = DateTime.UtcNow 
        };

        await _processSemaphore.WaitAsync(cts.Token);
        
        try
        {
            // Logika biznesowa z sprawdzaniem anulowania
            foreach (var batch in batches)
            {
                if (cts.Token.IsCancellationRequested) break;
                
                // Przetwarzanie batch
                await ProcessBatchAsync(batch, cts.Token);
            }
            
            return CreateSuccessResult();
        }
        catch (OperationCanceledException)
        {
            return CreateCancelledResult();
        }
        finally
        {
            _processSemaphore.Release();
            _cancellationTokens.TryRemove(processId, out _);
            if (_activeProcesses.TryRemove(processId, out var status))
            {
                status.CompletedAt = DateTime.UtcNow;
            }
        }
    }
}
```

### Konfiguracja SemaphoreSlim
```csharp
// Orkiestratory szkolne (drogie operacje)
new SemaphoreSlim(2, 2)

// Orkiestratory użytkowników (średnie obciążenie)  
new SemaphoreSlim(3, 3)

// PowerShell bulk operations (kontrola API)
new SemaphoreSlim(3, 3)
```

### Przykłady implementacji
- **TeamsManager.Application.Services.SchoolYearProcessOrchestrator** - wzorzec bazowy
- **TeamsManager.Application.Services.BulkUserManagementOrchestrator** - zarządzanie użytkownikami
- **TeamsManager.Application.Services.TeamLifecycleOrchestrator** - lifecycle zespołów
- **TeamsManager.Application.Services.HealthMonitoringOrchestrator** - monitoring
- **TeamsManager.Application.Services.ReportingOrchestrator** - raportowanie

### Kiedy stosować
- Długotrwałe operacje masowe
- Operacje wymagające kontroli współbieżności
- Procesy z możliwością anulowania
- Integracje z zewnętrznymi API

---

## 4. Wzorzec Auditowania i Śledzenia Operacji

### Opis
Kompleksowy wzorzec logowania wszystkich operacji biznesowych z progression tracking i szczegółowymi metadanymi.

### Implementacja
```csharp
// Rozpoczęcie operacji
var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
    OperationType.TeamCreated,
    nameof(Team),
    targetEntityId: teamId,
    targetEntityName: teamName,
    details: JsonSerializer.Serialize(new { Options = createOptions })
);

try
{
    // Logika biznesowa z aktualizacją postępu
    await _operationHistoryService.UpdateOperationProgressAsync(
        operation.Id,
        processedItems: processedCount,
        failedItems: failedCount,
        totalItems: totalCount
    );
    
    // Zakończenie sukcesu
    await _operationHistoryService.UpdateOperationStatusAsync(
        operation.Id,
        OperationStatus.Completed,
        $"Processed {processedCount} items successfully"
    );
}
catch (Exception ex)
{
    // Zakończenie błędu
    await _operationHistoryService.UpdateOperationStatusAsync(
        operation.Id,
        OperationStatus.Failed,
        ex.Message,
        ex.StackTrace
    );
    throw;
}
```

### Model audytu
```csharp
public class OperationHistory : BaseEntity
{
    public OperationType Type { get; set; }
    public string TargetEntityType { get; set; }
    public string TargetEntityId { get; set; }
    public string TargetEntityName { get; set; }
    public OperationStatus Status { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public TimeSpan? Duration { get; set; }
    
    // Progress tracking
    public int? TotalItems { get; set; }
    public int? ProcessedItems { get; set; }
    public int? FailedItems { get; set; }
    
    // Audit fields
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
    public string? UserIpAddress { get; set; }
    public string? SessionId { get; set; }
}
```

### Przykłady implementacji
- **TeamsManager.Core.Services.OperationHistoryService** - centralny serwis audytu
- **TeamsManager.Core.Services.TeamService** - audyt operacji zespołowych
- **TeamsManager.Core.Services.PowerShell.PowerShellBulkOperationsService** - audyt PowerShell
- **TeamsManager.Core.Services.PowerShell.PowerShellConnectionService** - audyt połączeń

### Kiedy stosować
- Wszystkie operacje zmieniające stan systemu
- Operacje masowe/wsadowe
- Krytyczne operacje biznesowe
- Integracje z zewnętrznymi systemami

---

## 5. Wzorzec Obsługi Błędów i Odpowiedzi API

### Opis
Standardowy wzorzec odpowiedzi API z rozróżnieniem na sukces/błąd i szczegółową obsługą operacji masowych.

### Implementacja
```csharp
// Model odpowiedzi masowej
public class BulkOperationResult
{
    public bool Success { get; set; }
    public bool IsSuccess { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime ProcessedAt { get; set; } = DateTime.UtcNow;
    public List<BulkOperationSuccess> SuccessfulOperations { get; set; } = new();
    public List<BulkOperationError> Errors { get; set; } = new();
    
    public static BulkOperationResult CreateSuccess(string? operationType = null) => new()
    {
        Success = true,
        IsSuccess = true,
        OperationType = operationType
    };
    
    public static BulkOperationResult CreateError(string errorMessage) => new()
    {
        Success = false,
        IsSuccess = false,
        ErrorMessage = errorMessage
    };
}

// Pattern obsługi w kontrolerze
[HttpPost("bulk-operation")]
public async Task<IActionResult> BulkOperation([FromBody] BulkRequest request)
{
    try
    {
        var result = await _orchestrator.ProcessAsync(request.Data, accessToken);
        
        if (result.IsSuccess)
        {
            _logger.LogInformation("✅ API: Operacja zakończona sukcesem. Sukcesy: {Success}, Błędy: {Errors}", 
                result.SuccessfulOperations.Count, result.Errors.Count);
            return Ok(new BulkOperationResponse
            {
                Success = true,
                Message = $"Zakończone. Sukcesy: {result.SuccessfulOperations.Count}",
                Result = result
            });
        }
        else
        {
            _logger.LogWarning("⚠️ API: Operacja z błędami: {ErrorMessage}", result.ErrorMessage);
            return BadRequest(new BulkOperationResponse
            {
                Success = false,
                Message = result.ErrorMessage ?? "Wystąpiły błędy",
                Result = result
            });
        }
    }
    catch (Exception ex)
    {
        _logger.LogError(ex, "❌ API: Błąd podczas operacji");
        return StatusCode(500, new BulkOperationResponse 
        { 
            Success = false, 
            Message = "Wystąpił błąd wewnętrzny serwera" 
        });
    }
}
```

### Wzorzec logowania z emoji
```csharp
_logger.LogInformation("✅ API: Operacja zakończona sukcesem");
_logger.LogWarning("⚠️ API: Operacja z ostrzeżeniami");  
_logger.LogError("❌ API: Błąd krytyczny");
```

### Przykłady implementacji
- **TeamsManager.Api.Controllers.TeamLifecycleController** - operacje lifecycle
- **TeamsManager.Api.Controllers.BulkUserManagementController** - zarządzanie użytkownikami
- **TeamsManager.Core.Models.BulkOperationResult** - model wyników
- **TeamsManager.Application.Services.BulkUserManagementOrchestrator** - logika biznesowa

### Kiedy stosować
- Wszystkie endpointy API
- Operacje masowe z częściowymi sukcesami
- Długotrwałe operacje asynchroniczne
- Integracje wymagające szczegółowych raportów błędów

---

## 6. Wzorzec Walidacji Argumentów w Konstruktorach

### Opis
Standardowy wzorzec walidacji wszystkich zależności w konstruktorach z użyciem null-coalescing operator.

### Implementacja
```csharp
public class ExampleService : IExampleService
{
    private readonly IRepository _repository;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILogger<ExampleService> _logger;
    
    public ExampleService(
        IRepository repository,
        ICurrentUserService currentUserService,
        ILogger<ExampleService> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }
}
```

### Walidacja w metodach publicznych
```csharp
public async Task<User> UpdateUserAsync(User userToUpdate)
{
    if (userToUpdate?.Id == null)
        throw new ArgumentNullException(nameof(userToUpdate), "Obiekt użytkownika lub jego ID nie może być null/pusty.");
    
    // Logika metody
}
```

### Przykłady implementacji
- **TeamsManager.Core.Services.UserService** - pełna walidacja DI
- **TeamsManager.Core.Services.TeamService** - orkiestrator z wieloma zależnościami
- **TeamsManager.Application.Services.BulkUserManagementOrchestrator** - walidacja orkiestratora
- **TeamsManager.Data.UnitOfWork.EfUnitOfWork** - walidacja infrastruktury

### Kiedy stosować
- Wszystkie konstruktory publicznych serwisów
- Metody przyjmujące krytyczne parametry biznesowe
- Fabryki i builderzy
- Serwisy infrastrukturalne

---

## 7. Wzorzec Lazy Loading i Inicjalizacji

### Opis
Wzorzec opóźnionej inicjalizacji dla unikania cyklicznych zależności i optymalizacji wydajności.

### Implementacja
```csharp
public class PowerShellService : IPowerShellService
{
    // Lazy initialization dla serwisów domenowych
    private readonly Lazy<IPowerShellTeamManagementService> _teamService;
    private readonly Lazy<IPowerShellUserManagementService> _userService;
    private readonly Lazy<IPowerShellBulkOperationsService> _bulkOperationsService;

    public PowerShellService(IServiceProvider serviceProvider, /* other deps */)
    {
        // Lazy initialization pozwala uniknąć cyklicznych zależności
        _teamService = new Lazy<IPowerShellTeamManagementService>(() =>
            serviceProvider.GetRequiredService<IPowerShellTeamManagementService>());
        _userService = new Lazy<IPowerShellUserManagementService>(() =>
            serviceProvider.GetRequiredService<IPowerShellUserManagementService>());
    }

    // Właściwości udostępniające lazy-loaded serwisy
    public IPowerShellTeamManagementService Teams => _teamService.Value;
    public IPowerShellUserManagementService Users => _userService.Value;
}
```

### Lazy-loaded repozytoria
```csharp
public class EfUnitOfWork : IUnitOfWork
{
    private IUserRepository? _userRepository;
    private ITeamRepository? _teamRepository;
    
    public IUserRepository Users => 
        _userRepository ??= new UserRepository(_context);
    
    public ITeamRepository Teams => 
        _teamRepository ??= new TeamRepository(_context);
}
```

### Cache warming pattern
```csharp
public async Task WarmCacheAsync(string cacheKey, Func<Task<object>> dataLoader)
{
    if (_cache.TryGetValue(cacheKey, out _)) return; // Already warm
    
    var data = await dataLoader();
    if (data != null)
    {
        Set(cacheKey, data, _defaultCacheDuration);
    }
}
```

### Przykłady implementacji
- **TeamsManager.Core.Services.PowerShell.PowerShellService** - lazy serwisy
- **TeamsManager.Data.UnitOfWork.EfUnitOfWork** - lazy repozytoria
- **TeamsManager.Core.Services.PowerShell.PowerShellCacheService** - cache warming

### Kiedy stosować
- Unikanie cyklicznych zależności
- Drogie do zainicjalizowania serwisy
- Opcjonalne zależności
- Cache warming strategii

---

## 8. Wzorzec Repository z Cache

### Opis
Rozszerzenie wzorca Repository o inteligentne cache'owanie z forceRefresh i automatyczną inwalidacją.

### Implementacja
```csharp
public async Task<List<T>> GetAllActiveAsync(bool forceRefresh = false)
{
    const string cacheKey = "Entities_AllActive";
    
    if (!forceRefresh && _cache.TryGetValue(cacheKey, out List<T>? cached))
    {
        _logger.LogDebug("Cache HIT: {CacheKey}", cacheKey);
        Interlocked.Increment(ref _cacheHits);
        return cached!;
    }
    
    _logger.LogDebug("Cache MISS: {CacheKey}", cacheKey);
    Interlocked.Increment(ref _cacheMisses);
    
    var entities = await _repository.GetAllActiveAsync();
    _cache.Set(cacheKey, entities, GetDefaultCacheEntryOptions());
    
    return entities;
}

// Pattern z semaforem (thundering herd protection)
public async Task<T?> GetByIdAsync(string id, bool forceRefresh = false)
{
    string cacheKey = $"Entity_Id_{id}";
    
    if (!forceRefresh && _cache.TryGetValue(cacheKey, out T? cached))
        return cached;
    
    await _cacheSemaphore.WaitAsync();
    try
    {
        // Double-check pattern
        if (_cache.TryGetValue(cacheKey, out cached))
            return cached;
            
        var entity = await _repository.GetByIdAsync(id);
        if (entity != null)
        {
            _cache.Set(cacheKey, entity, GetDefaultCacheEntryOptions());
        }
        return entity;
    }
    finally
    {
        _cacheSemaphore.Release();
    }
}
```

### Metryki cache
```csharp
// Zbieranie metryk
private long _cacheHits = 0;
private long _cacheMisses = 0;

public CacheMetrics GetCacheMetrics()
{
    var total = _cacheHits + _cacheMisses;
    return new CacheMetrics
    {
        CacheHits = _cacheHits,
        CacheMisses = _cacheMisses,
        HitRate = total > 0 ? (_cacheHits * 100.0 / total) : 0.0,
        TotalOperations = total
    };
}
```

### Przykłady implementacji
- **TeamsManager.Core.Services.UserService** - cache użytkowników
- **TeamsManager.Core.Services.DepartmentService** - cache hierarchiczny
- **TeamsManager.Core.Services.SchoolYearService** - thundering herd protection
- **TeamsManager.Core.Services.SubjectService** - cache z metrykami

### Kiedy stosować
- Często odczytywane dane
- Hierarchiczne struktury
- Drogie zapytania z join
- Dane rzadko się zmieniające

---

## 9. Wzorzec Circuit Breaker

### Opis
Wzorzec odporności na błędy dla zewnętrznych integracji z automatic recovery i event system.

### Implementacja
```csharp
public class ModernCircuitBreaker
{
    private readonly int _failureThreshold;
    private readonly TimeSpan _openDuration;
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    
    private int _failureCount;
    private CircuitState _state = CircuitState.Closed;
    private DateTime _openedAt;

    public event EventHandler<CircuitBreakerStateChangedEventArgs>? StateChanged;
    
    public async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        await _semaphore.WaitAsync();
        try
        {
            if (_state == CircuitState.Open)
            {
                if (DateTime.UtcNow - _openedAt < _openDuration)
                {
                    throw new CircuitBreakerOpenException(
                        $"Circuit breaker is open. Will retry after {_openedAt.Add(_openDuration):HH:mm:ss}");
                }
                _state = CircuitState.HalfOpen;
                StateChanged?.Invoke(this, new CircuitBreakerStateChangedEventArgs(CircuitState.Open, _state));
            }

            try
            {
                var result = await operation();
                OnSuccess();
                return result;
            }
            catch (Exception)
            {
                OnFailure();
                throw;
            }
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
```

### Retry policy integration
```csharp
private readonly TimeSpan _initialRetryDelay = TimeSpan.FromSeconds(1);
private readonly TimeSpan _maxRetryDelay = TimeSpan.FromSeconds(30);
private readonly int _maxRetryAttempts = 3;

for (int attempt = 1; attempt <= _maxRetryAttempts; attempt++)
{
    try
    {
        return await _connectionCircuitBreaker.ExecuteAsync(async () => {
            return await ConnectToGraphAsync(accessToken, scopes);
        });
    }
    catch (CircuitBreakerOpenException)
    {
        _logger.LogWarning("Circuit breaker is open, skipping retry attempt {Attempt}", attempt);
        throw;
    }
    catch (Exception ex) when (attempt < _maxRetryAttempts)
    {
        var delay = TimeSpan.FromMilliseconds(Math.Min(
            _initialRetryDelay.TotalMilliseconds * Math.Pow(2, attempt - 1),
            _maxRetryDelay.TotalMilliseconds));
        
        await Task.Delay(delay);
    }
}
```

### Przykłady implementacji
- **TeamsManager.Core.Common.ModernCircuitBreaker** - nowoczesna implementacja
- **TeamsManager.Core.Services.PowerShell.PowerShellConnectionService** - integracja z Graph API

### Kiedy stosować
- Połączenia z zewnętrznymi API
- Operacje sieciowe
- Zasoby mogące być tymczasowo niedostępne
- Systemy wymagające high availability

---

## 10. Wzorzec Unit of Work

### Opis
Zarządzanie transakcjami i koordynacja pracy wielu repozytoriów z lazy loading i proper disposal.

### Implementacja
```csharp
public class EfUnitOfWork : IUnitOfWork
{
    private readonly TeamsManagerDbContext _context;
    private readonly Dictionary<Type, object> _repositories;
    private IDbContextTransaction? _currentTransaction;
    
    // Lazy-loaded repozytoria
    public IUserRepository Users => 
        _userRepository ??= new UserRepository(_context);

    public async Task<int> SaveChangesAsync()
    {
        try
        {
            return await _context.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas zapisywania zmian w Unit of Work");
            throw;
        }
    }

    public async Task BeginTransactionAsync()
    {
        if (_currentTransaction != null)
            throw new InvalidOperationException("Transakcja jest już aktywna");
        
        _currentTransaction = await _context.Database.BeginTransactionAsync();
        _logger.LogDebug("Rozpoczęto transakcję");
    }

    public async Task CommitTransactionAsync()
    {
        if (_currentTransaction == null)
            throw new InvalidOperationException("Brak aktywnej transakcji");
        
        try
        {
            await _currentTransaction.CommitAsync();
            _logger.LogDebug("Zacommitowano transakcję");
        }
        finally
        {
            await _currentTransaction.DisposeAsync();
            _currentTransaction = null;
        }
    }
}
```

### Pattern użycia
```csharp
// W serwisie biznesowym
public async Task<bool> ComplexBusinessOperationAsync()
{
    await _unitOfWork.BeginTransactionAsync();
    try
    {
        // Operacja 1
        var user = await _unitOfWork.Users.GetByIdAsync(userId);
        user.UpdateStatus(UserStatus.Active);
        
        // Operacja 2
        var team = new Team { /* properties */ };
        await _unitOfWork.Teams.AddAsync(team);
        
        // Operacja 3 - historia
        var operation = await _operationHistoryService.CreateNewOperationEntryAsync(/* params */);
        
        await _unitOfWork.SaveChangesAsync();
        await _unitOfWork.CommitTransactionAsync();
        return true;
    }
    catch (Exception)
    {
        await _unitOfWork.RollbackTransactionAsync();
        throw;
    }
}
```

### Przykłady implementacji
- **TeamsManager.Data.UnitOfWork.EfUnitOfWork** - główna implementacja
- **TeamsManager.Core.Services.UserService** - użycie w serwisach

### Kiedy stosować
- Operacje obejmujące wiele repozytoriów
- Transakcje biznesowe wymagające ACID
- Bulk operations wymagające rollback
- Skomplikowane operacje domenowe

---

## 11. Wzorzec Powiadomień i Notyfikacji

### Opis
Wielopoziomowy system powiadomień: użytkownik → admin → system z różnymi kanałami dostawy.

### Implementacja
```csharp
// Powiadomienia użytkownika
public interface INotificationService
{
    Task SendNotificationToUserAsync(string userUpn, string message, string type = "info");
    Task SendBulkNotificationAsync(List<string> userUpns, string message, string type = "info");
}

// Powiadomienia administratora  
public interface IAdminNotificationService
{
    Task SendUserCreatedNotificationAsync(string newUserUpn, string createdBy, string departmentName, string schoolType);
    Task SendCriticalErrorNotificationAsync(string operation, string errorMessage, string affectedEntity, string stackTrace, string reportedBy);
    Task SendBulkOperationCompletedAsync(string operationType, int successCount, int failureCount, string operatedBy);
}

// Pattern wykorzystania
public async Task<User> CreateUserAsync(CreateUserRequest request)
{
    try
    {
        var user = await _userRepository.CreateAsync(request);
        
        // Powiadomienie użytkownika
        await _notificationService.SendNotificationToUserAsync(
            user.Email,
            "Witamy w systemie TeamsManager!",
            "success"
        );
        
        // Powiadomienie administratora
        await _adminNotificationService.SendUserCreatedNotificationAsync(
            user.Email,
            _currentUserService.GetCurrentUserUpn(),
            user.Department.Name,
            user.SchoolType.Name
        );
        
        return user;
    }
    catch (Exception ex)
    {
        // Powiadomienie o błędzie krytycznym
        await _adminNotificationService.SendCriticalErrorNotificationAsync(
            "CreateUser",
            ex.Message,
            request.Email,
            ex.StackTrace,
            _currentUserService.GetCurrentUserUpn()
        );
        throw;
    }
}
```

### Typy powiadomień
```csharp
// Standardowe typy
public const string INFO = "info";
public const string SUCCESS = "success";
public const string WARNING = "warning";
public const string ERROR = "error";
public const string CRITICAL = "critical";

// Szablony wiadomości
private static readonly Dictionary<string, string> MessageTemplates = new()
{
    ["user.created"] = "Nowy użytkownik {0} został utworzony w dziale {1}",
    ["team.archived"] = "Zespół {0} został zarchiwizowany przez {1}",
    ["bulk.completed"] = "Operacja {0}: {1} sukcesów, {2} błędów",
    ["critical.error"] = "BŁĄD KRYTYCZNY w {0}: {1}"
};
```

### Przykłady implementacji
- **TeamsManager.Core.Services.StubNotificationService** - implementacja podstawowa
- **TeamsManager.Core.Services.StubAdminNotificationService** - powiadomienia admin
- **TeamsManager.Core.Services.TeamService** - integracja w operacjach biznesowych

### Kiedy stosować
- Operacje wymagające potwierdzenia
- Błędy krytyczne wymagające interwencji
- Zakończenie długotrwałych operacji
- Zmiany wymagające akceptacji administratora

---

## 12. Wzorzec Batch Processing

### Opis
Standardowy wzorzec przetwarzania wsadowego z kontrolą współbieżności, progress tracking i error handling.

### Implementacja
```csharp
public async Task<Dictionary<string, BulkOperationResult>> BulkProcessAsync<T>(
    List<T> items, 
    Func<List<T>, Task<Dictionary<string, BulkOperationResult>>> processor,
    int batchSize = 50)
{
    var results = new Dictionary<string, BulkOperationResult>();
    var processedCount = 0;
    var failedCount = 0;

    // Podziel na batche
    var batches = items
        .Select((item, index) => new { item, index })
        .GroupBy(x => x.index / batchSize)
        .Select(g => g.Select(x => x.item).ToList())
        .ToList();

    foreach (var batch in batches)
    {
        await _semaphore.WaitAsync(); // Kontrola współbieżności
        try
        {
            var batchResults = await processor(batch);
            
            foreach (var result in batchResults)
            {
                results[result.Key] = result.Value;
                if (result.Value.Success) processedCount++;
                else failedCount++;
            }

            // Aktualizuj postęp
            await _operationHistoryService.UpdateOperationProgressAsync(
                operationId,
                processedItems: processedCount,
                failedItems: failedCount,
                totalItems: items.Count
            );
        }
        finally
        {
            _semaphore.Release();
        }

        // Throttling między batchami
        if (batch != batches.Last())
        {
            await Task.Delay(1000); // 1 sekunda przerwy
        }
    }

    return results;
}
```

### Configuration pattern
```csharp
// Stałe konfiguracyjne
private const int BatchSize = 50;
private const int ThrottleLimit = 5;
private const int ThrottleDelayMs = 1000;
private readonly SemaphoreSlim _semaphore = new SemaphoreSlim(3, 3);

// PowerShell specific batching
private async Task<Dictionary<string, BulkOperationResult>> ProcessUserBatchAsync(
    string teamId, 
    List<string> userUpns, 
    TeamMemberRole role)
{
    var script = $@"
        $users = @({string.Join(",", userUpns.Select(upn => $"'{upn}'"))})
        $results = @()
        
        $users | ForEach-Object -Parallel {{
            try {{
                Add-TeamUser -GroupId '{teamId}' -User $_ -Role '{role}'
                $results += @{{ UserUpn = $_; Success = $true; Error = $null }}
            }}
            catch {{
                $results += @{{ UserUpn = $_; Success = $false; Error = $_.Exception.Message }}
            }}
        }} -ThrottleLimit {ThrottleLimit}
        
        return $results
    ";
    
    return await ExecutePowerShellBatchAsync(script);
}
```

### Przykłady implementacji
- **TeamsManager.Core.Services.PowerShell.PowerShellBulkOperationsService** - PowerShell batching
- **TeamsManager.Application.Services.BulkUserManagementOrchestrator** - user operations
- **TeamsManager.Application.Services.TeamLifecycleOrchestrator** - team operations

### Kiedy stosować
- Operacje na dużych zbiorach danych (>100 elementów)
- Integracje z API z limitami rate-limiting
- Operacje PowerShell/Graph wymagające throttling
- Długotrwałe operacje wymagające progress tracking

---

## Podsumowanie Wzorców

### Najważniejsze zasady
1. **Konsystencja** - wszystkie serwisy używają tych samych wzorców
2. **Thread-safety** - orkiestratory z SemaphoreSlim i ConcurrentDictionary
3. **Audit trail** - każda operacja biznesowa logowana w OperationHistory
4. **Cache hierarchy** - MemoryCache (serwis) → PowerShellCacheService (shared)
5. **Error handling** - BulkOperationResult z szczegółowymi błędami
6. **Dependency validation** - null-coalescing w konstruktorach
7. **Lazy loading** - unikanie cyklicznych zależności
8. **Notification system** - wielopoziomowe powiadomienia
9. **Batch processing** - standardowe batching z throttling
10. **Circuit breaker** - odporność na błędy zewnętrznych systemów

### Gdzie szukać implementacji
- **Serwisy domenowe**: `TeamsManager.Core/Services/`
- **Orkiestratory**: `TeamsManager.Application/Services/`
- **Kontrolery API**: `TeamsManager.Api/Controllers/`
- **Infrastruktura**: `TeamsManager.Data/`
- **Testy wzorców**: `TeamsManager.Tests/Services/`

Ten dokument powinien być aktualizowany przy wprowadzaniu nowych wzorców lub modyfikacji istniejących. 


## 13. Wzorzec Debugowania Błędów CS0854 - Moq + Parametry Opcjonalne

### Opis
Wzorzec rozwiązywania błędów kompilacji CS0854 ("Expression tree may not contain a call or invocation that uses optional parameters") występujących w testach jednostkowych z biblioteką Moq.

### Przyczyna błędu
```csharp
// BŁĘDNY setup - powoduje CS0854
_departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-id", It.IsAny<bool>()))
    .ReturnsAsync(testDepartment);

// Sygnatura metody z wieloma parametrami opcjonalnymi:
Task<Department?> GetDepartmentByIdAsync(string departmentId, 
    bool includeSubDepartments = false, 
    bool includeUsers = false, 
    bool forceRefresh = false);
```

**Problem**: Moq próbuje używać parametrów opcjonalnych w Expression Tree, co jest zabronione przez C#.

### Rozwiązanie - Podanie wszystkich parametrów explicite
```csharp
// PRAWIDŁOWY setup - wszystkie parametry explicite
_departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-id", 
    It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
    .ReturnsAsync(testDepartment);

// Alternatywne podejście - konkretne wartości
_departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-id", 
    false, false, false))
    .ReturnsAsync(testDepartment);
```

### Najczęstsze przypadki w TeamsManager
```csharp
// GetDepartmentByIdAsync
_departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync(
    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))

// GetTeamByIdAsync  
_teamServiceMock.Setup(x => x.GetTeamByIdAsync(
    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<string>()))

// GetUserByIdAsync
_userServiceMock.Setup(x => x.GetUserByIdAsync(
    It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>()))

// CreateUserAsync  
_userServiceMock.Setup(x => x.CreateUserAsync(
    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), 
    It.IsAny<UserRole>(), It.IsAny<string>(), It.IsAny<string>(), 
    It.IsAny<string>(), It.IsAny<bool>()))

// DeactivateUserAsync
_userServiceMock.Setup(x => x.DeactivateUserAsync(
    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<bool>()))
```

### Wzorzec debugowania
1. **Zidentyfikuj błąd CS0854** w komunikacie kompilera z numerem linii
2. **Znajdź setup Moq** w problematycznej linii
3. **Sprawdź sygnaturę metody** w interfejsie/implementacji
4. **Policz parametry opcjonalne** (domyślne wartości)
5. **Dodaj brakujące It.IsAny<T>()** dla wszystkich parametrów opcjonalnych
6. **Zbuduj projekt** i sprawdź czy błąd zniknął

### Narzędzia diagnostyczne
```bash
# Znajdź wszystkie błędy CS0854
dotnet build TeamsManager.Tests 2>&1 | findstr "CS0854"

# Znajdź wszystkie setupy z potencjalnymi problemami
grep -r "Setup.*It\.IsAny<bool>" TeamsManager.Tests/ 

# Sprawdź sygnatury metod z parametrami opcjonalnymi
grep -r "= false\|= true\|= null" TeamsManager.Core/Abstractions/Services/
```

### Przykłady naprawionych błędów
```csharp
// PRZED (5 błędów CS0854):
_departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", It.IsAny<bool>()))

// PO (0 błędów):
_departmentServiceMock.Setup(x => x.GetDepartmentByIdAsync("dept-matematyka", 
    It.IsAny<bool>(), It.IsAny<bool>(), It.IsAny<bool>()))
```

### Kiedy stosować
- **Każdy setup Moq** dla metod z parametrami opcjonalnymi
- **Debugowanie błędów CS0854** w testach jednostkowych
- **Code review** - sprawdzanie poprawności setupów Moq
- **Refactoring** - dodawanie nowych parametrów opcjonalnych do istniejących metod

### Wnioski techniczne
- **Moq + parametry opcjonalne** = Expression Tree limitations
- **Zawsze podawaj wszystkie parametry explicite** w setupach Moq
- **Dokładna analiza jednego błędu** pozwala znaleźć wzorzec dla wszystkich
- **CS0854 to zawsze problem z parametrami opcjonalnymi** w Expression Trees

---

## 14. Instrukcje natury ogólnej
- **Aktualna data** - zawsze wywołuj komendę powershellową, jeśli chcesz gdzieś umieścić wartość aktualnej daty