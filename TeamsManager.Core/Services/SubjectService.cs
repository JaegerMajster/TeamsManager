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
        private readonly IGenericRepository<UserSubject> _userSubjectRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SubjectService> _logger;
        private readonly IMemoryCache _cache;

        // Klucze cache
        private const string AllSubjectsCacheKey = "Subjects_AllActive";
        private const string SubjectByIdCacheKeyPrefix = "Subject_Id_";
        private const string TeachersForSubjectCacheKeyPrefix = "Subject_Teachers_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30);
        private readonly TimeSpan _shortCacheDuration = TimeSpan.FromMinutes(5);

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
            IMemoryCache memoryCache)
        {
            _subjectRepository = subjectRepository ?? throw new ArgumentNullException(nameof(subjectRepository));
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userSubjectRepository = userSubjectRepository ?? throw new ArgumentNullException(nameof(userSubjectRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions(TimeSpan? duration = null)
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(duration ?? _defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_subjectsCacheTokenSource.Token));
        }

        /// <inheritdoc />
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
                if (cachedSubject != null && !string.IsNullOrEmpty(cachedSubject.DefaultSchoolTypeId) && cachedSubject.DefaultSchoolType == null)
                {
                    _logger.LogDebug("Dociąganie DefaultSchoolType dla przedmiotu {SubjectId} z cache.", subjectId);
                    cachedSubject.DefaultSchoolType = await _schoolTypeRepository.GetByIdAsync(cachedSubject.DefaultSchoolTypeId);
                }
                return cachedSubject;
            }

            _logger.LogDebug("Przedmiot ID: {SubjectId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", subjectId);
            var subjectFromDb = await _subjectRepository.GetByIdWithDetailsAsync(subjectId);

            if (subjectFromDb != null)
            {
                _cache.Set(cacheKey, subjectFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Przedmiot ID: {SubjectId} dodany do cache.", subjectId);
            }
            else
            {
                _cache.Remove(cacheKey);
            }
            return subjectFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<Subject>> GetAllActiveSubjectsAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych przedmiotów. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllSubjectsCacheKey, out IEnumerable<Subject>? cachedSubjects) && cachedSubjects != null)
            {
                _logger.LogDebug("Wszystkie aktywne przedmioty znalezione w cache.");
                return cachedSubjects;
            }

            _logger.LogDebug("Wszystkie aktywne przedmioty nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var subjectsFromDb = await _subjectRepository.GetAllActiveWithDetailsAsync();

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
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SubjectCreated,
                TargetEntityType = nameof(Subject),
                TargetEntityName = name,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia przedmiotu: '{SubjectName}' przez {User}", name, currentUserUpn);

                if (string.IsNullOrWhiteSpace(name))
                {
                    operation.MarkAsFailed("Nazwa przedmiotu nie może być pusta.");
                    _logger.LogError("Nie można utworzyć przedmiotu: Nazwa jest pusta.");
                    return null;
                }

                if (!string.IsNullOrWhiteSpace(code))
                {
                    var existingByCode = await _subjectRepository.GetByCodeAsync(code);
                    if (existingByCode != null)
                    {
                        operation.MarkAsFailed($"Przedmiot o kodzie '{code}' już istnieje.");
                        _logger.LogError("Nie można utworzyć przedmiotu: Przedmiot o kodzie {SubjectCode} już istnieje.", code);
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
                        defaultSchoolTypeId = null;
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

                operation.TargetEntityId = newSubject.Id;
                operation.MarkAsCompleted($"Przedmiot ID: {newSubject.Id} ('{newSubject.Name}') przygotowany do utworzenia.");
                _logger.LogInformation("Przedmiot '{SubjectName}' pomyślnie przygotowany do zapisu. ID: {SubjectId}", name, newSubject.Id);

                InvalidateCache(subjectId: newSubject.Id, invalidateAll: true); // Pełna inwalidacja przy tworzeniu
                return newSubject;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia przedmiotu {SubjectName}. Wiadomość: {ErrorMessage}", name, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
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


            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SubjectUpdated,
                TargetEntityType = nameof(Subject),
                TargetEntityId = subjectToUpdate.Id,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji przedmiotu ID: {SubjectId} przez {User}", subjectToUpdate.Id, currentUserUpn);

            try
            {
                var existingSubject = await _subjectRepository.GetByIdWithDetailsAsync(subjectToUpdate.Id);
                if (existingSubject == null)
                {
                    operation.MarkAsFailed($"Przedmiot o ID '{subjectToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować przedmiotu ID {SubjectId} - nie istnieje.", subjectToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSubject.Name;


                if (string.IsNullOrWhiteSpace(subjectToUpdate.Name))
                {
                    operation.MarkAsFailed("Nazwa przedmiotu nie może być pusta.");
                    _logger.LogError("Błąd walidacji przy aktualizacji przedmiotu {SubjectId}: Nazwa pusta.", subjectToUpdate.Id);
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(subjectToUpdate.Code) && existingSubject.Code != subjectToUpdate.Code)
                {
                    var conflicting = await _subjectRepository.GetByCodeAsync(subjectToUpdate.Code);
                    if (conflicting != null && conflicting.Id != existingSubject.Id)
                    {
                        operation.MarkAsFailed($"Przedmiot o kodzie '{subjectToUpdate.Code}' już istnieje.");
                        _logger.LogError("Nie można zaktualizować przedmiotu: Kod '{SubjectCode}' już istnieje.", subjectToUpdate.Code);
                        return false;
                    }
                }

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
                        _logger.LogWarning("Podany domyślny typ szkoły (ID: {DefaultSchoolTypeId}) dla przedmiotu '{SubjectName}' nie istnieje lub jest nieaktywny. Powiązanie z typem szkoły nie zostanie zmienione.", subjectToUpdate.DefaultSchoolTypeId, subjectToUpdate.Name);
                        subjectToUpdate.DefaultSchoolTypeId = existingSubject.DefaultSchoolTypeId;
                        subjectToUpdate.DefaultSchoolType = existingSubject.DefaultSchoolType;
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
                existingSubject.IsActive = subjectToUpdate.IsActive;
                existingSubject.MarkAsModified(currentUserUpn);

                _subjectRepository.Update(existingSubject);

                operation.TargetEntityName = existingSubject.Name;
                operation.MarkAsCompleted($"Przedmiot ID: {existingSubject.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Przedmiot ID: {SubjectId} pomyślnie przygotowany do aktualizacji.", existingSubject.Id);

                InvalidateCache(subjectId: existingSubject.Id, invalidateTeachersList: true, invalidateAll: true); // Zmiana mogła wpłynąć na listy
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSubjectAsync(string subjectId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SubjectDeleted,
                TargetEntityType = nameof(Subject),
                TargetEntityId = subjectId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Usuwanie (dezaktywacja) przedmiotu ID: {SubjectId}", subjectId);
            Subject? subject = null;
            try
            {
                subject = await _subjectRepository.GetByIdAsync(subjectId);
                if (subject == null)
                {
                    operation.MarkAsFailed($"Przedmiot o ID '{subjectId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć przedmiotu ID {SubjectId} - nie istnieje.", subjectId);
                    return false;
                }
                operation.TargetEntityName = subject.Name;

                if (!subject.IsActive)
                {
                    operation.MarkAsCompleted($"Przedmiot '{subject.Name}' był już nieaktywny.");
                    _logger.LogInformation("Przedmiot ID {SubjectId} był już nieaktywny.", subjectId);
                    InvalidateCache(subjectId, invalidateTeachersList: true, invalidateAll: true);
                    return true;
                }

                var assignments = await _userSubjectRepository.FindAsync(us => us.SubjectId == subjectId && us.IsActive);
                if (assignments.Any())
                {
                    foreach (var assignment in assignments)
                    {
                        assignment.MarkAsDeleted(currentUserUpn);
                        _userSubjectRepository.Update(assignment);
                    }
                    _logger.LogInformation("Zdezaktywowano {Count} przypisań nauczycieli do przedmiotu {SubjectId}.", assignments.Count(), subjectId);
                }

                subject.MarkAsDeleted(currentUserUpn);
                _subjectRepository.Update(subject);
                operation.MarkAsCompleted("Przedmiot oznaczony jako usunięty wraz z przypisaniami.");

                InvalidateCache(subjectId, invalidateTeachersList: true, invalidateAll: true);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania przedmiotu ID {SubjectId}. Wiadomość: {ErrorMessage}", subjectId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
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

            var subjectExistsAndActive = await _subjectRepository.GetByIdWithDetailsAsync(subjectId);
            if (subjectExistsAndActive == null)
            {
                _logger.LogWarning("Przedmiot o ID {SubjectId} nie istnieje lub jest nieaktywny podczas pobierania nauczycieli.", subjectId);
                _cache.Set(cacheKey, Enumerable.Empty<User>(), GetDefaultCacheEntryOptions(_shortCacheDuration));
                return Enumerable.Empty<User>();
            }

            var teachersFromDb = await _subjectRepository.GetTeachersAsync(subjectId);

            _cache.Set(cacheKey, teachersFromDb, GetDefaultCacheEntryOptions(_shortCacheDuration));
            _logger.LogDebug("Nauczyciele dla przedmiotu ID: {SubjectId} dodani do cache.", subjectId);
            return teachersFromDb;

        }

        /// <inheritdoc />
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a przedmiotów.");
            InvalidateCache(invalidateAll: true);
            _logger.LogInformation("Cache przedmiotów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        /// <inheritdoc />
        public Task InvalidateTeachersCacheForSubjectAsync(string subjectId)
        {
            _logger.LogInformation("Unieważnianie cache listy nauczycieli dla przedmiotu ID: {SubjectId}", subjectId);
            if (string.IsNullOrWhiteSpace(subjectId))
            {
                _logger.LogWarning("Próba unieważnienia cache listy nauczycieli dla pustego ID przedmiotu.");
                return Task.CompletedTask;
            }
            InvalidateCache(subjectId: subjectId, invalidateTeachersList: true);
            return Task.CompletedTask;
        }

        // Prywatna metoda do unieważniania cache.
        private void InvalidateCache(string? subjectId = null, bool invalidateTeachersList = false, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u przedmiotów. subjectId: {SubjectId}, invalidateTeachersList: {InvalidateTeachersList}, invalidateAll: {InvalidateAll}",
                subjectId, invalidateTeachersList, invalidateAll);

            var oldTokenSource = Interlocked.Exchange(ref _subjectsCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla przedmiotów został zresetowany.");

            if (invalidateAll)
            {
                _cache.Remove(AllSubjectsCacheKey);
                _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych przedmiotów (AllSubjectsCacheKey) z powodu invalidateAll=true.");
                // Przy pełnej inwalidacji, nie ma potrzeby usuwać bardziej szczegółowych kluczy, bo token to załatwi.
                // Można by je usunąć dla natychmiastowego efektu, ale nie jest to krytyczne.
                if (!string.IsNullOrWhiteSpace(subjectId))
                {
                    _cache.Remove(SubjectByIdCacheKeyPrefix + subjectId);
                    _cache.Remove(TeachersForSubjectCacheKeyPrefix + subjectId);
                }
                return;
            }

            if (!string.IsNullOrWhiteSpace(subjectId))
            {
                _cache.Remove(SubjectByIdCacheKeyPrefix + subjectId);
                _logger.LogDebug("Usunięto z cache przedmiot o ID: {SubjectId}", subjectId);
                if (invalidateTeachersList)
                {
                    _cache.Remove(TeachersForSubjectCacheKeyPrefix + subjectId);
                    _logger.LogDebug("Usunięto z cache listę nauczycieli dla przedmiotu ID: {SubjectId}", subjectId);
                }
            }
            // Każda modyfikacja pojedynczego przedmiotu lub jego nauczycieli powinna też unieważnić globalną listę.
            _cache.Remove(AllSubjectsCacheKey);
            _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych przedmiotów (AllSubjectsCacheKey) z powodu modyfikacji.");
        }

        // Metoda pomocnicza do zapisu OperationHistory
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
            _logger.LogDebug("Zapisano nowy wpis historii operacji ID: {OperationId} dla przedmiotu.", operation.Id);
        }
    }
}