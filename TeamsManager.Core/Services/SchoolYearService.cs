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
        private readonly ITeamRepository _teamRepository;
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
        /// <param name="schoolYearRepository">Repozytorium lat szkolnych.</param>
        /// <param name="operationHistoryRepository">Repozytorium historii operacji.</param>
        /// <param name="currentUserService">Serwis informacji o bieżącym użytkowniku.</param>
        /// <param name="logger">Rejestrator zdarzeń.</param>
        /// <param name="teamRepository">Repozytorium zespołów.</param>
        /// <param name="memoryCache">Pamięć podręczna.</param>
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

            if (schoolYearFromDb != null)
            {
                _cache.Set(cacheKey, schoolYearFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Rok szkolny ID: {SchoolYearId} dodany do cache.", schoolYearId);
            }
            else
            {
                _cache.Remove(cacheKey);
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
                _logger.LogDebug("Bieżący rok szkolny znaleziony w cache.");
                return cachedCurrentSchoolYear; // Może być null, jeśli nie ma bieżącego roku
            }

            _logger.LogDebug("Bieżący rok szkolny nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var currentSchoolYearFromDb = await _schoolYearRepository.GetCurrentSchoolYearAsync();

            _cache.Set(CurrentSchoolYearCacheKey, currentSchoolYearFromDb, GetDefaultCacheEntryOptions()); // Cache'ujemy również null, aby uniknąć wielokrotnych zapytań do DB, jeśli nie ma bieżącego
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

            try
            {
                var newCurrentSchoolYear = await _schoolYearRepository.GetByIdAsync(schoolYearId);
                if (newCurrentSchoolYear == null || !newCurrentSchoolYear.IsActive)
                {
                    operation.MarkAsFailed($"Rok szkolny o ID '{schoolYearId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można ustawić roku szkolnego ID {SchoolYearId} jako bieżący - nie istnieje lub nieaktywny.", schoolYearId);
                    await SaveOperationHistoryAsync(operation);
                    return false;
                }
                operation.TargetEntityName = newCurrentSchoolYear.Name;

                var currentlyActiveYears = await _schoolYearRepository.FindAsync(sy => sy.IsCurrent && sy.Id != schoolYearId && sy.IsActive);
                bool changesMade = false;

                foreach (var oldCurrentYear in currentlyActiveYears)
                {
                    oldCurrentYearIdToInvalidate = oldCurrentYear.Id; // Zapamiętaj ID do inwalidacji
                    oldCurrentYear.IsCurrent = false;
                    oldCurrentYear.MarkAsModified(currentUserUpn);
                    _schoolYearRepository.Update(oldCurrentYear);
                    changesMade = true;
                    _logger.LogInformation("Rok szkolny {OldSchoolYearName} (ID: {OldSchoolYearId}) został odznaczony jako bieżący.", oldCurrentYear.Name, oldCurrentYear.Id);
                }

                if (!newCurrentSchoolYear.IsCurrent)
                {
                    newCurrentSchoolYear.IsCurrent = true;
                    newCurrentSchoolYear.MarkAsModified(currentUserUpn);
                    _schoolYearRepository.Update(newCurrentSchoolYear);
                    changesMade = true;
                    _logger.LogInformation("Rok szkolny {NewSchoolYearName} (ID: {NewSchoolYearId}) został ustawiony jako bieżący.", newCurrentSchoolYear.Name, newCurrentSchoolYear.Id);
                }
                else
                {
                    _logger.LogInformation("Rok szkolny {NewSchoolYearName} (ID: {NewSchoolYearId}) był już ustawiony jako bieżący.", newCurrentSchoolYear.Name, newCurrentSchoolYear.Id);
                }

                if (changesMade)
                {
                    operation.MarkAsCompleted($"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) ustawiony jako bieżący. Poprzednie odznaczone.");
                    InvalidateCache(schoolYearId, true); // Inwaliduj nowy bieżący
                    if (oldCurrentYearIdToInvalidate != null)
                    {
                        InvalidateCache(oldCurrentYearIdToInvalidate, false); // Inwaliduj stary bieżący (już nie jest bieżący)
                    }
                }
                else
                {
                    operation.MarkAsCompleted($"Rok szkolny '{newCurrentSchoolYear.Name}' (ID: {schoolYearId}) był już bieżący. Brak zmian.");
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
                if (existing != null && existing.IsActive)
                {
                    operation.MarkAsFailed($"Aktywny rok szkolny o nazwie '{name}' już istnieje.");
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
                    IsCurrent = false,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _schoolYearRepository.AddAsync(newSchoolYear);

                operation.TargetEntityId = newSchoolYear.Id;
                operation.MarkAsCompleted($"Rok szkolny '{newSchoolYear.Name}' (ID: {newSchoolYear.Id}) przygotowany do utworzenia.");
                _logger.LogInformation("Rok szkolny '{Name}' pomyślnie przygotowany do zapisu. ID: {SchoolYearId}", name, newSchoolYear.Id);

                InvalidateCache(newSchoolYear.Id, newSchoolYear.IsCurrent); // Nowy rok nie jest current
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
                    _logger.LogWarning("Nie można zaktualizować roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSchoolYear.Name;
                wasCurrentBeforeUpdate = existingSchoolYear.IsCurrent;

                if (string.IsNullOrWhiteSpace(schoolYearToUpdate.Name) || schoolYearToUpdate.StartDate.Date >= schoolYearToUpdate.EndDate.Date)
                {
                    operation.MarkAsFailed("Niepoprawne dane wejściowe (nazwa, daty). Nazwa nie może być pusta, a data rozpoczęcia musi być wcześniejsza niż data zakończenia.");
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
                // Flaga IsCurrent jest zarządzana przez SetCurrentSchoolYearAsync,
                // więc jeśli schoolYearToUpdate.IsCurrent jest inne niż existingSchoolYear.IsCurrent,
                // należy użyć SetCurrentSchoolYearAsync zamiast bezpośredniego update.
                // Tutaj zakładamy, że UpdateSchoolYearAsync nie zmienia flagi IsCurrent, chyba że to zamierzone i obsłużone.
                if (existingSchoolYear.IsCurrent != schoolYearToUpdate.IsCurrent)
                {
                    // Logika SetCurrentSchoolYearAsync powinna być wywołana, jeśli chcemy zmienić status IsCurrent
                    // Dla uproszczenia, ten serwis nie pozwoli na zmianę IsCurrent przez Update.
                    // Jeśli IsCurrent ma się zmienić, operacja powinna iść przez SetCurrentSchoolYearAsync.
                    // Jeśli schoolYearToUpdate.IsCurrent jest true, a existing.IsCurrent false,
                    // to SetCurrentSchoolYearAsync(existingSchoolYear.Id) powinno być wywołane.
                    // Jeśli schoolYearToUpdate.IsCurrent jest false, a existing.IsCurrent true,
                    // to oznacza próbę odznaczenia bieżącego roku, co jest dozwolone, ale wymaga uwagi.
                    _logger.LogWarning("Aktualizacja flagi IsCurrent dla roku szkolnego ID {SchoolYearId} powinna być wykonana przez metodę SetCurrentSchoolYearAsync.", existingSchoolYear.Id);
                    // Można rzucić wyjątek lub zignorować zmianę IsCurrent w tej metodzie
                    // Na razie zignorujmy:
                    // existingSchoolYear.IsCurrent = existingSchoolYear.IsCurrent; // Pozostaw bez zmian
                }


                existingSchoolYear.MarkAsModified(currentUserUpn);
                _schoolYearRepository.Update(existingSchoolYear);

                operation.TargetEntityName = existingSchoolYear.Name;
                operation.MarkAsCompleted($"Rok szkolny ID: {existingSchoolYear.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Rok szkolny ID: {SchoolYearId} pomyślnie przygotowany do aktualizacji.", existingSchoolYear.Id);

                // Jeśli IsCurrent się zmieniło (lub potencjalnie mogło się zmienić) lub IsActive
                InvalidateCache(existingSchoolYear.Id, wasCurrentBeforeUpdate || existingSchoolYear.IsCurrent);
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
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - nie istnieje.", schoolYearId);
                    return false;
                }
                operation.TargetEntityName = schoolYear.Name;

                if (!schoolYear.IsActive)
                {
                    operation.MarkAsCompleted($"Rok szkolny '{schoolYear.Name}' był już nieaktywny.");
                    _logger.LogInformation("Rok szkolny ID {SchoolYearId} był już nieaktywny.", schoolYearId);
                    InvalidateCache(schoolYearId, schoolYear.IsCurrent);
                    return true;
                }
                if (schoolYear.IsCurrent)
                {
                    operation.MarkAsFailed("Nie można usunąć (dezaktywować) bieżącego roku szkolnego. Najpierw ustaw inny rok jako bieżący.");
                    _logger.LogWarning("Nie można usunąć/dezaktywować bieżącego roku szkolnego ID {SchoolYearId}.", schoolYearId);
                    return false;
                }

                var teamsUsingYear = await _teamRepository.FindAsync(t => t.SchoolYearId == schoolYearId && t.IsActive && t.Status == TeamStatus.Active);
                if (teamsUsingYear.Any())
                {
                    operation.MarkAsFailed($"Nie można usunąć roku szkolnego '{schoolYear.Name}', ponieważ jest nadal używany przez {teamsUsingYear.Count()} aktywnych zespołów.");
                    _logger.LogWarning("Nie można usunąć roku szkolnego ID {SchoolYearId} - jest używany przez {Count} aktywnych zespołów.", schoolYearId, teamsUsingYear.Count());
                    return false;
                }

                schoolYear.MarkAsDeleted(currentUserUpn);
                _schoolYearRepository.Update(schoolYear);

                operation.MarkAsCompleted($"Rok szkolny '{schoolYear.Name}' (ID: {schoolYearId}) oznaczony jako usunięty.");
                _logger.LogInformation("Rok szkolny ID {SchoolYearId} pomyślnie oznaczony jako usunięty.", schoolYearId);

                InvalidateCache(schoolYearId, schoolYear.IsCurrent); // Rok nieaktywny nie jest bieżący
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
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a lat szkolnych.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache lat szkolnych został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        // Prywatna metoda do unieważniania cache.
        private void InvalidateCache(string? schoolYearId = null, bool wasOrIsCurrent = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u lat szkolnych. schoolYearId: {SchoolYearId}, wasOrIsCurrent: {WasOrIsCurrent}, invalidateAll: {InvalidateAll}",
               schoolYearId, wasOrIsCurrent, invalidateAll);

            // Unieważnienie globalnego tokenu jest najprostszym sposobem
            // na zapewnienie spójności dla GetAll i GetCurrent,
            // szczególnie gdy SetCurrentSchoolYear zmienia wiele rzeczy.
            var oldTokenSource = Interlocked.Exchange(ref _schoolYearsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla lat szkolnych został zresetowany.");

            // Dodatkowo można usunąć specyficzny klucz ID, jeśli jest znany
            if (!string.IsNullOrWhiteSpace(schoolYearId))
            {
                _cache.Remove(SchoolYearByIdCacheKeyPrefix + schoolYearId);
                _logger.LogDebug("Usunięto z cache rok szkolny o ID: {SchoolYearId}", schoolYearId);
            }

            // Jeśli operacja mogła wpłynąć na to, który rok jest bieżący LUB jeśli była to zmiana flagi IsCurrent
            // LUB jeśli wymuszamy pełną inwalidację.
            if (wasOrIsCurrent || invalidateAll)
            {
                _cache.Remove(CurrentSchoolYearCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla bieżącego roku szkolnego.");
            }

            // Klucz dla wszystkich aktywnych lat również powinien być unieważniony przy każdej modyfikacji
            // danych lat szkolnych, ponieważ zmiana IsActive lub dodanie/usunięcie wpływa na tę listę.
            // Reset tokenu powyżej powinien o to zadbać, ale dla pewności można też jawnie.
            if (invalidateAll)
            { // Lub zawsze, jeśli token nie jest wystarczająco granularny dla tego przypadku
                _cache.Remove(AllSchoolYearsCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych lat szkolnych.");
            }
        }

        // Metoda pomocnicza do zapisu OperationHistory
        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString();
            if (string.IsNullOrEmpty(operation.CreatedBy))
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save";

            var existingLog = await _operationHistoryRepository.GetByIdAsync(operation.Id);
            if (existingLog == null)
            {
                await _operationHistoryRepository.AddAsync(operation);
            }
            else
            {
                existingLog.Status = operation.Status;
                existingLog.CompletedAt = operation.CompletedAt;
                existingLog.Duration = operation.Duration;
                existingLog.ErrorMessage = operation.ErrorMessage;
                existingLog.ErrorStackTrace = operation.ErrorStackTrace;
                existingLog.OperationDetails = operation.OperationDetails;
                existingLog.TargetEntityName = operation.TargetEntityName;
                existingLog.TargetEntityId = operation.TargetEntityId;
                existingLog.ProcessedItems = operation.ProcessedItems;
                existingLog.FailedItems = operation.FailedItems;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
        }
    }
}