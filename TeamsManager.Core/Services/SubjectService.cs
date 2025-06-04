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
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za logikę biznesową przedmiotów.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class SubjectService : ISubjectService
    {
        private readonly ISubjectRepository _subjectRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly IGenericRepository<UserSubject> _userSubjectRepository; // Potrzebne do usuwania przypisań przy usuwaniu przedmiotu
        private readonly IUserRepository _userRepository; // Niewykorzystywane bezpośrednio, ale może być potrzebne w przyszłości
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly IPowerShellCacheService _powerShellCacheService;
        private readonly ILogger<SubjectService> _logger;
        private readonly IMemoryCache _cache;

        // Definicje kluczy cache
        private const string AllSubjectsCacheKey = "Subjects_AllActive";
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30); // Przedmioty zmieniają się rzadziej niż użytkownicy
        private readonly TimeSpan _shortCacheDuration = TimeSpan.FromMinutes(5); // Dla list nauczycieli, które mogą się zmieniać częściej

        // Metryki cache
        private long _cacheHits = 0;
        private long _cacheMisses = 0;

        /// <summary>
        /// Konstruktor serwisu przedmiotów.
        /// </summary>
        public SubjectService(
            ISubjectRepository subjectRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IGenericRepository<UserSubject> userSubjectRepository,
            IUserRepository userRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            IPowerShellCacheService powerShellCacheService,
            ILogger<SubjectService> logger,
            IMemoryCache memoryCache)
        {
            _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userSubjectRepository = userSubjectRepository ?? throw new ArgumentNullException(nameof(userSubjectRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<Subject?> GetSubjectByIdAsync(string subjectId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie przedmiotu o ID: {SubjectId}. Wymuszenie odświeżenia: {ForceRefresh}", subjectId, forceRefresh);
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                _logger.LogWarning("Próba pobrania przedmiotu z pustym ID.");
                return null;
            }

            string cacheKey = SubjectByIdCacheKeyPrefix + subjectId;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out Subject? cachedSubject))
            {
                Interlocked.Increment(ref _cacheHits);
                _logger.LogDebug("Cache HIT dla przedmiotu ID: {SubjectId}. Metryki: {Hits}/{Misses}", 
                    subjectId, _cacheHits, _cacheMisses);
                return cachedSubject;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("Cache MISS dla przedmiotu ID: {SubjectId}. Metryki: {Hits}/{Misses}", 
                subjectId, _cacheHits, _cacheMisses);
            var subjectFromDb = await _subjectRepository.GetByIdWithDetailsAsync(subjectId); // Ta metoda dołącza DefaultSchoolType

            if (subjectFromDb != null) // Repozytorium zwraca null jeśli nieaktywny
            {
                _powerShellCacheService.Set(cacheKey, subjectFromDb, _defaultCacheDuration);
                _logger.LogDebug("Przedmiot ID: {SubjectId} dodany do cache.", subjectId);
            }
            else
            {
                _powerShellCacheService.Remove(cacheKey);
                _logger.LogDebug("Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny. Usunięto z cache.", subjectId);
            }
            return subjectFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<Subject>> GetAllActiveSubjectsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych przedmiotów. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _powerShellCacheService.TryGetValue(AllSubjectsCacheKey, out IEnumerable<Subject>? cachedSubjects) && cachedSubjects != null)
            {
                Interlocked.Increment(ref _cacheHits);
                _logger.LogDebug("Cache HIT dla wszystkich aktywnych przedmiotów. Metryki: {Hits}/{Misses}", 
                    _cacheHits, _cacheMisses);
                return cachedSubjects;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("Cache MISS dla wszystkich aktywnych przedmiotów. Metryki: {Hits}/{Misses}", 
                _cacheHits, _cacheMisses);
            var subjectsFromDb = await _subjectRepository.GetAllActiveWithDetailsAsync(); // Ta metoda dołącza DefaultSchoolType i filtruje po IsActive

            _powerShellCacheService.Set(AllSubjectsCacheKey, subjectsFromDb, _defaultCacheDuration);
            _logger.LogDebug("Wszystkie aktywne przedmioty dodane do cache.");
            return subjectsFromDb;
        }

        /// <inheritdoc />
        public async Task<Subject?> CreateSubjectAsync(
            string name,
            string? code = null,
            string? description = null,
            int? hours = null,
            string? defaultSchoolTypeId = null,
            string? category = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie tworzenia przedmiotu: '{SubjectName}'", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogError("Nie można utworzyć przedmiotu: Nazwa jest pusta.");
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nie można utworzyć przedmiotu: nazwa nie może być pusta",
                    "error"
                );
                return null;
            }

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SubjectCreated,
                nameof(Subject),
                targetEntityName: name
            );

            try
            {
                if (!string.IsNullOrWhiteSpace(code))
                {
                    var existingByCode = await _subjectRepository.GetByCodeAsync(code); // GetByCodeAsync zwraca tylko aktywne
                    if (existingByCode != null)
                    {
                        _logger.LogError("Nie można utworzyć przedmiotu: Aktywny przedmiot o kodzie {SubjectCode} już istnieje.", code);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Aktywny przedmiot o kodzie '{code}' już istnieje."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            $"Nie można utworzyć przedmiotu: kod '{code}' już istnieje",
                            "error"
                        );
                        return null;
                    }
                }

                SchoolType? defaultSchoolType = null;
                if (!string.IsNullOrEmpty(defaultSchoolTypeId))
                {
                    defaultSchoolType = await _schoolTypeRepository.GetByIdAsync(defaultSchoolTypeId);
                    if (defaultSchoolType == null || !defaultSchoolType.IsActive)
                    {
                        _logger.LogWarning("Podany domyślny typ szkoły (ID: {DefaultSchoolTypeId}) dla przedmiotu '{SubjectName}' nie istnieje lub jest nieaktywny. Pole zostanie zignorowane.", defaultSchoolTypeId, name);
                        defaultSchoolTypeId = null; // Zresetuj ID, jeśli typ szkoły nie jest prawidłowy
                        defaultSchoolType = null;
                    }
                }

                var newSubject = new Subject
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Code = code,
                    Description = description,
                    Hours = hours,
                    DefaultSchoolTypeId = defaultSchoolTypeId,
                    DefaultSchoolType = defaultSchoolType,
                    Category = category,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _subjectRepository.AddAsync(newSubject);
                // SaveChangesAsync() na wyższym poziomie

                _logger.LogInformation("Przedmiot '{SubjectName}' pomyślnie przygotowany do zapisu. ID: {SubjectId}", name, newSubject.Id);

                InvalidateCache(subjectId: newSubject.Id, invalidateAll: false);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przedmiot '{newSubject.Name}' utworzony pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Przedmiot '{newSubject.Name}' został utworzony",
                    "success"
                );
                return newSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia przedmiotu '{SubjectName}'. Wiadomość: {ErrorMessage}", name, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Nie udało się utworzyć przedmiotu: {ex.Message}",
                    "error"
                );
                return null;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSubjectAsync(Subject subjectToUpdate)
        {
            if (subjectToUpdate == null || string.IsNullOrEmpty(subjectToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji przedmiotu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(subjectToUpdate), "Obiekt przedmiotu lub jego ID nie może być null/pusty.");
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie aktualizacji przedmiotu ID: {SubjectId}", subjectToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SubjectUpdated,
                nameof(Subject),
                targetEntityId: subjectToUpdate.Id,
                targetEntityName: subjectToUpdate.Name
            );

            try
            {
                var existingSubject = await _subjectRepository.GetByIdWithDetailsAsync(subjectToUpdate.Id); // Pobierze tylko aktywny
                if (existingSubject == null)
                {
                    _logger.LogWarning("Nie można zaktualizować przedmiotu ID {SubjectId} - nie istnieje lub jest nieaktywny.", subjectToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przedmiot o ID '{subjectToUpdate.Id}' nie istnieje lub jest nieaktywny."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować przedmiotu: nie istnieje w systemie",
                        "error"
                    );
                    return false;
                }

                if (string.IsNullOrWhiteSpace(subjectToUpdate.Name))
                {
                    _logger.LogError("Błąd walidacji przy aktualizacji przedmiotu {SubjectId}: Nazwa pusta.", subjectToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa przedmiotu nie może być pusta."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować przedmiotu: nazwa nie może być pusta",
                        "error"
                    );
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(subjectToUpdate.Code) && existingSubject.Code != subjectToUpdate.Code)
                {
                    // Sprawdzamy, czy inny *aktywny* przedmiot ma już taki kod
                    var conflicting = await _subjectRepository.GetByCodeAsync(subjectToUpdate.Code);
                    if (conflicting != null && conflicting.Id != existingSubject.Id)
                    {
                        _logger.LogError("Nie można zaktualizować przedmiotu: Aktywny przedmiot o kodzie '{SubjectCode}' już istnieje.", subjectToUpdate.Code);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Aktywny przedmiot o kodzie '{subjectToUpdate.Code}' już istnieje."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            $"Nie można zaktualizować przedmiotu: kod '{subjectToUpdate.Code}' już istnieje",
                            "error"
                        );
                        return false;
                    }
                }

                // Obsługa DefaultSchoolType
                if (!string.IsNullOrEmpty(subjectToUpdate.DefaultSchoolTypeId))
                {
                    var schoolType = await _schoolTypeRepository.GetByIdAsync(subjectToUpdate.DefaultSchoolTypeId);
                    if (schoolType != null && schoolType.IsActive)
                    {
                        existingSubject.DefaultSchoolTypeId = schoolType.Id;
                        existingSubject.DefaultSchoolType = schoolType;
                    }
                    else
                    {
                        _logger.LogWarning("Podany domyślny typ szkoły (ID: {DefaultSchoolTypeId}) dla przedmiotu '{SubjectName}' nie istnieje lub jest nieaktywny. Powiązanie z typem szkoły nie zostanie zmienione, pozostaje: {OldSchoolTypeId}",
                            subjectToUpdate.DefaultSchoolTypeId, subjectToUpdate.Name, existingSubject.DefaultSchoolTypeId);
                    }
                }
                else
                {
                    existingSubject.DefaultSchoolTypeId = null;
                    existingSubject.DefaultSchoolType = null;
                }

                existingSubject.Name = subjectToUpdate.Name;
                existingSubject.Code = subjectToUpdate.Code;
                existingSubject.Description = subjectToUpdate.Description;
                existingSubject.Hours = subjectToUpdate.Hours;
                existingSubject.Category = subjectToUpdate.Category;
                existingSubject.MarkAsModified(currentUserUpn);

                _subjectRepository.Update(existingSubject);

                InvalidateCache(subjectId: existingSubject.Id, invalidateAll: false);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przedmiot '{existingSubject.Name}' zaktualizowany pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Przedmiot '{existingSubject.Name}' został zaktualizowany",
                    "success"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectToUpdate.Id, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas aktualizacji przedmiotu: {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSubjectAsync(string subjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Usuwanie (dezaktywacja) przedmiotu ID: {SubjectId}", subjectId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SubjectDeleted,
                nameof(Subject),
                targetEntityId: subjectId
            );

            try
            {
                var subject = await _subjectRepository.GetByIdIncludingInactiveAsync(subjectId);
                if (subject == null)
                {
                    _logger.LogWarning("Nie można usunąć przedmiotu ID {SubjectId} - nie istnieje.", subjectId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przedmiot o ID '{subjectId}' nie istnieje."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można usunąć przedmiotu: nie istnieje w systemie",
                        "error"
                    );
                    return false;
                }

                if (!subject.IsActive)
                {
                    _logger.LogInformation("Przedmiot ID {SubjectId} był już nieaktywny.", subjectId);
                    InvalidateCache(subjectId: subjectId, invalidateTeachersList: true, invalidateAll: false);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Przedmiot '{subject.Name}' był już nieaktywny."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Przedmiot '{subject.Name}' był już nieaktywny",
                        "info"
                    );
                    return true;
                }

                // Przed dezaktywacją przedmiotu, zdezaktywuj wszystkie aktywne przypisania nauczycieli (UserSubject) do tego przedmiotu.
                var assignments = await _userSubjectRepository.FindAsync(us => us.SubjectId == subjectId && us.IsActive);
                if (assignments.Any())
                {
                    foreach (var assignment in assignments)
                    {
                        assignment.MarkAsDeleted(currentUserUpn);
                        _userSubjectRepository.Update(assignment);
                    }
                    _logger.LogInformation("Zdezaktywowano {Count} przypisań nauczycieli do przedmiotu {SubjectId} przed usunięciem przedmiotu.", assignments.Count(), subjectId);
                }

                subject.MarkAsDeleted(currentUserUpn);
                _subjectRepository.Update(subject);

                InvalidateCache(subjectId: subjectId, invalidateTeachersList: true, invalidateAll: false);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przedmiot '{subject.Name}' oznaczony jako usunięty wraz z przypisaniami."
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Przedmiot '{subject.Name}' został usunięty",
                    "success"
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectId, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas usuwania przedmiotu: {ex.Message}",
                    "error"
                );
                return false;
            }
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<User>> GetTeachersForSubjectAsync(string subjectId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie nauczycieli dla przedmiotu ID: {SubjectId}. Wymuszenie odświeżenia: {ForceRefresh}", subjectId, forceRefresh);
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                _logger.LogWarning("Próba pobrania nauczycieli dla pustego ID przedmiotu.");
                return Enumerable.Empty<User>();
            }

            // ZMIANA: Najpierw sprawdź czy przedmiot istnieje używając GetByIdWithDetailsAsync
            var subject = await _subjectRepository.GetByIdWithDetailsAsync(subjectId);
            if (subject == null)
            {
                _logger.LogWarning("Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny.", subjectId);
                return Enumerable.Empty<User>();
            }

            string cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            if (!forceRefresh && _powerShellCacheService.TryGetValue(cacheKey, out IEnumerable<User>? cachedTeachers) && cachedTeachers != null)
            {
                Interlocked.Increment(ref _cacheHits);
                _logger.LogDebug("Cache HIT dla nauczycieli przedmiotu ID: {SubjectId}. Metryki: {Hits}/{Misses}", 
                    subjectId, _cacheHits, _cacheMisses);
                return cachedTeachers;
            }

            Interlocked.Increment(ref _cacheMisses);
            _logger.LogDebug("Cache MISS dla nauczycieli przedmiotu ID: {SubjectId}. Metryki: {Hits}/{Misses}", 
                subjectId, _cacheHits, _cacheMisses);
            var teachersFromDb = await _subjectRepository.GetTeachersAsync(subjectId);

            _powerShellCacheService.Set(cacheKey, teachersFromDb, _shortCacheDuration);
            _logger.LogDebug("Nauczyciele dla przedmiotu ID: {SubjectId} dodani do cache.", subjectId);

            return teachersFromDb;
        }

        /// <inheritdoc />
        public Task InvalidateTeachersCacheForSubjectAsync(string subjectId)
        {
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                _logger.LogWarning("Próba unieważnienia cache nauczycieli dla pustego ID przedmiotu.");
                return Task.CompletedTask;
            }

            _logger.LogInformation("Unieważnianie cache nauczycieli dla przedmiotu ID: {SubjectId}", subjectId);
            _powerShellCacheService.InvalidateTeachersForSubject(subjectId);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla przedmiotów.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a przedmiotów.");
            _powerShellCacheService.InvalidateAllCache();
            _logger.LogInformation("Cache przedmiotów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unieważnia cache dla przedmiotów w sposób granularny.
        /// Deleguje inwalidację do PowerShellCacheService.
        /// </summary>
        /// <param name="subjectId">ID konkretnego przedmiotu do usunięcia z cache (opcjonalnie).</param>
        /// <param name="invalidateTeachersList">Czy unieważnić cache listy nauczycieli dla konkretnego przedmiotu (opcjonalnie).</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie klucze związane z przedmiotami (opcjonalnie, domyślnie false).</param>
        private void InvalidateCache(string? subjectId = null, bool invalidateTeachersList = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Granularna inwalidacja cache przedmiotów. subjectId: {SubjectId}, invalidateTeachersList: {InvalidateTeachers}, invalidateAll: {InvalidateAll}",
                             subjectId, invalidateTeachersList, invalidateAll);

            if (invalidateAll)
            {
                _logger.LogDebug("Globalna inwalidacja cache przedmiotów.");
                _powerShellCacheService.InvalidateAllCache();
                return;
            }

            // Zawsze inwaliduj listę wszystkich przedmiotów przy każdej zmianie
            _powerShellCacheService.InvalidateAllActiveSubjectsList();
            
            if (!string.IsNullOrWhiteSpace(subjectId))
            {
                // Pobierz przedmiot, aby znać jego kod - używamy GetByIdIncludingInactiveAsync
                // bo może być już nieaktywny (np. w DeleteSubjectAsync)
                var subject = _subjectRepository.GetByIdIncludingInactiveAsync(subjectId).Result;
                string? subjectCode = subject?.Code;
                
                _powerShellCacheService.InvalidateSubjectById(subjectId, subjectCode);
                
                if (invalidateTeachersList)
                {
                    _powerShellCacheService.InvalidateTeachersForSubject(subjectId);
                }
            }
        }

        /// <summary>
        /// Zwraca statystyki cache'a dla SubjectService.
        /// </summary>
        /// <returns>Tuple zawierający liczbę trafień, chybień i współczynnik trafień.</returns>
        public (long hits, long misses, double hitRate) GetCacheMetrics()
        {
            var total = _cacheHits + _cacheMisses;
            var hitRate = total > 0 ? (double)_cacheHits / total : 0;
            return (_cacheHits, _cacheMisses, hitRate);
        }
    }
}