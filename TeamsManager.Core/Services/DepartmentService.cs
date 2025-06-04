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
        private readonly IPowerShellCacheService _powerShellCacheService;
        private readonly IOperationHistoryService _operationHistoryService;

        // Klucze cache
        private const string AllDepartmentsRootOnlyCacheKey = "Departments_AllActive_RootOnly";
        private const string AllDepartmentsAllCacheKey = "Departments_AllActive_All";
        private const string DepartmentByIdCacheKeyPrefix = "Department_Id_";
        private const string SubDepartmentsByParentIdCacheKeyPrefix = "Department_Sub_ParentId_";
        private const string UsersInDepartmentCacheKeyPrefix = "Department_UsersIn_Id_";
        private readonly TimeSpan _defaultCacheDuration = TimeSpan.FromMinutes(30);

        /// <summary>
        /// Konstruktor serwisu działów.
        /// </summary>
        public DepartmentService(
            IGenericRepository<Department> departmentRepository,
            IUserRepository userRepository,
            IOperationHistoryRepository operationHistoryRepository,
            ICurrentUserService currentUserService,
            ILogger<DepartmentService> logger,
            IMemoryCache memoryCache,
            IPowerShellCacheService powerShellCacheService,
            IOperationHistoryService operationHistoryService)
        {
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryRepository = operationHistoryRepository ?? throw new ArgumentNullException(nameof(operationHistoryRepository));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _cache = memoryCache ?? throw new ArgumentNullException(nameof(memoryCache));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
        }

        private MemoryCacheEntryOptions GetDefaultCacheEntryOptions()
        {
            return _powerShellCacheService.GetDefaultCacheEntryOptions();
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
            _logger.LogInformation("Rozpoczynanie tworzenia działu: '{DepartmentName}'", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                var message = "Nazwa działu nie może być pusta.";
                _logger.LogError("Nie można utworzyć działu: {ErrorReason}", message);
                throw new ArgumentException(message, nameof(name));
            }

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.DepartmentCreated,
                nameof(Department),
                targetEntityName: name
            );

            try
            {
                Department? parentDepartment = null;
                if (!string.IsNullOrEmpty(parentDepartmentId))
                {
                    parentDepartment = await _departmentRepository.GetByIdAsync(parentDepartmentId);
                    if (parentDepartment == null || !parentDepartment.IsActive)
                    {
                        _logger.LogWarning("Nie można utworzyć działu: Dział nadrzędny o ID {ParentDepartmentId} nie istnieje lub jest nieaktywny.", parentDepartmentId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Dział nadrzędny o ID '{parentDepartmentId}' nie istnieje lub jest nieaktywny."
                        );
                        return null;
                    }
                }

                var newDepartment = new Department
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = name,
                    Description = description,
                    ParentDepartmentId = parentDepartmentId,
                    ParentDepartment = parentDepartment,
                    DepartmentCode = departmentCode,
                    CreatedBy = _currentUserService.GetCurrentUserUpn() ?? "system",
                    IsActive = true
                };

                await _departmentRepository.AddAsync(newDepartment);

                _logger.LogInformation("Dział '{DepartmentName}' pomyślnie przygotowany do zapisu. ID: {DepartmentId}", name, newDepartment.Id);

                _powerShellCacheService.InvalidateAllDepartmentLists();
                if (!string.IsNullOrEmpty(newDepartment.ParentDepartmentId))
                {
                    _powerShellCacheService.InvalidateSubDepartments(newDepartment.ParentDepartmentId);
                }

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Dział ID: {newDepartment.Id} przygotowany do utworzenia."
                );
                return newDepartment;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia działu {DepartmentName}. Wiadomość: {ErrorMessage}", name, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                throw;
            }
        }

        /// <inheritdoc />
        public async Task<bool> UpdateDepartmentAsync(Department departmentToUpdate)
        {
            if (departmentToUpdate == null || string.IsNullOrEmpty(departmentToUpdate.Id))
            {
                _logger.LogError("Próba aktualizacji działu z nieprawidłowymi danymi (null lub brak ID).");
                throw new ArgumentNullException(nameof(departmentToUpdate), "Obiekt działu lub jego ID nie może być null/pusty.");
            }

            _logger.LogInformation("Aktualizowanie działu ID: {DepartmentId}", departmentToUpdate.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.DepartmentUpdated,
                nameof(Department),
                targetEntityId: departmentToUpdate.Id,
                targetEntityName: departmentToUpdate.Name
            );

            try
            {
                string? oldParentId = null;

                var existingDepartment = await _departmentRepository.GetByIdAsync(departmentToUpdate.Id);
                if (existingDepartment == null)
                {
                    _logger.LogWarning("Nie można zaktualizować działu ID {DepartmentId} - nie istnieje.", departmentToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Dział nie istnieje."
                    );
                    return false;
                }

                oldParentId = existingDepartment.ParentDepartmentId;

                if (string.IsNullOrWhiteSpace(departmentToUpdate.Name))
                {
                    _logger.LogError("Błąd walidacji przy aktualizacji działu {DepartmentId}: Nazwa pusta.", departmentToUpdate.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa działu nie może być pusta."
                    );
                    return false;
                }

                if (!string.IsNullOrEmpty(departmentToUpdate.ParentDepartmentId))
                {
                    if (departmentToUpdate.ParentDepartmentId == departmentToUpdate.Id)
                    {
                        _logger.LogWarning("Próba ustawienia działu {DepartmentId} jako swojego własnego rodzica.", departmentToUpdate.Id);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            "Dział nie może być swoim własnym rodzicem."
                        );
                        return false;
                    }

                    var parentDepartment = await _departmentRepository.GetByIdAsync(departmentToUpdate.ParentDepartmentId);
                    if (parentDepartment == null || !parentDepartment.IsActive)
                    {
                        _logger.LogWarning("Dział nadrzędny {ParentDepartmentId} nie istnieje lub jest nieaktywny.", departmentToUpdate.ParentDepartmentId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Dział nadrzędny o ID '{departmentToUpdate.ParentDepartmentId}' nie istnieje lub jest nieaktywny."
                        );
                        return false;
                    }

                    if (await IsDescendantAsync(departmentToUpdate.ParentDepartmentId, departmentToUpdate.Id))
                    {
                        _logger.LogWarning("Próba utworzenia cyklicznej zależności między działami {DepartmentId} i {ParentDepartmentId}.",
                            departmentToUpdate.Id, departmentToUpdate.ParentDepartmentId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            "Nie można ustawić działu jako rodzica, ponieważ spowodowałoby to cykliczną zależność."
                        );
                        return false;
                    }
                    existingDepartment.ParentDepartment = parentDepartment;
                }
                else
                {
                    existingDepartment.ParentDepartment = null;
                }

                existingDepartment.Name = departmentToUpdate.Name;
                existingDepartment.Description = departmentToUpdate.Description;
                existingDepartment.ParentDepartmentId = departmentToUpdate.ParentDepartmentId;
                existingDepartment.DepartmentCode = departmentToUpdate.DepartmentCode;
                existingDepartment.Email = departmentToUpdate.Email;
                existingDepartment.Phone = departmentToUpdate.Phone;
                existingDepartment.Location = departmentToUpdate.Location;
                existingDepartment.SortOrder = departmentToUpdate.SortOrder;
                existingDepartment.IsActive = departmentToUpdate.IsActive;
                existingDepartment.MarkAsModified(_currentUserService.GetCurrentUserUpn() ?? "system");

                _departmentRepository.Update(existingDepartment);

                _powerShellCacheService.InvalidateDepartment(existingDepartment.Id);
                _powerShellCacheService.InvalidateAllDepartmentLists();

                if (!string.IsNullOrEmpty(oldParentId))
                {
                    _powerShellCacheService.InvalidateSubDepartments(oldParentId);
                }

                if (!string.IsNullOrEmpty(existingDepartment.ParentDepartmentId) && 
                    existingDepartment.ParentDepartmentId != oldParentId)
                {
                    _powerShellCacheService.InvalidateSubDepartments(existingDepartment.ParentDepartmentId);
                }

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Dział przygotowany do aktualizacji."
                );
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji działu ID {DepartmentId}. Wiadomość: {ErrorMessage}", departmentToUpdate.Id, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Błąd: {ex.Message}",
                    ex.StackTrace
                );
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<bool> DeleteDepartmentAsync(string departmentId)
        {
            _logger.LogInformation("Usuwanie działu ID: {DepartmentId}", departmentId);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.DepartmentDeleted,
                nameof(Department),
                targetEntityId: departmentId
            );

            try
            {
                var department = await _departmentRepository.GetByIdAsync(departmentId);
                if (department == null)
                {
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - nie istnieje.", departmentId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Dział nie istnieje."
                    );
                    return false;
                }

                if (!department.IsActive)
                {
                    _logger.LogInformation("Dział ID {DepartmentId} był już nieaktywny.", departmentId);
                    _powerShellCacheService.InvalidateDepartment(departmentId);
                    _powerShellCacheService.InvalidateAllDepartmentLists();

                    if (!string.IsNullOrEmpty(department.ParentDepartmentId))
                    {
                        _powerShellCacheService.InvalidateSubDepartments(department.ParentDepartmentId);
                    }
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Completed,
                        $"Dział '{department.Name}' był już nieaktywny."
                    );
                    return true;
                }

                var subDepartments = await GetSubDepartmentsAsync(departmentId);
                if (subDepartments.Any())
                {
                    var message = "Nie można usunąć działu, ponieważ ma przypisane aktywne poddziały.";
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - ma aktywne poddziały.", departmentId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        message
                    );
                    throw new InvalidOperationException(message);
                }

                var usersInDepartment = await _userRepository.FindAsync(u => u.DepartmentId == departmentId && u.IsActive);
                if (usersInDepartment.Any())
                {
                    var message = "Nie można usunąć działu, ponieważ ma przypisanych aktywnych użytkowników.";
                    _logger.LogWarning("Nie można usunąć działu ID {DepartmentId} - ma aktywnych użytkowników.", departmentId);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        message
                    );
                    throw new InvalidOperationException(message);
                }

                department.MarkAsDeleted(_currentUserService.GetCurrentUserUpn() ?? "system");
                _departmentRepository.Update(department);

                _powerShellCacheService.InvalidateDepartment(departmentId);
                _powerShellCacheService.InvalidateAllDepartmentLists();

                if (!string.IsNullOrEmpty(department.ParentDepartmentId))
                {
                    _powerShellCacheService.InvalidateSubDepartments(department.ParentDepartmentId);
                }

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    "Dział oznaczony jako usunięty."
                );
                return true;
            }
            catch (Exception ex) when (!(ex is InvalidOperationException))
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania działu ID {DepartmentId}. Wiadomość: {ErrorMessage}", departmentId, ex.Message);
                
                // 3. Aktualizacja statusu na błąd w przypadku nieoczekiwanego wyjątku
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Failed,
                    $"Krytyczny błąd: {ex.Message}",
                    ex.StackTrace
                );
                throw;
            }
            catch (InvalidOperationException)
            {
                // Wyjątki walidacyjne już zostały obsłużone i zaktualizowane w historii
                throw;
            }
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda unieważnia globalny cache dla działów.</remarks>
        public Task RefreshCacheAsync()
        {
            _logger.LogInformation("Rozpoczynanie odświeżania całego cache'a działów.");
            _powerShellCacheService.InvalidateAllCache();
            _logger.LogInformation("Cache działów został zresetowany. Wpisy zostaną odświeżone przy następnym żądaniu.");
            return Task.CompletedTask;
        }

        private async Task<bool> IsDescendantAsync(string potentialAncestorId, string departmentIdToCheck)
        {
            var department = await _departmentRepository.GetByIdAsync(departmentIdToCheck);
            if (department?.ParentDepartmentId == null)
                return false;

            if (department.ParentDepartmentId == potentialAncestorId)
                return true;

            return await IsDescendantAsync(potentialAncestorId, department.ParentDepartmentId);
        }
    }
}