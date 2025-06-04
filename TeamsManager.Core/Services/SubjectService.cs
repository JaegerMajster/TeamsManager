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
    /// Serwis odpowiedzialny za logikę biznesową przedmiotów.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class SubjectService : ISubjectService
    {
        private readonly ISubjectRepository _subjectRepository;
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly IGenericRepository<UserSubject> _userSubjectRepository; // Potrzebne do usuwania przypisań przy usuwaniu przedmiotu
        private readonly IUserRepository _userRepository; // Niewykorzystywane bezpośrednio, ale może być potrzebne w przyszłości
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SubjectService> _logger;
        private readonly IMemoryCache _cache;
        private readonly IOperationHistoryService _operationHistoryService;

        // Definicje kluczy cache
        private const string AllSubjectsCacheKey = "Subjects_AllActive";
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30); // Przedmioty zmieniają się rzadziej niż użytkownicy
        private readonly TimeSpan _shortCacheDuration = TimeSpan.FromMinutes(5); // Dla list nauczycieli, które mogą się zmieniać częściej

        // Token do zarządzania unieważnianiem wpisów cache dla przedmiotów
        private static CancellationTokenSource _subjectsCacheTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Konstruktor serwisu przedmiotów.
        /// </summary>
        public SubjectService(
            ISubjectRepository subjectRepository,
            IGenericRepository<SchoolType> schoolTypeRepository,
            IGenericRepository<UserSubject> userSubjectRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<SubjectService> logger,
            IMemoryCache memoryCache,
            IOperationHistoryService operationHistoryService)
        {
            _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userSubjectRepository = userSubjectRepository ?? throw new ArgumentNullException(nameof(userSubjectRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions(TimeSpan? duration = null)
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(duration ?? _defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_subjectsCacheTokenSource.Token));
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

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out Subject? cachedSubject))
            {
                _logger.LogDebug("Przedmiot ID: {SubjectId} znaleziony w cache.", subjectId);
                // Dociągnięcie DefaultSchoolType, jeśli nie zostało załadowane z cache, jest teraz w repozytorium GetByIdWithDetailsAsync
                return cachedSubject;
            }

            _logger.LogDebug("Przedmiot ID: {SubjectId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", subjectId);
            var subjectFromDb = await _subjectRepository.GetByIdWithDetailsAsync(subjectId); // Ta metoda dołącza DefaultSchoolType

            if (subjectFromDb != null) // Repozytorium zwraca null jeśli nieaktywny
            {
                _cache.Set(cacheKey, subjectFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Przedmiot ID: {SubjectId} dodany do cache.", subjectId);
            }
            else
            {
                _cache.Remove(cacheKey);
                _logger.LogDebug("Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny. Usunięto z cache.", subjectId);
            }
            return subjectFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<Subject>> GetAllActiveSubjectsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych przedmiotów. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllSubjectsCacheKey, out IEnumerable<Subject>? cachedSubjects) && cachedSubjects != null)
            {
                _logger.LogDebug("Wszystkie aktywne przedmioty znalezione w cache.");
                return cachedSubjects;
            }

            _logger.LogDebug("Wszystkie aktywne przedmioty nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var subjectsFromDb = await _subjectRepository.GetAllActiveWithDetailsAsync(); // Ta metoda dołącza DefaultSchoolType i filtruje po IsActive

            _cache.Set(AllSubjectsCacheKey, subjectsFromDb, GetDefaultCacheEntryOptions());
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
            _logger.LogInformation("Rozpoczynanie tworzenia przedmiotu: '{SubjectName}'", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.LogError("Nie można utworzyć przedmiotu: Nazwa jest pusta.");
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
                    CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system", // Ustawiane również przez DbContext
                    IsActive = true
                };

                await _subjectRepository.AddAsync(newSubject);
                // SaveChangesAsync() na wyższym poziomie

                _logger.LogInformation("Przedmiot '{SubjectName}' pomyślnie przygotowany do zapisu. ID: {SubjectId}", name, newSubject.Id);

                InvalidateCache(subjectId: newSubject.Id, invalidateAll: true);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przedmiot ID: {newSubject.Id} ('{newSubject.Name}') przygotowany do utworzenia."
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

            _logger.LogInformation("Rozpoczynanie aktualizacji przedmiotu ID: {SubjectId}", subjectToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SubjectUpdated,
                nameof(Subject),
                targetEntityId: subjectToUpdate.Id
            );

            try
            {
                var existingSubject = await _subjectRepository.GetByIdWithDetailsAsync(subjectToUpdate.Id); // Pobierze tylko aktywny
                if (existingSubject == null)
                {
                    // Jeśli chcemy zezwolić na aktualizację także nieaktywnych, musielibyśmy użyć GetByIdAsync
                    // i potem sprawdzić existingSubject.IsActive
                    _logger.LogWarning("Nie można zaktualizować przedmiotu ID {SubjectId} - nie istnieje lub jest nieaktywny.", subjectToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przedmiot o ID '{subjectToUpdate.Id}' nie istnieje lub jest nieaktywny."
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
                        // Nie zmieniamy DefaultSchoolTypeId ani DefaultSchoolType w existingSubject, jeśli nowy jest nieprawidłowy
                    }
                }
                else // Jeśli DefaultSchoolTypeId w danych do aktualizacji jest null/pusty, czyścimy powiązanie
                {
                    existingSubject.DefaultSchoolTypeId = null;
                    existingSubject.DefaultSchoolType = null;
                }

                existingSubject.Name = subjectToUpdate.Name;
                existingSubject.Code = subjectToUpdate.Code;
                existingSubject.Description = subjectToUpdate.Description;
                existingSubject.Hours = subjectToUpdate.Hours;
                existingSubject.Category = subjectToUpdate.Category;
                existingSubject.IsActive = subjectToUpdate.IsActive; // Pozwalamy na zmianę IsActive
                existingSubject.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system");

                _subjectRepository.Update(existingSubject);
                _logger.LogInformation("Przedmiot ID: {SubjectId} pomyślnie przygotowany do aktualizacji.", existingSubject.Id);

                // Zmiana przedmiotu (np. jego status IsActive) może wpłynąć na listy nauczycieli i wszystkie listy przedmiotów.
                InvalidateCache(subjectId: existingSubject.Id, invalidateTeachersList: true, invalidateAll: true);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przedmiot ID: {existingSubject.Id} przygotowany do aktualizacji."
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectToUpdate.Id, ex.Message);
                
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
        public async Task<bool> DeleteSubjectAsync(string subjectId)
        {
            _logger.LogInformation("Usuwanie (dezaktywacja) przedmiotu ID: {SubjectId}", subjectId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.SubjectDeleted,
                nameof(Subject),
                targetEntityId: subjectId
            );

            try
            {
                // Używamy GetByIdAsync, aby móc zdezaktywować nawet już nieaktywny rekord (jeśli logika na to pozwala,
                // ale standardowo MarkAsDeleted zadziała idempotetnie na IsActive).
                // Jednakże GetByIdWithDetailsAsync zwraca null dla nieaktywnych, więc użyjemy GetByIdAsync z repozytorium.
                var subject = await _subjectRepository.GetByIdAsync(subjectId);
                if (subject == null)
                {
                    _logger.LogWarning("Nie można usunąć przedmiotu ID {SubjectId} - nie istnieje.", subjectId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Przedmiot o ID '{subjectId}' nie istnieje."
                    );
                    return false;
                }

                if (!subject.IsActive)
                {
                    _logger.LogInformation("Przedmiot ID {SubjectId} był już nieaktywny.", subjectId);
                    // Mimo wszystko unieważnij cache, bo mogło dojść do zmiany np. przypisań nauczycieli w międzyczasie
                    InvalidateCache(subjectId: subjectId, invalidateTeachersList: true, invalidateAll: true);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Przedmiot '{subject.Name}' był już nieaktywny."
                    );
                    return true;
                }

                // Przed dezaktywacją przedmiotu, zdezaktywuj wszystkie aktywne przypisania nauczycieli (UserSubject) do tego przedmiotu.
                var assignments = await _userSubjectRepository.FindAsync(us => us.SubjectId == subjectId && us.IsActive);
                if (assignments.Any())
                {
                    foreach (var assignment in assignments)
                    {
                        assignment.MarkAsDeleted(_currentUserService.GetCurrentUserUpn() ?? "system"); // Ustawia IsActive = false
                        _userSubjectRepository.Update(assignment);
                    }
                    _logger.LogInformation("Zdezaktywowano {Count} przypisań nauczycieli do przedmiotu {SubjectId} przed usunięciem przedmiotu.", assignments.Count(), subjectId);
                }

                subject.MarkAsDeleted(_currentUserService.GetCurrentUserUpn() ?? "system"); // Ustawia IsActive = false
                _subjectRepository.Update(subject);

                InvalidateCache(subjectId: subjectId, invalidateTeachersList: true, invalidateAll: true);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Przedmiot oznaczony jako usunięty wraz z przypisaniami."
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

            string cacheKey = TeachersForSubjectCacheKeyPrefix + subjectId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<User>? cachedTeachers) && cachedTeachers != null)
            {
                _logger.LogDebug("Nauczyciele dla przedmiotu ID: {SubjectId} znalezieni w cache.", subjectId);
                return cachedTeachers;
            }

            _logger.LogDebug("Nauczyciele dla przedmiotu ID: {SubjectId} nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", subjectId);
            var teachersFromDb = await _subjectRepository.GetTeachersAsync(subjectId);

            _cache.Set(cacheKey, teachersFromDb, GetDefaultCacheEntryOptions(_shortCacheDuration));
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
            InvalidateCache(subjectId: subjectId, invalidateTeachersList: true);
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla przedmiotów.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a przedmiotów.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache przedmiotów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unieważnia cache dla przedmiotów.
        /// Resetuje globalny token dla przedmiotów, co unieważnia wszystkie zależne wpisy.
        /// Dodatkowo, jawnie usuwa klucze cache'a na podstawie podanych parametrów
        /// dla natychmiastowego efektu.
        /// </summary>
        /// <param name="subjectId">ID konkretnego przedmiotu do usunięcia z cache (opcjonalnie).</param>
        /// <param name="invalidateTeachersList">Czy unieważnić cache listy nauczycieli dla konkretnego przedmiotu (opcjonalnie).</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie klucze związane z przedmiotami (opcjonalnie, domyślnie false).</param>
        private void InvalidateCache(string? subjectId = null, bool invalidateTeachersList = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u przedmiotów. subjectId: {SubjectId}, invalidateTeachersList: {InvalidateTeachers}, invalidateAll: {InvalidateAll}",
                             subjectId, invalidateTeachersList, invalidateAll);

            // 1. Zresetuj CancellationTokenSource - to unieważni wszystkie wpisy używające tego tokenu.
            var oldTokenSource = Interlocked.Exchange(ref _subjectsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla przedmiotów został zresetowany.");

            // 2. Jawnie usuń kluczowe listy
            _cache.Remove(AllSubjectsCacheKey);
            _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych przedmiotów.");

            // 3. Jeśli podano subjectId, usuń specyficzne klucze dla tego przedmiotu
            if (!string.IsNullOrWhiteSpace(subjectId))
            {
                _cache.Remove(SubjectByIdCacheKeyPrefix + subjectId);
                _logger.LogDebug("Usunięto z cache przedmiot o ID: {SubjectId}", subjectId);

                if (invalidateTeachersList)
                {
                    _cache.Remove(TeachersForSubjectCacheKeyPrefix + subjectId);
                    _logger.LogDebug("Usunięto z cache listę nauczycieli dla przedmiotu o ID: {SubjectId}", subjectId);
                }
            }

            if (invalidateAll)
            {
                _logger.LogDebug("Globalna inwalidacja (invalidateAll=true) dla cache'u przedmiotów.");
                // Reset tokenu już załatwia globalną inwalidację, ale można tu dodać dodatkowe operacje, jeśli potrzebne
            }
        }
    }
}