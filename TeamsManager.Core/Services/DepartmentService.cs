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
    /// Serwis odpowiedzialny za logikę biznesową działów.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class DepartmentService : IDepartmentService
    {
        private readonly IGenericRepository<Department> _departmentRepository;
        private readonly IUserRepository _userRepository;
        private readonly IOperationHistoryRepository _operationHistoryRepository;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DepartmentService> _logger;
        private readonly IMemoryCache _cache;

        // Klucze cache
        private const string AllDepartmentsRootOnlyCacheKey = "Departments_AllActive_RootOnly";
        private const string AllDepartmentsAllCacheKey = "Departments_AllActive_All";
        private const string DepartmentByIdCacheKeyPrefix = "Department_Id_";
        private const string SubDepartmentsByParentIdCacheKeyPrefix = "Department_Sub_ParentId_";
        private const string UsersInDepartmentCacheKeyPrefix = "Department_UsersIn_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30);

        // Token do unieważniania cache'u dla działów
        private static CancellationTokenSource _departmentsCacheTokenSource = new CancellationTokenSource();

        /// <summary>
        /// Konstruktor serwisu działów.
        /// </summary>
        public DepartmentService(
            IGenericRepository<Department> departmentRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<DepartmentService> logger,
            IMemoryCache memoryCache)
        {
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
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
                .AddExpirationToken(new CancellationChangeToken(_departmentsCacheTokenSource.Token));
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments = false, bool includeUsers = false, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie działu o ID: {DepartmentId}. Dołączanie poddziałów: {IncludeSubDepartments}, Dołączanie użytkowników: {IncludeUsers}, Wymuszenie odświeżenia: {ForceRefresh}", //
                                departmentId, includeSubDepartments, includeUsers, forceRefresh);

            if (string.IsNullOrWhiteSpace(departmentId))
            {
                _logger.LogWarning("Próba pobrania działu z pustym ID."); //
                return null;
            }

            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId;
            Department? department;

            // Pobieranie bazowego obiektu działu
            if (!forceRefresh && _cache.TryGetValue(cacheKey, out department) && department != null)
            {
                _logger.LogDebug("Dział ID: {DepartmentId} znaleziony w cache (tylko obiekt bazowy).", departmentId); //
            }
            else
            {
                _logger.LogDebug("Dział ID: {DepartmentId} nie znaleziony w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", departmentId); //
                department = await _departmentRepository.GetByIdAsync(departmentId); //

                if (department != null)
                {
                    _cache.Set(cacheKey, department, GetDefaultCacheEntryOptions());
                    _logger.LogDebug("Dział ID: {DepartmentId} dodany do cache (obiekt bazowy).", departmentId); //
                }
                else
                {
                    _cache.Remove(cacheKey);
                }
            }

            // Jeśli dział istnieje, dociągnij opcjonalne powiązania
            if (department != null)
            {
                if (includeSubDepartments)
                {
                    department.SubDepartments = (await GetSubDepartmentsAsync(departmentId, forceRefresh)).ToList(); //
                    _logger.LogDebug("Załadowano {Count} poddziałów dla działu {DepartmentId}. forceRefresh: {ForceRefresh}", department.SubDepartments.Count, departmentId, forceRefresh); //
                }
                if (includeUsers)
                {
                    department.Users = (await GetUsersInDepartmentAsync(departmentId, forceRefresh)).ToList(); //
                    _logger.LogDebug("Załadowano {Count} użytkowników dla działu {DepartmentId}. forceRefresh: {ForceRefresh}", department.Users.Count, departmentId, forceRefresh); //
                }
            }
            return department;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<Department>> GetAllDepartmentsAsync(bool onlyRootDepartments = false, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie wszystkich aktywnych działów. Tylko główne: {OnlyRoot}, Wymuszenie odświeżenia: {ForceRefresh}", onlyRootDepartments, forceRefresh); //
            string cacheKey = onlyRootDepartments ? AllDepartmentsRootOnlyCacheKey : AllDepartmentsAllCacheKey; //

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Department>? cachedDepartments) && cachedDepartments != null)
            {
                _logger.LogDebug("Lista działów (OnlyRoot={OnlyRoot}) znaleziona w cache.", onlyRootDepartments); //
                return cachedDepartments;
            }

            _logger.LogDebug("Lista działów (OnlyRoot={OnlyRoot}) nie znaleziona w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", onlyRootDepartments); //
            var departmentsFromDb = onlyRootDepartments
                ? await _departmentRepository.FindAsync(d => d.IsActive && d.ParentDepartmentId == null) //
                : await _departmentRepository.FindAsync(d => d.IsActive); //

            _cache.Set(cacheKey, departmentsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Lista działów (OnlyRoot={OnlyRoot}) dodana do cache.", onlyRootDepartments); //

            return departmentsFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie poddziałów dla działu ID: {ParentDepartmentId}. Wymuszenie odświeżenia: {ForceRefresh}", parentDepartmentId, forceRefresh); //
            if (string.IsNullOrWhiteSpace(parentDepartmentId))
            {
                _logger.LogWarning("Próba pobrania poddziałów dla pustego ID rodzica."); //
                return Enumerable.Empty<Department>();
            }

            string cacheKey = SubDepartmentsByParentIdCacheKeyPrefix + parentDepartmentId; //

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<Department>? cachedSubDepartments) && cachedSubDepartments != null)
            {
                _logger.LogDebug("Poddziały dla rodzica ID: {ParentDepartmentId} znalezione w cache.", parentDepartmentId); //
                return cachedSubDepartments;
            }

            _logger.LogDebug("Poddziały dla rodzica ID: {ParentDepartmentId} nie znalezione w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", parentDepartmentId); //
            var subDepartmentsFromDb = await _departmentRepository.FindAsync(d => d.ParentDepartmentId == parentDepartmentId && d.IsActive); //

            _cache.Set(cacheKey, subDepartmentsFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Poddziały dla rodzica ID: {ParentDepartmentId} dodane do cache.", parentDepartmentId); //

            return subDepartmentsFromDb;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkowników dla działu ID: {DepartmentId}. Wymuszenie odświeżenia: {ForceRefresh}", departmentId, forceRefresh); //
            if (string.IsNullOrWhiteSpace(departmentId))
            {
                _logger.LogWarning("Próba pobrania użytkowników dla pustego ID działu."); //
                return Enumerable.Empty<User>();
            }

            string cacheKey = UsersInDepartmentCacheKeyPrefix + departmentId; //

            if (!forceRefresh && _cache.TryGetValue(cacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Użytkownicy dla działu ID: {DepartmentId} znalezieni w cache.", departmentId); //
                return cachedUsers;
            }

            _logger.LogDebug("Użytkownicy dla działu ID: {DepartmentId} nie znalezieni w cache lub wymuszono odświeżenie. Pobieranie z repozytorium.", departmentId); //
            var usersFromDb = await _userRepository.FindAsync(u => u.DepartmentId == departmentId && u.IsActive); //

            _cache.Set(cacheKey, usersFromDb, GetDefaultCacheEntryOptions());
            _logger.LogDebug("Użytkownicy dla działu ID: {DepartmentId} dodani do cache.", departmentId); //

            return usersFromDb;
        }

        /// <inheritdoc />
        public async Task<Department?> CreateDepartmentAsync(
            string name,
            string description,
            string? parentDepartmentId = null,
            string? departmentCode = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_creation"; //
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(), //
                Type = OperationType.DepartmentCreated, //
                TargetEntityType = nameof(Department), //
                TargetEntityName = name, //
                CreatedBy = currentUserUpn, //
                IsActive = true //
            };
            operation.MarkAsStarted(); //

            try
            {
                _logger.LogInformation("Rozpoczynanie tworzenia działu: '{DepartmentName}' przez {User}", name, currentUserUpn); //

                if (string.IsNullOrWhiteSpace(name))
                {
                    var message = "Nazwa działu nie może być pusta."; //
                    operation.MarkAsFailed(message); //
                    _logger.LogError("Nie można utworzyć działu: {ErrorReason}", message); //
                    throw new ArgumentException(message, nameof(name)); //
                }

                Department? parentDepartment = null; //
                if (!string.IsNullOrEmpty(parentDepartmentId))
                {
                    parentDepartment = await _departmentRepository.GetByIdAsync(parentDepartmentId); //
                    if (parentDepartment == null || !parentDepartment.IsActive)
                    {
                        operation.MarkAsFailed($"Dział nadrzędny o ID '{parentDepartmentId}' nie istnieje lub jest nieaktywny."); //
                        _logger.LogWarning("Nie można utworzyć działu: Dział nadrzędny o ID {ParentDepartmentId} nie istnieje lub jest nieaktywny.", parentDepartmentId); //
                        return null;
                    }
                }

                var newDepartment = new Department
                {
                    Id = Guid.NewGuid().ToString(), //
                    Name = name, //
                    Description = description, //
                    ParentDepartmentId = parentDepartmentId, //
                    ParentDepartment = parentDepartment, //
                    DepartmentCode = departmentCode, //
                    CreatedBy = currentUserUpn, //
                    IsActive = true //
                };

                await _departmentRepository.AddAsync(newDepartment); //

                operation.TargetEntityId = newDepartment.Id; //
                operation.MarkAsCompleted($"Dział ID: {newDepartment.Id} przygotowany do utworzenia."); //
                _logger.LogInformation("Dział '{DepartmentName}' pomyślnie przygotowany do zapisu. ID: {DepartmentId}", name, newDepartment.Id); //

                InvalidateCache(departmentId: newDepartment.Id, parentId: newDepartment.ParentDepartmentId, invalidateAll: true);
                return newDepartment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia działu {DepartmentName}. Wiadomość: {ErrorMessage}", name, ex.Message); //
                operation.MarkAsFailed($"Krytyczny błąd: {ex.Message}", ex.ToString()); //
                throw;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation); //
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateDepartmentAsync(Department departmentToUpdate)
        {
            if (departmentToUpdate == null || string.IsNullOrEmpty(departmentToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji działu z nieprawidłowymi danymi (null lub brak ID)."); //
                throw new ArgumentNullException(nameof(departmentToUpdate), "Obiekt działu lub jego ID nie może być null/pusty.");
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_update"; //
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(), //
                Type = OperationType.DepartmentUpdated, //
                TargetEntityType = nameof(Department), //
                TargetEntityId = departmentToUpdate.Id, //
                TargetEntityName = departmentToUpdate.Name, //
                CreatedBy = currentUserUpn, //
                IsActive = true //
            };
            operation.MarkAsStarted(); //
            _logger.LogInformation("Aktualizowanie działu ID: {DepartmentId}", departmentToUpdate.Id); //

            string? oldParentId = null; //
            try
            {
                var existingDepartment = await _departmentRepository.GetByIdAsync(departmentToUpdate.Id); //
                if (existingDepartment == null)
                {
                    operation.MarkAsFailed("Dział nie istnieje."); //
                    _logger.LogWarning("Nie można zaktualizować działu ID {DepartmentId} - nie istnieje.", departmentToUpdate.Id); //
                    return false;
                }
                operation.TargetEntityName = existingDepartment.Name;
                oldParentId = existingDepartment.ParentDepartmentId; //

                if (string.IsNullOrWhiteSpace(departmentToUpdate.Name))
                {
                    operation.MarkAsFailed("Nazwa działu nie może być pusta."); //
                    _logger.LogError("Błąd walidacji przy aktualizacji działu {DepartmentId}: Nazwa pusta.", departmentToUpdate.Id); //
                    return false;
                }

                if (!string.IsNullOrEmpty(departmentToUpdate.ParentDepartmentId))
                {
                    if (departmentToUpdate.ParentDepartmentId == departmentToUpdate.Id) //
                    {
                        operation.MarkAsFailed("Dział nie może być swoim własnym rodzicem."); //
                        _logger.LogWarning("Próba ustawienia działu {DepartmentId} jako swojego własnego rodzica.", departmentToUpdate.Id); //
                        return false;
                    }

                    var parentDepartment = await _departmentRepository.GetByIdAsync(departmentToUpdate.ParentDepartmentId); //
                    if (parentDepartment == null || !parentDepartment.IsActive)
                    {
                        operation.MarkAsFailed($"Dział nadrzędny o ID '{departmentToUpdate.ParentDepartmentId}' nie istnieje lub jest nieaktywny."); //
                        _logger.LogWarning("Dział nadrzędny {ParentDepartmentId} nie istnieje lub jest nieaktywny.", departmentToUpdate.ParentDepartmentId); //
                        return false;
                    }

                    if (await IsDescendantAsync(departmentToUpdate.ParentDepartmentId, departmentToUpdate.Id)) //
                    {
                        operation.MarkAsFailed("Nie można ustawić działu jako rodzica, ponieważ spowodowałoby to cykliczną zależność."); //
                        _logger.LogWarning("Próba utworzenia cyklicznej zależności między działami {DepartmentId} i {ParentDepartmentId}.", //
                            departmentToUpdate.Id, departmentToUpdate.ParentDepartmentId);
                        return false;
                    }
                    existingDepartment.ParentDepartment = parentDepartment;
                }
                else
                {
                    existingDepartment.ParentDepartment = null;
                }

                existingDepartment.Name = departmentToUpdate.Name; //
                existingDepartment.Description = departmentToUpdate.Description; //
                existingDepartment.ParentDepartmentId = departmentToUpdate.ParentDepartmentId; //
                existingDepartment.DepartmentCode = departmentToUpdate.DepartmentCode; //
                existingDepartment.Email = departmentToUpdate.Email; //
                existingDepartment.Phone = departmentToUpdate.Phone; //
                existingDepartment.Location = departmentToUpdate.Location; //
                existingDepartment.SortOrder = departmentToUpdate.SortOrder; //
                existingDepartment.IsActive = departmentToUpdate.IsActive;
                existingDepartment.MarkAsModified(currentUserUpn); //

                _departmentRepository.Update(existingDepartment); //
                operation.TargetEntityName = existingDepartment.Name;
                operation.MarkAsCompleted("Dział przygotowany do aktualizacji."); //

                InvalidateCache(departmentId: existingDepartment.Id, parentId: oldParentId, invalidateAll: true);
                if (existingDepartment.ParentDepartmentId != oldParentId && !string.IsNullOrWhiteSpace(existingDepartment.ParentDepartmentId))
                {
                    InvalidateCache(parentId: existingDepartment.ParentDepartmentId, invalidateAll: true);
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji działu ID {DepartmentId}. Wiadomość: {ErrorMessage}", departmentToUpdate.Id, ex.Message); //
                operation.MarkAsFailed($"Błąd: {ex.Message}", ex.ToString()); //
                return false;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation); //
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteDepartmentAsync(string departmentId)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system_delete"; //
            var operation = new OperationHistory
            {
                Id = Guid.NewGuid().ToString(), //
                Type = OperationType.DepartmentDeleted, //
                TargetEntityType = nameof(Department), //
                TargetEntityId = departmentId, //
                CreatedBy = currentUserUpn, //
                IsActive = true //
            };
            operation.MarkAsStarted(); //
            _logger.LogInformation("Usuwanie działu ID: {DepartmentId}", departmentId); //

            Department? department = null; //
            try
            {
                department = await _departmentRepository.GetByIdAsync(departmentId); //
                if (department == null)
                {
                    operation.MarkAsFailed("Dział nie istnieje."); //
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - nie istnieje.", departmentId); //
                    return false;
                }
                operation.TargetEntityName = department.Name; //

                if (!department.IsActive)
                {
                    operation.MarkAsCompleted($"Dział '{department.Name}' był już nieaktywny."); //
                    _logger.LogInformation("Dział ID {DepartmentId} był już nieaktywny.", departmentId); //
                    InvalidateCache(departmentId, department.ParentDepartmentId, invalidateAll: true);
                    return true;
                }

                var subDepartments = await GetSubDepartmentsAsync(departmentId); //
                if (subDepartments.Any())
                {
                    var message = "Nie można usunąć działu, ponieważ ma przypisane aktywne poddziały."; //
                    operation.MarkAsFailed(message); //
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - ma aktywne poddziały.", departmentId); //
                    throw new InvalidOperationException(message); //
                }

                var usersInDepartment = await _userRepository.FindAsync(u => u.DepartmentId == departmentId && u.IsActive); //
                if (usersInDepartment.Any())
                {
                    var message = "Nie można usunąć działu, ponieważ ma przypisanych aktywnych użytkowników."; //
                    operation.MarkAsFailed(message); //
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - ma aktywnych użytkowników.", departmentId); //
                    throw new InvalidOperationException(message); //
                }

                department.MarkAsDeleted(currentUserUpn); //
                _departmentRepository.Update(department); //

                operation.MarkAsCompleted("Dział oznaczony jako usunięty."); //
                InvalidateCache(departmentId: departmentId, parentId: department.ParentDepartmentId, invalidateAll: true);
                return true;
            }
            catch
            {
                throw;
            }
            finally
            {
                await SaveOperationHistoryAsync(operation); //
            }
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla działów.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a działów."); //
            InvalidateCache(invalidateAll: true); //
            _logger.LogInformation("Cache działów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu."); //
            return Task.CompletedTask;
        }

        /// <summary>
        /// Unieważnia cache dla działów.
        /// Resetuje globalny token dla działów, co unieważnia wszystkie zależne wpisy.
        /// Dodatkowo, jawnie usuwa klucze cache'a na podstawie podanych parametrów
        /// dla natychmiastowego efektu oraz klucze globalne.
        /// </summary>
        /// <param name="departmentId">ID działu, którego specyficzny cache ma być usunięty (opcjonalnie).</param>
        /// <param name="parentId">ID działu nadrzędnego, którego cache poddziałów ma być usunięty (opcjonalnie).</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie klucze związane z działami (opcjonalnie, domyślnie false).</param>
        private void InvalidateCache(string? departmentId = null, string? parentId = null, bool invalidateAll = false)
        {
            _logger.LogDebug("Inwalidacja cache'u działów. departmentId: {DepartmentId}, parentId: {ParentId}, invalidateAll: {InvalidateAll}", //
                departmentId, parentId, invalidateAll);

            // 1. Zresetuj CancellationTokenSource
            var oldTokenSource = Interlocked.Exchange(ref _departmentsCacheTokenSource, new CancellationTokenSource()); //
            if (oldTokenSource != null && !oldTokenSource.IsCancellationRequested)
            {
                oldTokenSource.Cancel(); //
                oldTokenSource.Dispose(); //
            }
            _logger.LogDebug("Token cache'u dla działów został zresetowany."); //

            // 2. Jawnie usuń globalne klucze cache
            _cache.Remove(AllDepartmentsRootOnlyCacheKey); //
            _cache.Remove(AllDepartmentsAllCacheKey); //
            _logger.LogDebug("Usunięto z cache klucze: {Key1}, {Key2}", AllDepartmentsRootOnlyCacheKey, AllDepartmentsAllCacheKey);

            // 3. Jawnie usuń klucze dla specyficznych elementów, jeśli dotyczy
            if (!string.IsNullOrWhiteSpace(departmentId))
            {
                _cache.Remove(DepartmentByIdCacheKeyPrefix + departmentId); //
                _cache.Remove(UsersInDepartmentCacheKeyPrefix + departmentId);
                _cache.Remove(SubDepartmentsByParentIdCacheKeyPrefix + departmentId);
                _logger.LogDebug("Usunięto z cache wpisy specyficzne dla działu ID: {DepartmentId}", departmentId); //
            }
            if (!string.IsNullOrWhiteSpace(parentId))
            {
                _cache.Remove(SubDepartmentsByParentIdCacheKeyPrefix + parentId);
                _logger.LogDebug("Usunięto z cache wpisy dla poddziałów rodzica ID: {ParentId}", parentId); //
            }

            // Jeśli invalidateAll jest true, powyższe jawne usunięcie globalnych kluczy już to pokryło.
            // Jeśli invalidateAll jest false, a dokonano modyfikacji (np. departmentId nie jest null),
            // globalne klucze i tak zostały usunięte powyżej, co jest pożądane,
            // gdyż każda modyfikacja działu może wpłynąć na listy wszystkich lub głównych działów.
        }

        private async Task SaveOperationHistoryAsync(OperationHistory operation)
        {
            if (string.IsNullOrEmpty(operation.Id)) operation.Id = Guid.NewGuid().ToString(); //
            if (string.IsNullOrEmpty(operation.CreatedBy)) //
                operation.CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system_log_save"; //

            if (operation.StartedAt == default(DateTime) &&
                (operation.Status == OperationStatus.InProgress || operation.Status == OperationStatus.Pending || operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed))
            {
                if (operation.StartedAt == default(DateTime)) operation.StartedAt = DateTime.UtcNow; //
                if (operation.Status == OperationStatus.Completed || operation.Status == OperationStatus.Failed || operation.Status == OperationStatus.Cancelled || operation.Status == OperationStatus.PartialSuccess)
                {
                    if (!operation.CompletedAt.HasValue) operation.CompletedAt = DateTime.UtcNow; //
                    if (!operation.Duration.HasValue && operation.CompletedAt.HasValue) operation.Duration = operation.CompletedAt.Value - operation.StartedAt; //
                }
            }

            await _operationHistoryRepository.AddAsync(operation); //
            _logger.LogDebug("Zapisano nowy wpis historii operacji ID: {OperationId} dla działu.", operation.Id); //
        }

        private async Task<bool> IsDescendantAsync(string potentialAncestorId, string departmentIdToCheck)
        {
            var department = await _departmentRepository.GetByIdAsync(departmentIdToCheck); //
            if (department?.ParentDepartmentId == null) //
                return false;

            if (department.ParentDepartmentId == potentialAncestorId) //
                return true;

            return await IsDescendantAsync(potentialAncestorId, department.ParentDepartmentId); //
        }
    }
}