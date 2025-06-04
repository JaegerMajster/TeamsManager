using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Primitives;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za logikę biznesową lat szkolnych.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class SchoolYearService : ISchoolYearService
    {
        private readonly ISchoolYearRepository _schoolYearRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolYearService> _logger;
        private readonly ITeamRepository _teamRepository; // Potrzebne do sprawdzania zależności przy usuwaniu
        private readonly IMemoryCache _cache;
        private readonly IOperationHistoryService _operationHistoryService;

        // Klucze cache
        private const string AllSchoolYearsCacheKey = "SchoolYears_AllActive";
        private const string CurrentSchoolYearCacheKey = "SchoolYear_Current";
        private const string SchoolYearByIdCacheKeyPrefix = "SchoolYear_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromHours(1);

        // Token do unieważniania cache'u dla lat szkolnych
        private static CancellationTokenSource _schoolYearsCacheTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Konstruktor serwisu lat szkolnych.
        /// </summary>
        public SchoolYearService(
            ISchoolYearRepository schoolYearRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<SchoolYearService> logger,
            ITeamRepository teamRepository,
            IMemoryCache memoryCache,
            IOperationHistoryService operationHistoryService)
        {
            _schoolYearRepository = schoolYearRepository ?? throw new ArgumentNullException(nameof(schoolYearRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_schoolYearsCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<SchoolYear?> GetSchoolYearByIdAsync(string schoolYearId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie roku szkolnego o ID: {SchoolYearId}. Wymuszenie odświeżenia: {ForceRefresh}", schoolYearId, forceRefresh);

            if (string.IsNullOrWhiteSpace(schoolYearId))
            {
                _logger.LogWarning("Próba pobrania roku szkolnego z pustym ID.");
                return null;
            }

            string cacheKey = SchoolYearByIdCacheKeyPrefix + schoolYearId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out SchoolYear? cachedSchoolYear))
            {
                _logger.LogDebug("Rok szkolny ID: {SchoolYearId} znaleziony w cache.", schoolYearId);
                return cachedSchoolYear;
            }

            _logger.LogDebug("Rok szkolny ID: {SchoolYearId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", schoolYearId);
            var schoolYearFromDb = await _schoolYearRepository.GetByIdAsync(schoolYearId);

            if (schoolYearFromDb != null && schoolYearFromDb.IsActive)
            {
                _cache.Set(cacheKey, schoolYearFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Rok szkolny ID: {SchoolYearId} dodany do cache.", schoolYearId);
            }
            else
            {
                _cache.Remove(cacheKey);
                if (schoolYearFromDb != null && !schoolYearFromDb.IsActive)
                {
                    _logger.LogDebug("Rok szkolny ID: {SchoolYearId} jest nieaktywny, nie zostanie zcache'owany po ID i nie zostanie zwrócony.", schoolYearId);
                    return null; // Zwracamy null dla nieaktywnych
                }
            }
            return schoolYearFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SchoolYear>> GetAllActiveSchoolYearsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych lat szkolnych. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllSchoolYearsCacheKey, out IEnumerable<SchoolYear>? cachedSchoolYears) && cachedSchoolYears != null)
            {
                _logger.LogDebug("Wszystkie aktywne lata szkolne znalezione w cache.");
                return cachedSchoolYears;
            }

            _logger.LogDebug("Wszystkie aktywne lata szkolne nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var schoolYearsFromDb = await _schoolYearRepository.FindAsync(sy => sy.IsActive);

            _cache.Set(AllSchoolYearsCacheKey, schoolYearsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie aktywne lata szkolne dodane do cache.");

            return schoolYearsFromDb;
        }

        /// <inheritdoc />
        public async Task<SchoolYear?> GetCurrentSchoolYearAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie bieżącego roku szkolnego. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(CurrentSchoolYearCacheKey, out SchoolYear? cachedCurrentSchoolYear))
            {
                _logger.LogDebug("Bieżący rok szkolny znaleziony w cache (może być null).");
                return cachedCurrentSchoolYear;
            }

            _logger.LogDebug("Bieżący rok szkolny nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var currentSchoolYearFromDb = await _schoolYearRepository.GetCurrentSchoolYearAsync();

            _cache.Set(CurrentSchoolYearCacheKey, currentSchoolYearFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Bieżący rok szkolny (lub jego brak) dodany do cache.");

            return currentSchoolYearFromDb;
        }

        /// <inheritdoc />
        public async Task<bool> SetCurrentSchoolYearAsync(string schoolYearId)
        {
            _logger.LogInformation("Rozpoczynanie ustawiania roku szkolnego ID: {SchoolYearId} jako bieżący", schoolYearId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SchoolYearSetAsCurrent,
                nameof(SchoolYear),
                targetEntityId: schoolYearId
            );

            string? oldCurrentYearIdToInvalidate = null;

            try
            {
                var newCurrentSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);
                if (newCurrentSchoolYear == null || !newCurrentSchoolYear.IsActive)
                {
                    _logger.LogWarning("Nie można ustawić roku szkolnego ID {SchoolYearId} jako bieżący - nie istnieje lub nieaktywny.", schoolYearId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Rok szkolny o ID '{schoolYearId}' nie istnieje lub jest nieaktywny."
                    );
                    return false;
                }

                if (newCurrentSchoolYear.IsCurrent)
                {
                    _logger.LogInformation("Rok szkolny ID {SchoolYearId} był już ustawiony jako bieżący.", schoolYearId);
                    InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: true, invalidateAll: false);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) był już bieżący. Brak zmian."
                    );
                    return true;
                }

                var currentlyActiveYears = await _schoolYearRepository.FindAsync(sy => sy.IsCurrent && sy.Id != schoolYearId && sy.IsActive);
                foreach (var oldCurrentYear in currentlyActiveYears)
                {
                    oldCurrentYearIdToInvalidate = oldCurrentYear.Id;
                    oldCurrentYear.IsCurrent = false;
                    oldCurrentYear.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system");
                    _schoolYearRepository.Update(oldCurrentYear);
                    _logger.LogInformation("Rok szkolny {OldSchoolYearName} (ID: {OldSchoolYearId}) został odznaczony jako bieżący.", oldCurrentYear.Name, oldCurrentYear.Id);
                }

                newCurrentSchoolYear.IsCurrent = true;
                newCurrentSchoolYear.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system");
                _schoolYearRepository.Update(newCurrentSchoolYear);
                _logger.LogInformation("Rok szkolny {NewSchoolYearName} (ID: {NewSchoolYearId}) został ustawiony jako bieżący.", newCurrentSchoolYear.Name, newCurrentSchoolYear.Id);

                InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: true, invalidateAll: true);
                if (oldCurrentYearIdToInvalidate != null)
                {
                    InvalidateCache(schoolYearId: oldCurrentYearIdToInvalidate, wasOrIsCurrent: false, invalidateAll: false);
                }

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) ustawiony jako bieżący. Poprzednie odznaczone (jeśli były)."
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas ustawiania roku szkolnego ID {SchoolYearId} jako bieżący. Wiadomość: {ErrorMessage}", schoolYearId, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<SchoolYear?> CreateSchoolYearAsync(
            string name,
            DateTime startDate,
            DateTime endDate,
            string? description = null,
            DateTime? firstSemesterStart = null,
            DateTime? firstSemesterEnd = null,
            DateTime? secondSemesterStart = null,
            DateTime? secondSemesterEnd = null)
        {
            _logger.LogInformation("Rozpoczynanie tworzenia roku szkolnego: {Name}", name);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SchoolYearCreated,
                nameof(SchoolYear),
                targetEntityName: name
            );

            try
            {
                if (string.IsNullOrWhiteSpace(name))
                {
                    _logger.LogError("Nie można utworzyć roku szkolnego: Nazwa jest pusta.");
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa roku szkolnego nie może być pusta."
                    );
                    return null;
                }
                if (startDate.Date >= endDate.Date)
                {
                    _logger.LogError("Nie można utworzyć roku szkolnego: Data rozpoczęcia ({StartDate}) nie jest wcześniejsza niż data zakończenia ({EndDate}).", startDate, endDate);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Data rozpoczęcia musi być wcześniejsza niż data zakończenia."
                    );
                    return null;
                }

                var existing = await _schoolYearRepository.GetSchoolYearByNameAsync(name);
                if (existing != null && existing.IsActive)
                {
                    _logger.LogWarning("Aktywny rok szkolny o nazwie '{Name}' już istnieje.", name);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Aktywny rok szkolny o nazwie '{name}' już istnieje."
                    );
                    return null;
                }

                var newSchoolYear = new SchoolYear
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    StartDate = startDate.Date,
                    EndDate = endDate.Date,
                    Description = description ?? string.Empty,
                    FirstSemesterStart = firstSemesterStart?.Date,
                    FirstSemesterEnd = firstSemesterEnd?.Date,
                    SecondSemesterStart = secondSemesterStart?.Date,
                    SecondSemesterEnd = secondSemesterEnd?.Date,
                    IsCurrent = false,
                    IsActive = true
                    // CreatedBy zostanie ustawione przez DbContext
                };

                await _schoolYearRepository.AddAsync(newSchoolYear);

                _logger.LogInformation("Rok szkolny '{Name}' pomyślnie przygotowany do zapisu. ID: {SchoolYearId}", name, newSchoolYear.Id);

                InvalidateCache(schoolYearId: newSchoolYear.Id, wasOrIsCurrent: newSchoolYear.IsCurrent, invalidateAll: true);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Rok szkolny '{newSchoolYear.Name}' (ID: {newSchoolYear.Id}) przygotowany do utworzenia."
                );
                return newSchoolYear;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia roku szkolnego {Name}. Wiadomość: {ErrorMessage}", name, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSchoolYearAsync(SchoolYear schoolYearToUpdate)
        {
            if (schoolYearToUpdate == null || string.IsNullOrEmpty(schoolYearToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji roku szkolnego z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(schoolYearToUpdate), "Obiekt roku szkolnego lub jego ID nie może być null/pusty.");
            }

            _logger.LogInformation("Rozpoczynanie aktualizacji roku szkolnego ID: {SchoolYearId}", schoolYearToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SchoolYearUpdated,
                nameof(SchoolYear),
                targetEntityId: schoolYearToUpdate.Id
            );

            bool wasCurrentBeforeUpdate = false;

            try
            {
                var existingSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearToUpdate.Id);
                if (existingSchoolYear == null) // GetByIdAsync może zwracać null, jeśli rok jest nieaktywny i serwis go tak traktuje
                {
                    // Jeśli chcemy aktualizować także nieaktywne, repozytorium nie powinno ich filtrować w GetByIdAsync
                    // lub powinniśmy użyć FindAsync
                    _logger.LogWarning("Nie można zaktualizować roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Rok szkolny o ID '{schoolYearToUpdate.Id}' nie istnieje."
                    );
                    return false;
                }
                wasCurrentBeforeUpdate = existingSchoolYear.IsCurrent;

                if (string.IsNullOrWhiteSpace(schoolYearToUpdate.Name) || schoolYearToUpdate.StartDate.Date >= schoolYearToUpdate.EndDate.Date)
                {
                    _logger.LogError("Błąd walidacji przy aktualizacji roku szkolnego: {SchoolYearId}. Nazwa: '{Name}', Start: {StartDate}, Koniec: {EndDate}",
                        schoolYearToUpdate.Id, schoolYearToUpdate.Name, schoolYearToUpdate.StartDate, schoolYearToUpdate.EndDate);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Niepoprawne dane wejściowe (nazwa, daty). Nazwa nie może być pusta, a data rozpoczęcia musi być wcześniejsza niż data zakończenia."
                    );
                    return false;
                }

                if (existingSchoolYear.Name != schoolYearToUpdate.Name)
                {
                    var conflicting = await _schoolYearRepository.GetSchoolYearByNameAsync(schoolYearToUpdate.Name);
                    if (conflicting != null && conflicting.Id != existingSchoolYear.Id && conflicting.IsActive)
                    {
                        _logger.LogWarning("Rok szkolny o nazwie '{Name}' już istnieje (inny ID) i jest aktywny.", schoolYearToUpdate.Name);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Aktywny rok szkolny o nazwie '{schoolYearToUpdate.Name}' już istnieje."
                        );
                        return false;
                    }
                }

                existingSchoolYear.Name = schoolYearToUpdate.Name;
                existingSchoolYear.StartDate = schoolYearToUpdate.StartDate.Date;
                existingSchoolYear.EndDate = schoolYearToUpdate.EndDate.Date;
                existingSchoolYear.Description = schoolYearToUpdate.Description ?? string.Empty;
                existingSchoolYear.FirstSemesterStart = schoolYearToUpdate.FirstSemesterStart?.Date;
                existingSchoolYear.FirstSemesterEnd = schoolYearToUpdate.FirstSemesterEnd?.Date;
                existingSchoolYear.SecondSemesterStart = schoolYearToUpdate.SecondSemesterStart?.Date;
                existingSchoolYear.SecondSemesterEnd = schoolYearToUpdate.SecondSemesterEnd?.Date;
                existingSchoolYear.IsActive = schoolYearToUpdate.IsActive; // Pozwalamy na zmianę IsActive rekordu

                if (existingSchoolYear.IsCurrent != schoolYearToUpdate.IsCurrent)
                {
                    _logger.LogWarning("Próba zmiany flagi IsCurrent dla roku szkolnego ID {SchoolYearId} za pomocą UpdateSchoolYearAsync jest ignorowana. Użyj SetCurrentSchoolYearAsync.", existingSchoolYear.Id);
                    // Nie zmieniamy IsCurrent, aby wymusić użycie dedykowanej metody
                }

                existingSchoolYear.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system");
                _schoolYearRepository.Update(existingSchoolYear);

                _logger.LogInformation("Rok szkolny ID: {SchoolYearId} pomyślnie przygotowany do aktualizacji.", existingSchoolYear.Id);

                InvalidateCache(schoolYearId: existingSchoolYear.Id, wasOrIsCurrent: wasCurrentBeforeUpdate || existingSchoolYear.IsCurrent, invalidateAll: true);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Rok szkolny ID: {existingSchoolYear.Id} przygotowany do aktualizacji."
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji roku szkolnego ID {SchoolYearId}. Wiadomość: {ErrorMessage}", schoolYearToUpdate.Id, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSchoolYearAsync(string schoolYearId)
        {
            _logger.LogInformation("Rozpoczynanie usuwania (dezaktywacji) roku szkolnego ID: {SchoolYearId}", schoolYearId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SchoolYearDeleted,
                nameof(SchoolYear),
                targetEntityId: schoolYearId
            );

            SchoolYear? schoolYear = null;
            try
            {
                // Pobierz rok szkolny, nawet jeśli jest nieaktywny, aby móc go usunąć
                var schoolYears = await _schoolYearRepository.FindAsync(sy => sy.Id == schoolYearId);
                schoolYear = schoolYears.FirstOrDefault();

                if (schoolYear == null)
                {
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Rok szkolny o ID '{schoolYearId}' nie istnieje."
                    );
                    return false;
                }

                if (!schoolYear.IsActive)
                {
                    _logger.LogInformation("Rok szkolny ID {SchoolYearId} był już nieaktywny.", schoolYearId);
                    InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: schoolYear.IsCurrent, invalidateAll: true);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Rok szkolny '{schoolYear.Name}' był już nieaktywny."
                    );
                    return true;
                }

                if (schoolYear.IsCurrent)
                {
                    _logger.LogWarning("Nie można usunąć/dezaktywować bieżącego roku szkolnego ID {SchoolYearId}.", schoolYearId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nie można usunąć (dezaktywować) bieżącego roku szkolnego. Najpierw ustaw inny rok jako bieżący."
                    );
                    return false;
                }

                // Używamy nowego, obliczeniowego Team.IsActive, które bazuje na Team.Status
                var teamsUsingYear = await _teamRepository.FindAsync(t => t.SchoolYearId == schoolYearId && t.IsActive);
                if (teamsUsingYear.Any())
                {
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - jest używany przez {Count} aktywnych zespołów (Status=Active).", schoolYearId, teamsUsingYear.Count());
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Nie można usunąć roku szkolnego '{schoolYear.Name}', ponieważ jest nadal używany przez {teamsUsingYear.Count()} aktywnych zespołów (Team.Status = Active)."
                    );
                    return false;
                }

                schoolYear.MarkAsDeleted(_currentUserService.GetCurrentUserUpn() ?? "system"); // To ustawi IsActive = false w BaseEntity
                _schoolYearRepository.Update(schoolYear);

                _logger.LogInformation("Rok szkolny ID {SchoolYearId} pomyślnie oznaczony jako usunięty.", schoolYearId);

                InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: false, invalidateAll: true);
                
                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Rok szkolny '{schoolYear.Name}' (ID: {schoolYearId}) oznaczony jako usunięty."
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania roku szkolnego ID {SchoolYearId}. Wiadomość: {ErrorMessage}", schoolYearId, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false;
            }
        }

        /// <inheritdoc />
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a lat szkolnych.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache lat szkolnych został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private void InvalidateCache(string? schoolYearId = null, bool wasOrIsCurrent = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u lat szkolnych. schoolYearId: {SchoolYearId}, wasOrIsCurrent: {WasOrIsCurrent}, invalidateAll: {InvalidateAll}",
               schoolYearId, wasOrIsCurrent, invalidateAll);

            var oldTokenSource = Interlocked.Exchange(ref _schoolYearsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla lat szkolnych został zresetowany.");

            _cache.Remove(AllSchoolYearsCacheKey);
            _logger.LogDebug("Usunięto z cache klucz: {CacheKey}", AllSchoolYearsCacheKey);

            if (invalidateAll || wasOrIsCurrent) // Jeśli zmieniono bieżący rok lub pełna inwalidacja
            {
                _cache.Remove(CurrentSchoolYearCacheKey);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey} (z powodu invalidateAll lub wasOrIsCurrent)", CurrentSchoolYearCacheKey);
            }

            if (!string.IsNullOrWhiteSpace(schoolYearId))
            {
                _cache.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey}{Id}", SchoolYearByIdCacheKeyPrefix, schoolYearId);
            }
        }
    }
}