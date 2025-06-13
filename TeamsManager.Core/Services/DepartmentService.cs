using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
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
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<DepartmentService> _logger;
        private readonly IPowerShellCacheService _powerShellCacheService;

        // Klucze cache
        private const string AllDepartmentsRootOnlyCacheKey = "Departments_AllActive_RootOnly";
        private const string AllDepartmentsAllCacheKey = "Departments_AllActive_All";
        private const string DepartmentByIdCacheKeyPrefix = "Department_Id_";
        private const string SubDepartmentsByParentIdCacheKeyPrefix = "Department_Sub_ParentId_";
        private const string UsersInDepartmentCacheKeyPrefix = "Department_UsersIn_Id_";

        /// <summary>
        /// Konstruktor serwisu działów.
        /// </summary>
        public DepartmentService(
            IGenericRepository<Department> departmentRepository,
            IUserRepository userRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<DepartmentService> logger,
            IPowerShellCacheService powerShellCacheService)
        {
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments, bool includeUsers, bool forceRefresh)
        {
            _logger.LogInformation("Pobieranie działu {DepartmentId}. Poddziały: {IncludeSubDepartments}, Użytkownicy: {IncludeUsers}, Wymuszenie odświeżenia: {ForceRefresh}", departmentId, includeSubDepartments, includeUsers, forceRefresh); //
            string cacheKey = DepartmentByIdCacheKeyPrefix + departmentId; //

            if (!forceRefresh && _powerShellCacheService.TryGetValue<Department>(cacheKey, out Department? cachedDepartment) && cachedDepartment != null)
            {
                _logger.LogDebug("Dział {DepartmentId} znaleziony w cache.", departmentId); //
                // Jeśli dział jest w cache, ale potrzebujemy dodatkowych danych, dociągnij je
                if (includeSubDepartments && (cachedDepartment.SubDepartments == null || !cachedDepartment.SubDepartments.Any()))
                {
                    cachedDepartment.SubDepartments = (await GetSubDepartmentsAsync(departmentId, forceRefresh)).ToList(); //
                }
                if (includeUsers && (cachedDepartment.Users == null || !cachedDepartment.Users.Any()))
                {
                    cachedDepartment.Users = (await GetUsersInDepartmentAsync(departmentId, forceRefresh)).ToList(); //
                }
                return cachedDepartment;
            }

            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department != null)
            {
                _powerShellCacheService.Set(cacheKey, department);
                _logger.LogDebug("Dział {DepartmentId} zapisany w cache.", departmentId); //
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

            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<Department>>(cacheKey, out IEnumerable<Department>? cachedDepartments) && cachedDepartments != null)
            {
                _logger.LogDebug("Lista działów (OnlyRoot={OnlyRoot}) znaleziona w cache.", onlyRootDepartments); //
                return cachedDepartments;
            }

            var departments = await _departmentRepository.FindAsync(d => d.IsActive && (onlyRootDepartments ? d.ParentDepartmentId == null : true));
            _powerShellCacheService.Set(cacheKey, departments);
            _logger.LogDebug("Lista działów (OnlyRoot={OnlyRoot}) zapisana w cache. Znaleziono {Count} działów.", onlyRootDepartments, departments.Count()); //
            return departments;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie poddziałów dla działu {ParentDepartmentId}. Wymuszenie odświeżenia: {ForceRefresh}", parentDepartmentId, forceRefresh); //
            string cacheKey = SubDepartmentsByParentIdCacheKeyPrefix + parentDepartmentId; //

            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<Department>>(cacheKey, out IEnumerable<Department>? cachedSubDepartments) && cachedSubDepartments != null)
            {
                _logger.LogDebug("Poddziały dla działu {ParentDepartmentId} znalezione w cache.", parentDepartmentId); //
                return cachedSubDepartments;
            }

            var subDepartments = await _departmentRepository.FindAsync(d => d.ParentDepartmentId == parentDepartmentId && d.IsActive);
            _powerShellCacheService.Set(cacheKey, subDepartments);
            _logger.LogDebug("Poddziały dla działu {ParentDepartmentId} zapisane w cache. Znaleziono {Count} poddziałów.", parentDepartmentId, subDepartments.Count()); //
            return subDepartments;
        }

        /// <inheritdoc />
        /// <remarks>Ta metoda wykorzystuje cache. Użyj forceRefresh = true, aby pominąć cache.</remarks>
        public async Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId, bool forceRefresh = false)
        {
            _logger.LogInformation("Pobieranie użytkowników dla działu {DepartmentId}. Wymuszenie odświeżenia: {ForceRefresh}", departmentId, forceRefresh); //
            string cacheKey = UsersInDepartmentCacheKeyPrefix + departmentId; //

            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<User>>(cacheKey, out IEnumerable<User>? cachedUsers) && cachedUsers != null)
            {
                _logger.LogDebug("Użytkownicy dla działu {DepartmentId} znalezieni w cache.", departmentId); //
                return cachedUsers;
            }

            var users = await _userRepository.FindAsync(u => u.DepartmentId == departmentId && u.IsActive);
            _powerShellCacheService.Set(cacheKey, users);
            _logger.LogDebug("Użytkownicy dla działu {DepartmentId} zapisani w cache. Znaleziono {Count} użytkowników.", departmentId, users.Count()); //
            return users;
        }

        /// <inheritdoc />
        public async Task<Department?> CreateDepartmentAsync(
            string name,
            string description,
            string? parentDepartmentId = null,
            string? departmentCode = null)
        {
            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie tworzenia działu: '{DepartmentName}'", name);

            if (string.IsNullOrWhiteSpace(name))
            {
                var message = "Nazwa działu nie może być pusta.";
                _logger.LogError("Nie można utworzyć działu: {ErrorReason}", message);
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    message,
                    "error"
                );
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

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można utworzyć działu: dział nadrzędny nie istnieje lub jest nieaktywny",
                            "error"
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
                    CreatedBy = currentUserUpn,
                    IsActive = true
                };

                await _departmentRepository.AddAsync(newDepartment);

                _logger.LogInformation("Dział '{DepartmentName}' pomyślnie przygotowany do zapisu. ID: {DepartmentId}", name, newDepartment.Id);

                // Zapisz zmiany do bazy danych
                await _departmentRepository.SaveChangesAsync();

                // Invaliduj cache IMemoryCache
                _powerShellCacheService.InvalidateAllDepartmentLists();

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Dział '{name}' utworzony pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Dział '{name}' został utworzony",
                    "success"
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

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Nie udało się utworzyć działu: {ex.Message}",
                    "error"
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

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
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

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować działu: nie istnieje w systemie",
                        "error"
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

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować działu: nazwa nie może być pusta",
                        "error"
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

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Dział nie może być swoim własnym rodzicem",
                            "error"
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

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Dział nadrzędny nie istnieje lub jest nieaktywny",
                            "error"
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

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można ustawić tego działu jako rodzica - spowodowałoby to cykliczną zależność",
                            "error"
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
                existingDepartment.MarkAsModified(currentUserUpn);

                _departmentRepository.Update(existingDepartment);

                _powerShellCacheService.InvalidateDepartment(existingDepartment.Id);

                // Zapisz zmiany do bazy danych
                await _departmentRepository.SaveChangesAsync();

                // Invaliduj cache IMemoryCache
                _powerShellCacheService.InvalidateAllDepartmentLists();

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Dział '{existingDepartment.Name}' zaktualizowany pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Dział '{existingDepartment.Name}' został zaktualizowany",
                    "success"
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

                // 4. Powiadomienie o błędzie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Błąd podczas aktualizacji działu: {ex.Message}",
                    "error"
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

                var subDepartments = await GetSubDepartmentsAsync(departmentId, false);
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

                // Zapisz zmiany do bazy danych
                await _departmentRepository.SaveChangesAsync();

                // Invaliduj cache IMemoryCache
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
        public async Task RefreshCacheAsync()
        {
            _powerShellCacheService.InvalidateAllCache();
            _logger.LogInformation("Cache działów został odświeżony.");
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

        #region Wygodne przeciążenia metod

        /// <inheritdoc />
        public Task<Department?> GetDepartmentByIdAsync(string departmentId)
            => GetDepartmentByIdAsync(departmentId, false, false, false);

        /// <inheritdoc />
        public Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments)
            => GetDepartmentByIdAsync(departmentId, includeSubDepartments, false, false);

        /// <inheritdoc />
        public Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments, bool includeUsers)
            => GetDepartmentByIdAsync(departmentId, includeSubDepartments, includeUsers, false);

        /// <inheritdoc />
        public Task<IEnumerable<Department>> GetAllDepartmentsAsync()
            => GetAllDepartmentsAsync(false, false);

        /// <inheritdoc />
        public Task<IEnumerable<Department>> GetAllDepartmentsAsync(bool onlyRootDepartments)
            => GetAllDepartmentsAsync(onlyRootDepartments, false);

        /// <inheritdoc />
        public Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId)
            => GetSubDepartmentsAsync(parentDepartmentId, false);

        /// <inheritdoc />
        public Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId)
            => GetUsersInDepartmentAsync(departmentId, false);

        #endregion
    }
}