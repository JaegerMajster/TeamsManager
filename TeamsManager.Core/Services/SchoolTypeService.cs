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
    /// Serwis odpowiedzialny za logikę biznesową typów szkół.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class SchoolTypeService : ISchoolTypeService
    {
        private readonly IGenericRepository<SchoolType> _schoolTypeRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<SchoolTypeService> _logger;
        private readonly IMemoryCache _cache;

        private const string AllSchoolTypesCacheKey = "SchoolTypes_AllActive";
        private const string SchoolTypeByIdCacheKeyPrefix = "SchoolType_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30);

        private static CancellationTokenSource _schoolTypesCacheTokenSource = new CancellationTokenSource();

        public SchoolTypeService(
            IGenericRepository<SchoolType> schoolTypeRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<SchoolTypeService> logger,
            IMemoryCache memoryCache)
        {
            _schoolTypeRepository = schoolTypeRepository ?? throw new ArgumentNullException(nameof(schoolTypeRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return new MemoryCacheEntryOptions()
                .SetAbsoluteExpiration(_defaultCacheDuration)
                .AddExpirationToken(new CancellationChangeToken(_schoolTypesCacheTokenSource.Token));
        }

        /// <inheritdoc />
        public async Task<SchoolType?> GetSchoolTypeByIdAsync(string schoolTypeId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie typu szkoły o ID: {SchoolTypeId}. Wymuszenie odświeżenia: {ForceRefresh}", schoolTypeId, forceRefresh);
            if (string.IsNullOrWhiteSpace(schoolTypeId))
            {
                _logger.LogWarning("Próba pobrania typu szkoły z pustym ID.");
                return null;
            }

            string cacheKey = SchoolTypeByIdCacheKeyPrefix + schoolTypeId;

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out SchoolType? cachedSchoolType))
            {
                _logger.LogDebug("Typ szkoły ID: {SchoolTypeId} znaleziony w cache.", schoolTypeId);
                return cachedSchoolType;
            }

            _logger.LogDebug("Typ szkoły ID: {SchoolTypeId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", schoolTypeId);
            var schoolTypeFromDb = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

            if (schoolTypeFromDb != null)
            {
                _cache.Set(cacheKey, schoolTypeFromDb, GetDefaultCacheEntryOptions());
                _logger.LogDebug("Typ szkoły ID: {SchoolTypeId} dodany do cache.", schoolTypeId);
            }
            else
            {
                _cache.Remove(cacheKey); // Usuń z cache, jeśli nie znaleziono w DB
            }

            return schoolTypeFromDb;
        }

        /// <inheritdoc />
        public async Task<IEnumerable<SchoolType>> GetAllActiveSchoolTypesAsync(bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych typów szkół. Wymuszenie odświeżenia: {ForceRefresh}", forceRefresh);

            if (!forceRefresh && _cache.TryGetValue(AllSchoolTypesCacheKey, out IEnumerable<SchoolType>? cachedSchoolTypes) && cachedSchoolTypes != null)
            {
                _logger.LogDebug("Wszystkie aktywne typy szkół znalezione w cache.");
                return cachedSchoolTypes;
            }

            _logger.LogDebug("Wszystkie aktywne typy szkół nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.");
            var schoolTypesFromDb = await _schoolTypeRepository.FindAsync(st => st.IsActive);

            _cache.Set(AllSchoolTypesCacheKey, schoolTypesFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Wszystkie aktywne typy szkół dodane do cache.");

            return schoolTypesFromDb;
        }

        /// <inheritdoc />
        public async Task<SchoolType?> CreateSchoolTypeAsync(
            string shortName,
            string fullName,
            string description,
            string? colorCode = null,
            int sortOrder = 0)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolTypeCreated,
                TargetEntityType = nameof(SchoolType),
                TargetEntityName = $"{shortName} - {fullName}",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia typu szkoły: {ShortName} - {FullName} przez {User}", shortName, fullName, currentUserUpn);

                if (string.IsNullOrWhiteSpace(shortName) || string.IsNullOrWhiteSpace(fullName))
                {
                    operation.MarkAsFailed("Skrócona nazwa i pełna nazwa typu szkoły są wymagane.");
                    _logger.LogError("Nie można utworzyć typu szkoły: Skrócona nazwa lub pełna nazwa są puste.");
                    return null;
                }

                var existingSchoolType = (await _schoolTypeRepository.FindAsync(st => st.ShortName == shortName && st.IsActive)).FirstOrDefault();
                if (existingSchoolType != null)
                {
                    operation.MarkAsFailed($"Typ szkoły o skróconej nazwie '{shortName}' już istnieje i jest aktywny.");
                    _logger.LogError("Nie można utworzyć typu szkoły: Aktywny typ szkoły o skróconej nazwie {ShortName} już istnieje.", shortName);
                    return null;
                }

                var newSchoolType = new SchoolType
                {
                    Id = Guid.NewGuid().ToString(),
                    ShortName = shortName,
                    FullName = fullName,
                    Description = description,
                    ColorCode = colorCode,
                    SortOrder = sortOrder,
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _schoolTypeRepository.AddAsync(newSchoolType);

                operation.TargetEntityId = newSchoolType.Id;
                operation.MarkAsCompleted($"Typ szkoły ID: {newSchoolType.Id} ('{newSchoolType.ShortName}') przygotowany do utworzenia.");
                _logger.LogInformation("Typ szkoły '{FullName}' pomyślnie przygotowany do zapisu. ID: {SchoolTypeId}", fullName, newSchoolType.Id);

                InvalidateCache();
                return newSchoolType;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia typu szkoły '{FullName}'. Wiadomość: {ErrorMessage}", fullName, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return null;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateSchoolTypeAsync(SchoolType schoolTypeToUpdate)
        {
            if (schoolTypeToUpdate == null || string.IsNullOrEmpty(schoolTypeToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji typu szkoły z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(schoolTypeToUpdate), "Obiekt typu szkoły lub jego ID nie może być null/pusty.");
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolTypeUpdated,
                TargetEntityType = nameof(SchoolType),
                TargetEntityId = schoolTypeToUpdate.Id,
                TargetEntityName = schoolTypeToUpdate.FullName, // Może być wstępnie ustawione, potem zaktualizowane
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie aktualizacji typu szkoły ID: {SchoolTypeId} przez {User}", schoolTypeToUpdate.Id, currentUserUpn);

            string? oldShortName = null;

            try
            {
                var existingSchoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeToUpdate.Id);
                if (existingSchoolType == null)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeToUpdate.Id}' nie istnieje.");
                    _logger.LogWarning("Nie można zaktualizować typu szkoły ID {SchoolTypeId} - nie istnieje.", schoolTypeToUpdate.Id);
                    return false;
                }
                operation.TargetEntityName = existingSchoolType.FullName; // Aktualizacja nazwy w logu operacji
                oldShortName = existingSchoolType.ShortName;

                if (string.IsNullOrWhiteSpace(schoolTypeToUpdate.ShortName) || string.IsNullOrWhiteSpace(schoolTypeToUpdate.FullName))
                {
                    operation.MarkAsFailed("Skrócona nazwa i pełna nazwa są wymagane.");
                    _logger.LogError("Błąd aktualizacji typu szkoły {SchoolTypeId}: Skrócona lub pełna nazwa jest pusta.", schoolTypeToUpdate.Id);
                    return false;
                }

                if (existingSchoolType.ShortName != schoolTypeToUpdate.ShortName)
                {
                    var conflicting = (await _schoolTypeRepository.FindAsync(st => st.Id != existingSchoolType.Id && st.ShortName == schoolTypeToUpdate.ShortName && st.IsActive)).FirstOrDefault();
                    if (conflicting != null)
                    {
                        operation.MarkAsFailed($"Typ szkoły o skróconej nazwie '{schoolTypeToUpdate.ShortName}' już istnieje.");
                        _logger.LogError("Nie można zaktualizować typu szkoły: Skrócona nazwa '{ShortName}' już istnieje.", schoolTypeToUpdate.ShortName);
                        return false;
                    }
                }

                existingSchoolType.ShortName = schoolTypeToUpdate.ShortName;
                existingSchoolType.FullName = schoolTypeToUpdate.FullName;
                existingSchoolType.Description = schoolTypeToUpdate.Description;
                existingSchoolType.ColorCode = schoolTypeToUpdate.ColorCode;
                existingSchoolType.SortOrder = schoolTypeToUpdate.SortOrder;
                existingSchoolType.IsActive = schoolTypeToUpdate.IsActive;
                existingSchoolType.MarkAsModified(currentUserUpn);

                _schoolTypeRepository.Update(existingSchoolType);

                operation.TargetEntityName = existingSchoolType.FullName; // Upewnij się, że log zawiera aktualną nazwę
                operation.MarkAsCompleted($"Typ szkoły ID: {existingSchoolType.Id} przygotowany do aktualizacji.");
                _logger.LogInformation("Typ szkoły ID: {SchoolTypeId} pomyślnie przygotowany do aktualizacji.", existingSchoolType.Id);

                InvalidateCache(existingSchoolType.Id, oldShortName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji typu szkoły ID {SchoolTypeId}. Wiadomość: {ErrorMessage}", schoolTypeToUpdate.Id, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteSchoolTypeAsync(string schoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.SchoolTypeDeleted,
                TargetEntityType = nameof(SchoolType),
                TargetEntityId = schoolTypeId,
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            _logger.LogInformation("Rozpoczynanie usuwania (dezaktywacji) typu szkoły ID: {SchoolTypeId} przez {User}", schoolTypeId, currentUserUpn);

            SchoolType? schoolType = null;
            try
            {
                schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);
                if (schoolType == null)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' nie istnieje.");
                    _logger.LogWarning("Nie można usunąć typu szkoły ID {SchoolTypeId} - nie istnieje.", schoolTypeId);
                    return false;
                }
                operation.TargetEntityName = schoolType.FullName;

                if (!schoolType.IsActive)
                {
                    operation.MarkAsCompleted($"Typ szkoły '{schoolType.FullName}' był już nieaktywny.");
                    _logger.LogInformation("Typ szkoły ID {SchoolTypeId} był już nieaktywny.", schoolTypeId);
                    InvalidateCache(schoolTypeId, schoolType.ShortName); // Mimo wszystko unieważnij, jeśli był w cache
                    return true;
                }

                schoolType.MarkAsDeleted(currentUserUpn);
                _schoolTypeRepository.Update(schoolType);

                operation.MarkAsCompleted($"Typ szkoły '{schoolType.FullName}' (ID: {schoolTypeId}) oznaczony jako usunięty.");
                _logger.LogInformation("Typ szkoły ID {SchoolTypeId} pomyślnie oznaczony jako usunięty.", schoolTypeId);

                InvalidateCache(schoolTypeId, schoolType.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania typu szkoły ID {SchoolTypeId}. Wiadomość: {ErrorMessage}", schoolTypeId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> AssignViceDirectorToSchoolTypeAsync(string viceDirectorUserId, string schoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_assign_vd";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserAssignedToSchoolType,
                TargetEntityType = "UserSchoolTypeSupervision", // Ogólny typ dla relacji
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();

            try
            {
                _logger.LogInformation("Przypisywanie wicedyrektora {ViceDirectorUserId} do typu szkoły {SchoolTypeId} przez {User}", viceDirectorUserId, schoolTypeId, currentUserUpn);

                var viceDirector = await _userRepository.GetByIdAsync(viceDirectorUserId); // Pobierz pełny obiekt użytkownika
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

                if (viceDirector == null || !viceDirector.IsActive)
                {
                    operation.MarkAsFailed($"Użytkownik (wicedyrektor) o ID '{viceDirectorUserId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można przypisać wicedyrektora: Użytkownik ID {ViceDirectorUserId} nie istnieje lub nieaktywny.", viceDirectorUserId);
                    return false;
                }
                if (viceDirector.Role != UserRole.Wicedyrektor && viceDirector.Role != UserRole.Dyrektor && !viceDirector.IsSystemAdmin)
                {
                    operation.MarkAsFailed($"Użytkownik '{viceDirector.UPN}' nie ma uprawnień wicedyrektora, dyrektora ani administratora systemu.");
                    _logger.LogWarning("Nie można przypisać wicedyrektora: Użytkownik {ViceDirectorUPN} nie ma odpowiednich uprawnień.", viceDirector.UPN);
                    return false;
                }
                if (schoolType == null || !schoolType.IsActive)
                {
                    operation.MarkAsFailed($"Typ szkoły o ID '{schoolTypeId}' nie istnieje lub jest nieaktywny.");
                    _logger.LogWarning("Nie można przypisać wicedyrektora: Typ szkoły ID {SchoolTypeId} nie istnieje lub nieaktywny.", schoolTypeId);
                    return false;
                }
                // Ustawienie kontekstu operacji na nadzór
                operation.TargetEntityType = nameof(SchoolType); // lub bardziej specyficzny, jeśli jest
                operation.TargetEntityId = schoolTypeId;
                operation.TargetEntityName = $"Nadzór {viceDirector.UPN} nad {schoolType.ShortName}";


                if (viceDirector.SupervisedSchoolTypes.Any(st => st.Id == schoolTypeId))
                {
                    operation.MarkAsCompleted("Wicedyrektor był już przypisany do nadzoru tego typu szkoły.");
                    _logger.LogInformation("Wicedyrektor {ViceDirectorUPN} był już przypisany do nadzoru typu szkoły {SchoolTypeName}", viceDirector.UPN, schoolType.ShortName);
                    return true;
                }

                viceDirector.SupervisedSchoolTypes.Add(schoolType);
                _userRepository.Update(viceDirector);

                operation.MarkAsCompleted("Wicedyrektor pomyślnie przypisany do nadzoru typu szkoły.");
                _logger.LogInformation("Wicedyrektor {ViceDirectorUPN} pomyślnie przypisany do nadzoru typu szkoły {SchoolTypeName}", viceDirector.UPN, schoolType.ShortName);

                InvalidateCache(schoolTypeId, schoolType.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas przypisywania wicedyrektora {ViceDirectorUserId} do typu szkoły {SchoolTypeId}. Wiadomość: {ErrorMessage}", viceDirectorUserId, schoolTypeId, ex.Message);
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString());
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation);
            }
        }

        /// <inheritdoc />
        public async Task<bool> RemoveViceDirectorFromSchoolTypeAsync(string viceDirectorUserId, string schoolTypeId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_remove_vd";
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(),
                Type = OperationType.UserRemovedFromSchoolType,
                TargetEntityType = "UserSchoolTypeSupervision",
                CreatedBy = currentUserUpn,
                IsActive = true
            };
            operation.MarkAsStarted();
            try
            {
                _logger.LogInformation("Usuwanie nadzoru wicedyrektora {ViceDirectorUserId} z typu szkoły {SchoolTypeId} przez {User}", viceDirectorUserId, schoolTypeId, currentUserUpn);

                var viceDirector = await _userRepository.GetByIdAsync(viceDirectorUserId); // Pobierz pełny obiekt użytkownika
                var schoolType = await _schoolTypeRepository.GetByIdAsync(schoolTypeId);

                if (viceDirector == null || schoolType == null)
                {
                    operation.MarkAsFailed("Wicedyrektor lub typ szkoły nie istnieje.");
                    _logger.LogWarning("Nie można usunąć nadzoru: Wicedyrektor ID {ViceDirectorUserId} lub Typ Szkoły ID {SchoolTypeId} nie istnieje.", viceDirectorUserId, schoolTypeId);
                    return false;
                }
                operation.TargetEntityType = nameof(SchoolType);
                operation.TargetEntityId = schoolTypeId;
                operation.TargetEntityName = $"Usunięcie nadzoru {viceDirector.UPN} nad {schoolType.ShortName}";

                var supervisedSchoolTypeToRemove = viceDirector.SupervisedSchoolTypes.FirstOrDefault(st => st.Id == schoolTypeId);
                if (supervisedSchoolTypeToRemove == null)
                {
                    operation.MarkAsCompleted("Wicedyrektor nie nadzorował tego typu szkoły.");
                    _logger.LogInformation("Wicedyrektor {ViceDirectorUPN} nie nadzorował typu szkoły {SchoolTypeName}.", viceDirector.UPN, schoolType.ShortName);
                    return true; // Operacja zakończona sukcesem, bo stan docelowy osiągnięty
                }

                viceDirector.SupervisedSchoolTypes.Remove(supervisedSchoolTypeToRemove);
                _userRepository.Update(viceDirector);

                operation.MarkAsCompleted("Pomyślnie usunięto przypisanie wicedyrektora z nadzoru typu szkoły.");
                _logger.LogInformation("Pomyślnie usunięto nadzór wicedyrektora {ViceDirectorUPN} z typu szkoły {SchoolTypeName}.", viceDirector.UPN, schoolType.ShortName);

                InvalidateCache(schoolTypeId, schoolType.ShortName);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania nadzoru wicedyrektora {ViceDirectorUserId} z typu szkoły {SchoolTypeId}. Wiadomość: {ErrorMessage}", viceDirectorUserId, schoolTypeId, ex.Message);
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
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a typów szkół.");
            InvalidateCache();
            _logger.LogInformation("Cache typów szkół został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private void InvalidateCache(string? schoolTypeId = null, string? shortName = null)
        {
            var oldTokenSource = Interlocked.Exchange(ref _schoolTypesCacheTokenSource, new CancellationTokenSource());
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel();
                oldTokenSource.Dispose();
            }
            _logger.LogDebug("Token cache'u dla typów szkół został zresetowany.");

            // Zawsze usuwaj klucz dla listy wszystkich aktywnych typów,
            // ponieważ każda operacja (Create, Update, Delete) może na nią wpłynąć.
            _cache.Remove(AllSchoolTypesCacheKey);
            _logger.LogDebug("Usunięto z cache klucz dla wszystkich aktywnych typów szkół: {CacheKey}", AllSchoolTypesCacheKey);

            if (!string.IsNullOrEmpty(schoolTypeId))
            {
                _cache.Remove(SchoolTypeByIdCacheKeyPrefix + schoolTypeId);
                _logger.LogDebug("Usunięto z cache typ szkoły o ID: {SchoolTypeId}", schoolTypeId);
            }
            // Parametr 'shortName' obecnie nie jest wykorzystywany.
        }

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
                // Aktualizuj istniejący wpis, jeśli operacja była śledzona od początku
                existingLog.Status = operation.Status;
                existingLog.CompletedAt = operation.CompletedAt;
                existingLog.Duration = operation.Duration;
                existingLog.ErrorMessage = operation.ErrorMessage;
                existingLog.ErrorStackTrace = operation.ErrorStackTrace;
                existingLog.OperationDetails = operation.OperationDetails;
                existingLog.TargetEntityName = operation.TargetEntityName; // Upewnij się, że nazwa jest aktualna
                existingLog.TargetEntityId = operation.TargetEntityId;
                existingLog.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system_log_update");
                _operationHistoryRepository.Update(existingLog);
            }
        }
    }
}