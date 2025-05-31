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

        // Klucze cache
        private const string AllSchoolYearsCacheKey = "SchoolYears_AllActive";
        private const string CurrentSchoolYearCacheKey = "SchoolYear_Current";
        private const string SchoolYearByIdCacheKeyPrefix = "SchoolYear_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromHours(1); // Lata szkolne zmieniają się rzadko

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
            IMemoryCache memoryCache)
        {
            _schoolYearRepository = schoolYearRepository ?? throw new ArgumentNullException(nameof(schoolYearRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _teamRepository = teamRepository ?? throw new ArgumentNullException(nameof(teamRepository));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_schoolYearsCacheTokenSource.Token));
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
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

            if (schoolYearFromDb != null && schoolYearFromDb.IsActive) // Cache'ujemy tylko aktywne
            {
                _cache.Set(cacheKey, schoolYearFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Rok szkolny ID: {SchoolYearId} dodany do cache.", schoolYearId);
            }
            else
            {
                _cache.Remove(cacheKey);
                if (schoolYearFromDb != null && !schoolYearFromDb.IsActive)
                {
                    _logger.LogDebug("Rok szkolny ID: {SchoolYearId} jest nieaktywny, nie zostanie zcache'owany po ID.", schoolYearId);
                    return null;
                }
            }
            return schoolYearFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
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
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<SchoolYear?> GetCurrentSchoolYearAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie bieżącego roku szkolnego. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(CurrentSchoolYearCacheKey, out SchoolYear? cachedCurrentSchoolYear))
            {
                _logger.LogDebug("Bieżący rok szkolny znaleziony w cache (może być null).");
                return cachedCurrentSchoolYear;
            }

            _logger.LogDebug("Bieżący rok szkolny nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var currentSchoolYearFromDb = await _schoolYearRepository.GetCurrentSchoolYearAsync(); // Repozytorium powinno zwracać tylko aktywny bieżący rok

            _cache.Set(CurrentSchoolYearCacheKey, currentSchoolYearFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Bieżący rok szkolny (lub jego brak) dodany do cache.");

            return currentSchoolYearFromDb;
        }

        /// <inheritdoc />
        public async Task<bool> SetCurrentSchoolYearAsync(string schoolYearId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_set_current_sy";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearSetAsCurrent,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityId = schoolYearId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie ustawiania roku szkolnego ID: {SchoolYearId} jako bieżący, przez {User}", schoolYearId, currentUserUpn);

            string? oldCurrentYearIdToInvalidate = null;
            bool wasAnythingChanged = false;

            try
            {
                var newCurrentSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);
                if (newCurrentSchoolYear == null || !newCurrentSchoolYear.IsActive)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearId}' nie istnieje lub jest nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można ustawić roku szkolnego ID {SchoolYearId} jako bieżący - nie istnieje lub nieaktywny.", schoolYearId);
                    return false;
                }
                operation.TargetEntityName = newCurrentSchoolYear.Name;

                if (newCurrentSchoolYear.IsCurrent)
                {
                    operation.MarkAsCompleted($"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) był już bieżący. Brak zmian.");
                    _logger.LogInformation("Rok szkolny ID {SchoolYearId} był już ustawiony jako bieżący.", schoolYearId);
                    InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: true, invalidateAll: false); // Odśwież, na wszelki wypadek
                    return true; // Stan docelowy osiągnięty
                }

                var currentlyActiveYears = await _schoolYearRepository.FindAsync(sy => sy.IsCurrent && sy.Id != schoolYearId && sy.IsActive);
                foreach (var oldCurrentYear in currentlyActiveYears)
                {
                    oldCurrentYearIdToInvalidate = oldCurrentYear.Id;
                    oldCurrentYear.IsCurrent = false;
                    oldCurrentYear.MarkAsModified(currentUserUpn);
                    _schoolYearRepository.Update(oldCurrentYear);
                    wasAnythingChanged = true;
                    _logger.LogInformation("Rok szkolny {OldSchoolYearName} (ID: {OldSchoolYearId}) został odznaczony jako bieżący.", oldCurrentYear.Name, oldCurrentYear.Id);
                }

                newCurrentSchoolYear.IsCurrent = true;
                newCurrentSchoolYear.MarkAsModified(currentUserUpn);
                _schoolYearRepository.Update(newCurrentSchoolYear);
                wasAnythingChanged = true;
                _logger.LogInformation("Rok szkolny {NewSchoolYearName} (ID: {NewSchoolYearId}) został ustawiony jako bieżący.", newCurrentSchoolYear.Name, newCurrentSchoolYear.Id);

                operation.MarkAsCompleted($"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) ustawiony jako bieżący. Poprzednie odznaczone (jeśli były).");

                // Inwalidacja cache
                InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: true, invalidateAll: true); // Nowy bieżący i lista wszystkich
                if (oldCurrentYearIdToInvalidate != null)
                {
                    InvalidateCache(schoolYearId: oldCurrentYearIdToInvalidate, wasOrIsCurrent: false, invalidateAll: false); // Stary już nie jest bieżący (ale lista wszystkich i tak jest unieważniona)
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas ustawiania roku szkolnego ID {SchoolYearId} jako bieżący. Wiadomość: {ErrorMessage}", schoolYearId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearCreated,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityName = name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia roku szkolnego: {Name} przez {User}", name, currentUserUpn);

                if (string.IsNullOrWhiteSpace(name))
                {
                    operation.MarkAsFailed("Nazwa roku szkolnego nie może być pusta.");
                    _logger.LogError("Nie można utworzyć roku szkolnego: Nazwa jest pusta.");
                    return null;
                }
                if (startDate.Date >= endDate.Date)
                {
                    operation.MarkAsFailed("Data rozpoczęcia musi być wcześniejsza niż data zakończenia.");
                    _logger.LogError("Nie można utworzyć roku szkolnego: Data rozpoczęcia ({StartDate}) nie jest wcześniejsza niż data zakończenia ({EndDate}).", startDate, endDate);
                    return null;
                }

                var existing = await _schoolYearRepository.GetSchoolYearByNameAsync(name);
                if (existing != null && existing.IsActive) // Sprawdzamy tylko aktywne
                {
                    operation.MarkAsFailed($"Aktywny rok szkolny o nazwie '{name}' już istnieje.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Aktywny rok szkolny o nazwie '{Name}' już istnieje.", name);
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
                    IsCurrent = false, // Nowo tworzony rok nie jest domyślnie bieżący
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _schoolYearRepository.AddAsync(newSchoolYear);

                operation.TargetEntityId = newSchoolYear.Id;
                operation.MarkAsCompleted($"Rok szkolny '{newSchoolYear.Name}' (ID: {newSchoolYear.Id}) przygotowany do utworzenia.");
                _logger.LogInformation("Rok szkolny '{Name}' pomyślnie przygotowany do zapisu. ID: {SchoolYearId}", name, newSchoolYear.Id);

                InvalidateCache(schoolYearId: newSchoolYear.Id, wasOrIsCurrent: newSchoolYear.IsCurrent, invalidateAll: true);
                return newSchoolYear;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia roku szkolnego {Name}. Wiadomość: {ErrorMessage}", name, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
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

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearUpdated,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityId = schoolYearToUpdate.Id,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji roku szkolnego ID: {SchoolYearId} przez {User}", schoolYearToUpdate.Id, currentUserUpn);

            bool wasCurrentBeforeUpdate = false;

            try
            {
                var existingSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearToUpdate.Id);
                if (existingSchoolYear == null)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearToUpdate.Id}' nie istnieje.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można zaktualizować roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSchoolYear.Name;
                wasCurrentBeforeUpdate = existingSchoolYear.IsCurrent;

                if (string.IsNullOrWhiteSpace(schoolYearToUpdate.Name) || schoolYearToUpdate.StartDate.Date >= schoolYearToUpdate.EndDate.Date)
                {
                    operation.MarkAsFailed("Niepoprawne dane wejściowe (nazwa, daty). Nazwa nie może być pusta, a data rozpoczęcia musi być wcześniejsza niż data zakończenia.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogError("Błąd walidacji przy aktualizacji roku szkolnego: {SchoolYearId}. Nazwa: '{Name}', Start: {StartDate}, Koniec: {EndDate}",
                        schoolYearToUpdate.Id, schoolYearToUpdate.Name, schoolYearToUpdate.StartDate, schoolYearToUpdate.EndDate);
                    return false;
                }

                if (existingSchoolYear.Name != schoolYearToUpdate.Name)
                {
                    var conflicting = await _schoolYearRepository.GetSchoolYearByNameAsync(schoolYearToUpdate.Name);
                    if (conflicting != null && conflicting.Id != existingSchoolYear.Id && conflicting.IsActive)
                    {
                        operation.MarkAsFailed($"Aktywny rok szkolny o nazwie '{schoolYearToUpdate.Name}' już istnieje.");
                        await SaveOperationHistoryAsync(operation);
                        _logger.LogWarning("Rok szkolny o nazwie '{Name}' już istnieje (inny ID) i jest aktywny.", schoolYearToUpdate.Name);
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
                existingSchoolYear.IsActive = schoolYearToUpdate.IsActive;

                if (existingSchoolYear.IsCurrent != schoolYearToUpdate.IsCurrent)
                {
                    _logger.LogWarning("Zmiana flagi IsCurrent dla roku szkolnego ID {SchoolYearId} nie jest dozwolona przez UpdateSchoolYearAsync. Użyj SetCurrentSchoolYearAsync.", existingSchoolYear.Id);
                    // Nie zmieniamy IsCurrent, aby wymusić użycie dedykowanej metody
                }

                existingSchoolYear.MarkAsModified(currentUserUpn);
                _schoolYearRepository.Update(existingSchoolYear);

                operation.TargetEntityName = existingSchoolYear.Name;
                operation.MarkAsCompleted($"Rok szkolny ID: {existingSchoolYear.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Rok szkolny ID: {SchoolYearId} pomyślnie przygotowany do aktualizacji.", existingSchoolYear.Id);

                InvalidateCache(schoolYearId: existingSchoolYear.Id, wasOrIsCurrent: wasCurrentBeforeUpdate || existingSchoolYear.IsCurrent, invalidateAll: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji roku szkolnego ID {SchoolYearId}. Wiadomość: {ErrorMessage}", schoolYearToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSchoolYearAsync(string schoolYearId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolYearDeleted,
                TargetEntityType = nameof(SchoolYear),
                TargetEntityId = schoolYearId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie usuwania (dezaktywacji) roku szkolnego ID: {SchoolYearId} przez {User}", schoolYearId, currentUserUpn);

            SchoolYear? schoolYear = null;
            try
            {
                schoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);
                if (schoolYear == null)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearId}' nie istnieje.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearId);
                    return false;
                }
                operation.TargetEntityName = schoolYear.Name;

                if (!schoolYear.IsActive)
                {
                    operation.MarkAsCompleted($"Rok szkolny '{schoolYear.Name}' był już nieaktywny.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogInformation("Rok szkolny ID {SchoolYearId} był już nieaktywny.", schoolYearId);
                    InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: schoolYear.IsCurrent, invalidateAll: true);
                    return true;
                }

                if (schoolYear.IsCurrent)
                {
                    operation.MarkAsFailed("Nie można usunąć (dezaktywować) bieżącego roku szkolnego. Najpierw ustaw inny rok jako bieżący.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można usunąć/dezaktywować bieżącego roku szkolnego ID {SchoolYearId}.", schoolYearId);
                    return false;
                }

                var teamsUsingYear = await _teamRepository.FindAsync(t => t.SchoolYearId == schoolYearId && t.IsActive && t.Status == TeamStatus.Active);
                if (teamsUsingYear.Any())
                {
                    operation.MarkAsFailed($"Nie można usunąć roku szkolnego '{schoolYear.Name}', ponieważ jest nadal używany przez {teamsUsingYear.Count()} aktywnych zespołów.");
                    await SaveOperationHistoryAsync(operation);
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - jest używany przez {Count} aktywnych zespołów.", schoolYearId, teamsUsingYear.Count());
                    return false;
                }

                schoolYear.MarkAsDeleted(currentUserUpn);
                _schoolYearRepository.Update(schoolYear);

                operation.MarkAsCompleted($"Rok szkolny '{schoolYear.Name}' (ID: {schoolYearId}) oznaczony jako usunięty.");
                _logger.LogInformation("Rok szkolny ID {SchoolYearId} pomyślnie oznaczony jako usunięty.", schoolYearId);

                InvalidateCache(schoolYearId: schoolYearId, wasOrIsCurrent: false, invalidateAll: true); // Usunięty rok nie jest już bieżący
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania roku szkolnego ID {SchoolYearId}. Wiadomość: {ErrorMessage}", schoolYearId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla lat szkolnych.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a lat szkolnych.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache lat szkolnych został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unieważnia cache dla lat szkolnych.
        /// Resetuje globalny token dla lat szkolnych, co unieważnia wszystkie zależne wpisy.
        /// Jawnie usuwa klucz dla listy wszystkich aktywnych lat szkolnych.
        /// Opcjonalnie usuwa klucz dla konkretnego roku szkolnego i/lub klucz dla bieżącego roku szkolnego.
        /// </summary>
        /// <param name="schoolYearId">ID roku szkolnego, którego specyficzny cache ma być usunięty (opcjonalnie).</param>
        /// <param name="wasOrIsCurrent">Czy operacja dotyczyła roku, który był lub stał się bieżący (opcjonalnie, domyślnie false).</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie klucze związane z latami szkolnymi (opcjonalnie, domyślnie false).</param>
        private void InvalidateCache(string? schoolYearId = null, bool wasOrIsCurrent = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u lat szkolnych. schoolYearId: {SchoolYearId}, wasOrIsCurrent: {WasOrIsCurrent}, invalidateAll: {InvalidateAll}",
               schoolYearId, wasOrIsCurrent, invalidateAll);

            // 1. Zresetuj CancellationTokenSource
            var oldTokenSource = Interlocked.Exchange(ref _schoolYearsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla lat szkolnych został zresetowany.");

            // 2. Zawsze usuwaj klucz dla listy wszystkich aktywnych lat
            _cache.Remove(AllSchoolYearsCacheKey);
            _logger.LogDebug("Usunięto z cache klucz: {CacheKey}", AllSchoolYearsCacheKey);

            // 3. Jeśli invalidateAll jest true, usuń dodatkowo klucz dla bieżącego roku
            if (invalidateAll)
            {
                _cache.Remove(CurrentSchoolYearCacheKey);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey} (z powodu invalidateAll=true)", CurrentSchoolYearCacheKey);
            }
            // 4. Jeśli operacja dotyczyła roku, który był lub stał się bieżący, usuń klucz dla bieżącego roku
            else if (wasOrIsCurrent)
            {
                _cache.Remove(CurrentSchoolYearCacheKey);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey} (z powodu wasOrIsCurrent=true)", CurrentSchoolYearCacheKey);
            }

            // 5. Jeśli podano schoolYearId, usuń specyficzny klucz dla tego ID
            if (!string.IsNullOrWhiteSpace(schoolYearId))
            {
                _cache.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId);
                _logger.LogDebug("Usunięto z cache klucz: {CacheKey}{Id}", SchoolYearByIdCacheKeyPrefix, schoolYearId);
            }
        }

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            if (operation.StartedAt == default(DateTime) &&
                (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending || operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed))
            {
                if (operation.StartedAt == default(DateTime)) operation.StartedAt = DateTime.UtcNow;
                if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.PartialSuccess)
                {
                    if (!operation.CompletedAt.HasValue) operation.CompletedAt = DateTime.UtcNow;
                    if (!operation.Duration.HasValue && operation.CompletedAt.HasValue) operation.Duration = operation.CompletedAt.Value - operation.StartedAt;
                }
            }

            await _operationHistoryRepository.AddAsync(operation);
            _logger.LogDebug("Zapisano nowy wpis historii operacji ID: {OperationId} dla roku szkolnego.", operation.Id);
        }
    }
}