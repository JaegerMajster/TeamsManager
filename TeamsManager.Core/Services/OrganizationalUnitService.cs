using System;
using System.Collections.Generic;
using System.Linq;
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
    /// Serwis odpowiedzialny za logikę biznesową jednostek organizacyjnych.
    /// Implementuje cache'owanie dla często odpytywanych danych.
    /// </summary>
    public class OrganizationalUnitService : IOrganizationalUnitService
    {
        private readonly IGenericRepository<OrganizationalUnit> _organizationalUnitRepository;
        private readonly IGenericRepository<Department> _departmentRepository;
        private readonly IOperationHistoryService _operationHistoryService;
        private readonly INotificationService _notificationService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<OrganizationalUnitService> _logger;
        private readonly IPowerShellCacheService _powerShellCacheService;

        // Klucze cache
        private const string AllOrganizationalUnitsRootOnlyCacheKey = "OrganizationalUnits_AllActive_RootOnly";
        private const string AllOrganizationalUnitsAllCacheKey = "OrganizationalUnits_AllActive_All";
        private const string OrganizationalUnitByIdCacheKeyPrefix = "OrganizationalUnit_Id_";
        private const string SubUnitsByParentIdCacheKeyPrefix = "OrganizationalUnit_Sub_ParentId_";
        private const string DepartmentsByUnitIdCacheKeyPrefix = "OrganizationalUnit_Departments_Id_";
        private const string OrganizationalUnitsHierarchyCacheKey = "OrganizationalUnits_Hierarchy";

        public OrganizationalUnitService(
            IGenericRepository<OrganizationalUnit> organizationalUnitRepository,
            IGenericRepository<Department> departmentRepository,
            IOperationHistoryService operationHistoryService,
            INotificationService notificationService,
            ICurrentUserService currentUserService,
            ILogger<OrganizationalUnitService> logger,
            IPowerShellCacheService powerShellCacheService)
        {
            _organizationalUnitRepository = organizationalUnitRepository ?? throw new ArgumentNullException(nameof(organizationalUnitRepository));
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _operationHistoryService = operationHistoryService ?? throw new ArgumentNullException(nameof(operationHistoryService));
            _notificationService = notificationService ?? throw new ArgumentNullException(nameof(notificationService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _powerShellCacheService = powerShellCacheService ?? throw new ArgumentNullException(nameof(powerShellCacheService));
        }

        public async Task<OrganizationalUnit?> GetOrganizationalUnitByIdAsync(string unitId, bool includeSubUnits = false, bool includeDepartments = false, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                _logger.LogWarning("Próba pobrania jednostki organizacyjnej z pustym ID");
                return null;
            }

            var cacheKey = $"{OrganizationalUnitByIdCacheKeyPrefix}{unitId}_{includeSubUnits}_{includeDepartments}";

            if (!forceRefresh && _powerShellCacheService.TryGetValue<OrganizationalUnit>(cacheKey, out OrganizationalUnit? cachedUnit))
            {
                _logger.LogDebug("Pobrano jednostkę organizacyjną {UnitId} z cache", unitId);
                return cachedUnit;
            }

            try
            {
                var unit = await _organizationalUnitRepository.GetByIdAsync(unitId);

                if (unit != null && unit.IsActive)
                {
                    _powerShellCacheService.Set(cacheKey, unit);
                    _logger.LogDebug("Pobrano jednostkę organizacyjną {UnitId} z bazy danych", unitId);

                    // Załaduj powiązane dane jeśli potrzebne
                    if (includeSubUnits)
                    {
                        var subUnits = await GetSubUnitsAsync(unitId, forceRefresh);
                        unit.SubUnits = subUnits.ToList();
                        _logger.LogDebug("Załadowano {Count} podjednostek dla jednostki {UnitId}", unit.SubUnits.Count, unitId);
                    }

                    if (includeDepartments)
                    {
                        var departments = await GetDepartmentsByOrganizationalUnitAsync(unitId, forceRefresh);
                        unit.Departments = departments.ToList();
                        _logger.LogDebug("Załadowano {Count} działów dla jednostki {UnitId}", unit.Departments.Count, unitId);
                    }
                }
                else
                {
                    _logger.LogWarning("Nie znaleziono aktywnej jednostki organizacyjnej o ID: {UnitId}", unitId);
                    unit = null;
                }

                return unit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania jednostki organizacyjnej {UnitId}", unitId);
                throw;
            }
        }

        public async Task<IEnumerable<OrganizationalUnit>> GetAllOrganizationalUnitsAsync(bool onlyRootUnits = false, bool forceRefresh = false)
        {
            var cacheKey = onlyRootUnits ? AllOrganizationalUnitsRootOnlyCacheKey : AllOrganizationalUnitsAllCacheKey;

            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<OrganizationalUnit>>(cacheKey, out IEnumerable<OrganizationalUnit>? cachedUnits))
            {
                _logger.LogDebug("Pobrano jednostki organizacyjne z cache (onlyRoot: {OnlyRoot})", onlyRootUnits);
                return cachedUnits!;
            }

            try
            {
                IEnumerable<OrganizationalUnit> units;
                
                if (onlyRootUnits)
                {
                    units = await _organizationalUnitRepository.FindAsync(ou => ou.IsActive && ou.ParentUnitId == null);
                }
                else
                {
                    units = await _organizationalUnitRepository.FindAsync(ou => ou.IsActive);
                }

                // Sortuj wyniki
                units = units.OrderBy(ou => ou.SortOrder).ThenBy(ou => ou.Name).ToList();

                _powerShellCacheService.Set(cacheKey, units);
                _logger.LogDebug("Pobrano {Count} jednostek organizacyjnych z bazy danych (onlyRoot: {OnlyRoot})", units.Count(), onlyRootUnits);

                return units;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania jednostek organizacyjnych (onlyRoot: {OnlyRoot})", onlyRootUnits);
                throw;
            }
        }

        public async Task<IEnumerable<OrganizationalUnit>> GetSubUnitsAsync(string parentUnitId, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(parentUnitId))
            {
                _logger.LogWarning("Próba pobrania podjednostek z pustym ID jednostki nadrzędnej");
                return Enumerable.Empty<OrganizationalUnit>();
            }

            var cacheKey = $"{SubUnitsByParentIdCacheKeyPrefix}{parentUnitId}";

            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<OrganizationalUnit>>(cacheKey, out IEnumerable<OrganizationalUnit>? cachedSubUnits))
            {
                _logger.LogDebug("Pobrano podjednostki dla {ParentUnitId} z cache", parentUnitId);
                return cachedSubUnits!;
            }

            try
            {
                var subUnits = await _organizationalUnitRepository.FindAsync(ou => ou.ParentUnitId == parentUnitId && ou.IsActive);
                subUnits = subUnits.OrderBy(ou => ou.SortOrder).ThenBy(ou => ou.Name).ToList();

                _powerShellCacheService.Set(cacheKey, subUnits);
                _logger.LogDebug("Pobrano {Count} podjednostek dla {ParentUnitId} z bazy danych", subUnits.Count(), parentUnitId);

                return subUnits;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania podjednostek dla {ParentUnitId}", parentUnitId);
                throw;
            }
        }

        public async Task<IEnumerable<OrganizationalUnit>> GetOrganizationalUnitsHierarchyAsync(bool forceRefresh = false)
        {
            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<OrganizationalUnit>>(OrganizationalUnitsHierarchyCacheKey, out IEnumerable<OrganizationalUnit>? cachedHierarchy))
            {
                _logger.LogDebug("Pobrano hierarchię jednostek organizacyjnych z cache");
                return cachedHierarchy!;
            }

            try
            {
                // Pobierz wszystkie aktywne jednostki
                var allUnits = await _organizationalUnitRepository.FindAsync(ou => ou.IsActive);
                var unitsList = allUnits.OrderBy(ou => ou.SortOrder).ThenBy(ou => ou.Name).ToList();

                // Buduj hierarchię - załaduj podjednostki i działy dla każdej jednostki
                foreach (var unit in unitsList)
                {
                    // Załaduj podjednostki
                    var subUnits = unitsList.Where(u => u.ParentUnitId == unit.Id).ToList();
                    unit.SubUnits = subUnits;

                    // Załaduj działy
                    var departments = await _departmentRepository.FindAsync(d => d.OrganizationalUnitId == unit.Id && d.IsActive);
                    unit.Departments = departments.OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ToList();
                }

                // Zwróć tylko jednostki główne
                var rootUnits = unitsList.Where(ou => ou.ParentUnitId == null).ToList();

                _powerShellCacheService.Set(OrganizationalUnitsHierarchyCacheKey, rootUnits);
                _logger.LogDebug("Pobrano hierarchię {Count} jednostek organizacyjnych z bazy danych", rootUnits.Count);

                return rootUnits;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania hierarchii jednostek organizacyjnych");
                throw;
            }
        }

        public async Task<OrganizationalUnit> CreateOrganizationalUnitAsync(OrganizationalUnit unit)
        {
            if (unit == null)
                throw new ArgumentNullException(nameof(unit));

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie tworzenia jednostki organizacyjnej: '{UnitName}'", unit.Name);

            if (string.IsNullOrWhiteSpace(unit.Name))
            {
                var message = "Nazwa jednostki organizacyjnej nie może być pusta.";
                _logger.LogError("Nie można utworzyć jednostki organizacyjnej: {ErrorReason}", message);
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    message,
                    "error"
                );
                throw new ArgumentException(message, nameof(unit.Name));
            }

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.GenericCreated,
                "OrganizationalUnit",
                targetEntityName: unit.Name
            );

            try
            {
                // Sprawdź unikalność nazwy
                var isUnique = await IsNameUniqueAsync(unit.Name, unit.ParentUnitId);
                if (!isUnique)
                {
                    _logger.LogWarning("Nie można utworzyć jednostki organizacyjnej: Nazwa '{UnitName}' już istnieje w tej samej jednostce nadrzędnej.", unit.Name);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Jednostka organizacyjna o nazwie '{unit.Name}' już istnieje w tej samej jednostce nadrzędnej."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można utworzyć jednostki organizacyjnej: nazwa '{unit.Name}' już istnieje w tej samej jednostce nadrzędnej",
                        "error"
                    );
                    throw new InvalidOperationException($"Jednostka organizacyjna o nazwie '{unit.Name}' już istnieje w tej samej jednostce nadrzędnej.");
                }

                // Sprawdź czy jednostka nadrzędna istnieje (jeśli podano)
                if (!string.IsNullOrEmpty(unit.ParentUnitId))
                {
                    var parentUnit = await _organizationalUnitRepository.GetByIdAsync(unit.ParentUnitId);
                    if (parentUnit == null || !parentUnit.IsActive)
                    {
                        _logger.LogWarning("Nie można utworzyć jednostki organizacyjnej: Jednostka nadrzędna o ID {ParentUnitId} nie istnieje lub jest nieaktywna.", unit.ParentUnitId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Jednostka nadrzędna o ID '{unit.ParentUnitId}' nie istnieje lub jest nieaktywna."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można utworzyć jednostki organizacyjnej: jednostka nadrzędna nie istnieje lub jest nieaktywna",
                            "error"
                        );
                        throw new InvalidOperationException($"Jednostka nadrzędna o ID '{unit.ParentUnitId}' nie istnieje lub jest nieaktywna.");
                    }
                }

                // Ustaw ID jeśli nie został podany
                if (string.IsNullOrEmpty(unit.Id))
                {
                    unit.Id = Guid.NewGuid().ToString();
                }

                unit.IsActive = true;
                unit.CreatedBy = currentUserUpn;

                await _organizationalUnitRepository.AddAsync(unit);

                _logger.LogInformation("Jednostka organizacyjna '{UnitName}' pomyślnie przygotowana do zapisu. ID: {UnitId}", unit.Name, unit.Id);

                // Zapisz zmiany do bazy danych (natychmiastowy zapis)
                await _organizationalUnitRepository.SaveChangesAsync();
                
                // Wyczyść cache
                InvalidateCache(invalidateAll: true);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Jednostka organizacyjna '{unit.Name}' utworzona pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Jednostka organizacyjna '{unit.Name}' została utworzona",
                    "success"
                );

                _logger.LogInformation("Utworzono jednostkę organizacyjną {UnitName} (ID: {UnitId})", unit.Name, unit.Id);

                return unit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas tworzenia jednostki organizacyjnej {UnitName}. Wiadomość: {ErrorMessage}", unit.Name, ex.Message);
                
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
                    $"Nie udało się utworzyć jednostki organizacyjnej: {ex.Message}",
                    "error"
                );

                throw;
            }
        }

        public async Task<OrganizationalUnit> UpdateOrganizationalUnitAsync(OrganizationalUnit unit)
        {
            if (unit == null || string.IsNullOrEmpty(unit.Id))
                throw new ArgumentNullException(nameof(unit), "Obiekt jednostki organizacyjnej lub jego ID nie może być null/pusty.");

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie aktualizacji jednostki organizacyjnej ID: {UnitId}", unit.Id);

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.GenericUpdated,
                "OrganizationalUnit",
                targetEntityId: unit.Id,
                targetEntityName: unit.Name
            );

            try
            {
                // Sprawdź czy jednostka istnieje
                var existingUnit = await _organizationalUnitRepository.GetByIdAsync(unit.Id);
                if (existingUnit == null)
                {
                    _logger.LogWarning("Nie można zaktualizować jednostki organizacyjnej ID {UnitId} - nie istnieje.", unit.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Jednostka organizacyjna nie istnieje."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować jednostki organizacyjnej: nie istnieje w systemie",
                        "error"
                    );
                    throw new InvalidOperationException($"Jednostka organizacyjna o ID '{unit.Id}' nie istnieje.");
                }

                if (!existingUnit.IsActive)
                {
                    _logger.LogWarning("Nie można zaktualizować jednostki organizacyjnej ID {UnitId} - jest nieaktywna.", unit.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Jednostka organizacyjna jest nieaktywna."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować jednostki organizacyjnej: jest nieaktywna",
                        "error"
                    );
                    throw new InvalidOperationException($"Jednostka organizacyjna o ID '{unit.Id}' jest nieaktywna.");
                }

                if (string.IsNullOrWhiteSpace(unit.Name))
                {
                    _logger.LogError("Błąd walidacji przy aktualizacji jednostki organizacyjnej {UnitId}: Nazwa pusta.", unit.Id);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        "Nazwa jednostki organizacyjnej nie może być pusta."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        "Nie można zaktualizować jednostki organizacyjnej: nazwa nie może być pusta",
                        "error"
                    );
                    throw new ArgumentException("Nazwa jednostki organizacyjnej nie może być pusta.", nameof(unit.Name));
                }

                // Sprawdź unikalność nazwy (wykluczając aktualną jednostkę)
                var isUnique = await IsNameUniqueAsync(unit.Name, unit.ParentUnitId, unit.Id);
                if (!isUnique)
                {
                    _logger.LogWarning("Nie można zaktualizować jednostki organizacyjnej: Nazwa '{UnitName}' już istnieje w tej samej jednostce nadrzędnej.", unit.Name);
                    
                    await _operationHistoryService.UpdateOperationStatusAsync(
                        operation.Id,
                        OperationStatus.Failed,
                        $"Jednostka organizacyjna o nazwie '{unit.Name}' już istnieje w tej samej jednostce nadrzędnej."
                    );

                    await _notificationService.SendNotificationToUserAsync(
                        currentUserUpn,
                        $"Nie można zaktualizować jednostki organizacyjnej: nazwa '{unit.Name}' już istnieje w tej samej jednostce nadrzędnej",
                        "error"
                    );
                    throw new InvalidOperationException($"Jednostka organizacyjna o nazwie '{unit.Name}' już istnieje w tej samej jednostce nadrzędnej.");
                }

                // Sprawdź czy można przenieść jednostkę (sprawdź cykle)
                if (unit.ParentUnitId != existingUnit.ParentUnitId)
                {
                    var canMove = await CanMoveUnitAsync(unit.Id, unit.ParentUnitId);
                    if (!canMove)
                    {
                        _logger.LogWarning("Nie można przenieść jednostki organizacyjnej {UnitId} - utworzyłoby to cykl w hierarchii.", unit.Id);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            "Nie można przenieść jednostki organizacyjnej - utworzyłoby to cykl w hierarchii."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można przenieść jednostki organizacyjnej: utworzyłoby to cykl w hierarchii",
                            "error"
                        );
                        throw new InvalidOperationException("Nie można przenieść jednostki organizacyjnej - utworzyłoby to cykl w hierarchii.");
                    }
                }

                // Sprawdź czy jednostka nadrzędna istnieje (jeśli podano)
                if (!string.IsNullOrEmpty(unit.ParentUnitId))
                {
                    var parentUnit = await _organizationalUnitRepository.GetByIdAsync(unit.ParentUnitId);
                    if (parentUnit == null || !parentUnit.IsActive)
                    {
                        _logger.LogWarning("Nie można zaktualizować jednostki organizacyjnej: Jednostka nadrzędna o ID {ParentUnitId} nie istnieje lub jest nieaktywna.", unit.ParentUnitId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            $"Jednostka nadrzędna o ID '{unit.ParentUnitId}' nie istnieje lub jest nieaktywna."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można zaktualizować jednostki organizacyjnej: jednostka nadrzędna nie istnieje lub jest nieaktywna",
                            "error"
                        );
                        throw new InvalidOperationException($"Jednostka nadrzędna o ID '{unit.ParentUnitId}' nie istnieje lub jest nieaktywna.");
                    }
                }

                unit.MarkAsModified(currentUserUpn);

                // Zachowaj istniejące wartości, jeśli nie zostały zmienione
                unit.CreatedBy = existingUnit.CreatedBy;
                unit.CreatedDate = existingUnit.CreatedDate;
                unit.IsActive = existingUnit.IsActive;

                _organizationalUnitRepository.Update(unit);

                _logger.LogInformation("Jednostka organizacyjna '{UnitName}' pomyślnie przygotowana do aktualizacji. ID: {UnitId}", unit.Name, unit.Id);

                // Zapisz zmiany do bazy danych (natychmiastowy zapis)
                await _organizationalUnitRepository.SaveChangesAsync();
                
                // Wyczyść cache
                InvalidateCache(organizationalUnitId: unit.Id, invalidateAll: true);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Jednostka organizacyjna '{unit.Name}' zaktualizowana pomyślnie"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Jednostka organizacyjna '{unit.Name}' została zaktualizowana",
                    "success"
                );

                _logger.LogInformation("Zaktualizowano jednostkę organizacyjną {UnitName} (ID: {UnitId})", unit.Name, unit.Id);

                return unit;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas aktualizacji jednostki organizacyjnej {UnitId}. Wiadomość: {ErrorMessage}", unit.Id, ex.Message);
                
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
                    $"Nie udało się zaktualizować jednostki organizacyjnej: {ex.Message}",
                    "error"
                );

                throw;
            }
        }

        public async Task<bool> DeleteOrganizationalUnitAsync(string unitId, bool forceDelete = false)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                _logger.LogWarning("Próba usunięcia jednostki organizacyjnej z pustym ID");
                return false;
            }

            var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
            _logger.LogInformation("Rozpoczynanie usuwania jednostki organizacyjnej ID: {UnitId} (Force: {ForceDelete})", unitId, forceDelete);

            // Pobierz jednostkę przed utworzeniem operacji, aby mieć nazwę
            var unit = await _organizationalUnitRepository.GetByIdAsync(unitId);
            if (unit == null)
            {
                _logger.LogWarning("Nie znaleziono jednostki organizacyjnej o ID: {UnitId}", unitId);
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nie można usunąć jednostki organizacyjnej: nie istnieje w systemie",
                    "error"
                );
                return false;
            }

            if (!unit.IsActive)
            {
                _logger.LogWarning("Nie można usunąć jednostki organizacyjnej ID {UnitId} - jest już nieaktywna.", unitId);
                
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    "Nie można usunąć jednostki organizacyjnej: jest już nieaktywna",
                    "error"
                );
                return false;
            }

            // 1. Inicjalizacja operacji historii na początku
            var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                OperationType.GenericDeleted,
                "OrganizationalUnit",
                targetEntityId: unitId,
                targetEntityName: unit.Name
            );

            try
            {
                // Sprawdź czy można usunąć
                if (!forceDelete)
                {
                    var canDelete = await CanDeleteOrganizationalUnitAsync(unitId);
                    if (!canDelete)
                    {
                        _logger.LogWarning("Nie można usunąć jednostki organizacyjnej {UnitId} - ma przypisane działy lub podjednostki", unitId);
                        
                        await _operationHistoryService.UpdateOperationStatusAsync(
                            operation.Id,
                            OperationStatus.Failed,
                            "Jednostka organizacyjna ma przypisane działy lub podjednostki."
                        );

                        await _notificationService.SendNotificationToUserAsync(
                            currentUserUpn,
                            "Nie można usunąć jednostki organizacyjnej: ma przypisane działy lub podjednostki",
                            "error"
                        );
                        return false;
                    }
                }

                if (forceDelete)
                {
                    _logger.LogInformation("Wykonywanie wymuszonego usunięcia jednostki organizacyjnej {UnitId} - przenoszenie zależności", unitId);

                    // Przenieś działy do jednostki nadrzędnej lub ustaw na null
                    var departments = await GetDepartmentsByOrganizationalUnitAsync(unitId);
                    foreach (var department in departments)
                    {
                        department.OrganizationalUnitId = unit.ParentUnitId;
                        department.MarkAsModified(currentUserUpn);
                        _departmentRepository.Update(department);
                    }

                    // Przenieś podjednostki do jednostki nadrzędnej
                    var subUnits = await GetSubUnitsAsync(unitId);
                    foreach (var subUnit in subUnits)
                    {
                        subUnit.ParentUnitId = unit.ParentUnitId;
                        subUnit.MarkAsModified(currentUserUpn);
                        _organizationalUnitRepository.Update(subUnit);
                    }

                    _logger.LogInformation("Przeniesiono {DepartmentCount} działów i {SubUnitCount} podjednostek z jednostki {UnitId}", 
                        departments.Count(), subUnits.Count(), unitId);
                }

                // Usuń jednostkę (soft delete)
                unit.MarkAsDeleted(currentUserUpn);
                _organizationalUnitRepository.Update(unit);

                _logger.LogInformation("Jednostka organizacyjna '{UnitName}' pomyślnie przygotowana do usunięcia. ID: {UnitId}", unit.Name, unitId);

                // Zapisz zmiany do bazy danych (natychmiastowy zapis)
                await _organizationalUnitRepository.SaveChangesAsync();
                
                // Wyczyść cache
                InvalidateCache(organizationalUnitId: unitId, invalidateAll: true);

                // 2. Aktualizacja statusu na sukces po pomyślnym wykonaniu logiki
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Jednostka organizacyjna '{unit.Name}' usunięta pomyślnie (Force: {forceDelete})"
                );

                // 3. Powiadomienie o sukcesie
                await _notificationService.SendNotificationToUserAsync(
                    currentUserUpn,
                    $"Jednostka organizacyjna '{unit.Name}' została usunięta",
                    "success"
                );

                _logger.LogInformation("Usunięto jednostkę organizacyjną {UnitName} (ID: {UnitId}, Force: {ForceDelete})", unit.Name, unitId, forceDelete);

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Krytyczny błąd podczas usuwania jednostki organizacyjnej {UnitId}. Wiadomość: {ErrorMessage}", unitId, ex.Message);
                
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
                    $"Nie udało się usunąć jednostki organizacyjnej: {ex.Message}",
                    "error"
                );

                throw;
            }
        }

        public async Task<bool> CanDeleteOrganizationalUnitAsync(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return false;

            try
            {
                // Sprawdź czy ma podjednostki
                var subUnits = await _organizationalUnitRepository.FindAsync(ou => ou.ParentUnitId == unitId && ou.IsActive);
                if (subUnits.Any())
                    return false;

                // Sprawdź czy ma przypisane działy
                var departments = await _departmentRepository.FindAsync(d => d.OrganizationalUnitId == unitId && d.IsActive);
                return !departments.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania możliwości usunięcia jednostki organizacyjnej {UnitId}", unitId);
                return false;
            }
        }

        public async Task<IEnumerable<Department>> GetDepartmentsByOrganizationalUnitAsync(string unitId, bool forceRefresh = false)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                _logger.LogWarning("Próba pobrania działów z pustym ID jednostki organizacyjnej");
                return Enumerable.Empty<Department>();
            }

            var cacheKey = $"{DepartmentsByUnitIdCacheKeyPrefix}{unitId}";

            if (!forceRefresh && _powerShellCacheService.TryGetValue<IEnumerable<Department>>(cacheKey, out IEnumerable<Department>? cachedDepartments))
            {
                _logger.LogDebug("Pobrano działy dla jednostki {UnitId} z cache", unitId);
                return cachedDepartments!;
            }

            try
            {
                var departments = await _departmentRepository.FindAsync(d => d.OrganizationalUnitId == unitId && d.IsActive);
                departments = departments.OrderBy(d => d.SortOrder).ThenBy(d => d.Name).ToList();

                _powerShellCacheService.Set(cacheKey, departments);
                _logger.LogDebug("Pobrano {Count} działów dla jednostki {UnitId} z bazy danych", departments.Count(), unitId);

                return departments;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania działów dla jednostki {UnitId}", unitId);
                throw;
            }
        }

        public async Task<int> MoveDepartmentsToOrganizationalUnitAsync(IEnumerable<string> departmentIds, string targetUnitId)
        {
            if (departmentIds == null || !departmentIds.Any())
            {
                _logger.LogWarning("Próba przeniesienia działów z pustą listą ID");
                return 0;
            }

            if (string.IsNullOrWhiteSpace(targetUnitId))
            {
                _logger.LogWarning("Próba przeniesienia działów z pustym ID docelowej jednostki");
                return 0;
            }

            try
            {
                // Sprawdź czy docelowa jednostka istnieje
                var targetUnit = await _organizationalUnitRepository.GetByIdAsync(targetUnitId);
                if (targetUnit == null || !targetUnit.IsActive)
                {
                    throw new InvalidOperationException($"Docelowa jednostka organizacyjna o ID '{targetUnitId}' nie istnieje lub jest nieaktywna.");
                }

                var currentUserUpn = _currentUserService.GetCurrentUserUpn() ?? "system";
                int movedCount = 0;
                foreach (var departmentId in departmentIds)
                {
                    var department = await _departmentRepository.GetByIdAsync(departmentId);
                    if (department == null)
                    {
                        _logger.LogWarning("Dział o ID {DepartmentId} nie istnieje - pomijanie", departmentId);
                        continue;
                    }

                    if (!department.IsActive)
                    {
                        _logger.LogWarning("Dział o ID {DepartmentId} jest nieaktywny - pomijanie", departmentId);
                        continue;
                    }

                    if (department.OrganizationalUnitId == targetUnitId)
                    {
                        _logger.LogWarning("Dział o ID {DepartmentId} jest już przypisany do jednostki docelowej - pomijanie", departmentId);
                        continue;
                    }

                        department.OrganizationalUnitId = targetUnitId;
                    department.MarkAsModified(currentUserUpn);
                        _departmentRepository.Update(department);
                        movedCount++;
                }

                await _departmentRepository.SaveChangesAsync();

                // Wyczyść cache
                InvalidateCache(organizationalUnitId: targetUnitId, invalidateAll: true);

                // Zapisz historię operacji
                var operation = await _operationHistoryService.CreateNewOperationEntryAsync(
                    OperationType.GenericUpdated,
                    "Department",
                    string.Join(",", departmentIds),
                    $"Przeniesiono {movedCount} działów do jednostki organizacyjnej {targetUnitId}");
                
                await _operationHistoryService.UpdateOperationStatusAsync(
                    operation.Id,
                    OperationStatus.Completed,
                    $"Przeniesiono {movedCount} działów do jednostki organizacyjnej pomyślnie");

                _logger.LogInformation("Przeniesiono {MovedCount} działów do jednostki organizacyjnej {TargetUnitId}", movedCount, targetUnitId);

                return movedCount;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas przenoszenia działów do jednostki {TargetUnitId}", targetUnitId);
                throw;
            }
        }

        public async Task<bool> IsNameUniqueAsync(string name, string? parentUnitId = null, string? excludeUnitId = null)
        {
            if (string.IsNullOrWhiteSpace(name))
                return false;

            try
            {
                // Sprawdź czy jednostka nadrzędna istnieje i jest aktywna (jeśli podano)
                if (!string.IsNullOrEmpty(parentUnitId))
                {
                    var parentUnit = await _organizationalUnitRepository.GetByIdAsync(parentUnitId);
                    if (parentUnit == null || !parentUnit.IsActive)
                    {
                        _logger.LogWarning("Nie można sprawdzić unikalności nazwy: jednostka nadrzędna {ParentUnitId} nie istnieje lub jest nieaktywna", parentUnitId);
                        return false;
                    }
                }

                // Pobierz jednostki o tej samej nazwie w tej samej jednostce nadrzędnej
                var existingUnits = await _organizationalUnitRepository.FindAsync(ou => 
                    ou.Name.ToLower() == name.ToLower() && 
                    ou.IsActive &&
                    ou.ParentUnitId == parentUnitId);

                // Wyklucz aktualną jednostkę (dla aktualizacji)
                if (!string.IsNullOrEmpty(excludeUnitId))
                {
                    existingUnits = existingUnits.Where(ou => ou.Id != excludeUnitId);
                }

                return !existingUnits.Any();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania unikalności nazwy {Name}", name);
                return false;
            }
        }

        public async Task<bool> CanMoveUnitAsync(string unitId, string? newParentUnitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
                return false;

            // Jeśli nowy rodzic to null, zawsze można przenieść (na poziom główny)
            if (string.IsNullOrEmpty(newParentUnitId))
                return true;

            // Nie można przenieść jednostki pod samą siebie
            if (unitId == newParentUnitId)
                return false;

            try
            {
                // Sprawdź czy jednostka źródłowa istnieje i jest aktywna
                var sourceUnit = await _organizationalUnitRepository.GetByIdAsync(unitId);
                if (sourceUnit == null || !sourceUnit.IsActive)
                {
                    _logger.LogWarning("Nie można przenieść jednostki {UnitId} - nie istnieje lub jest nieaktywna", unitId);
                    return false;
                }

                // Sprawdź czy nowy rodzic nie jest potomkiem przenoszonej jednostki
                var currentParentId = newParentUnitId;
                while (!string.IsNullOrEmpty(currentParentId))
                {
                    if (currentParentId == unitId)
                        return false; // Cykl wykryty

                    var parentUnit = await _organizationalUnitRepository.GetByIdAsync(currentParentId);
                    if (parentUnit == null || !parentUnit.IsActive)
                    {
                        _logger.LogWarning("Nie można przenieść jednostki - jednostka nadrzędna {ParentId} nie istnieje lub jest nieaktywna", currentParentId);
                        return false;
                    }
                    currentParentId = parentUnit.ParentUnitId;
                }

                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania możliwości przeniesienia jednostki {UnitId}", unitId);
                return false;
            }
        }

        public async Task<IEnumerable<OrganizationalUnit>> GetHierarchyPathAsync(string unitId)
        {
            if (string.IsNullOrWhiteSpace(unitId))
            {
                _logger.LogWarning("Próba pobrania ścieżki hierarchii z pustym ID jednostki");
                return Enumerable.Empty<OrganizationalUnit>();
            }

            try
            {
                var path = new List<OrganizationalUnit>();
                var currentUnitId = unitId;

                while (!string.IsNullOrEmpty(currentUnitId))
                {
                    var unit = await _organizationalUnitRepository.GetByIdAsync(currentUnitId);
                    if (unit == null || !unit.IsActive)
                    {
                        _logger.LogWarning("Jednostka organizacyjna {UnitId} nie istnieje lub jest nieaktywna - przerywanie budowania ścieżki", currentUnitId);
                        break;
                    }

                    path.Insert(0, unit); // Dodaj na początek listy
                    currentUnitId = unit.ParentUnitId;
                }

                if (path.Count == 0)
                {
                    _logger.LogWarning("Nie znaleziono aktywnej ścieżki hierarchii dla jednostki {UnitId}", unitId);
                }
                else
                {
                _logger.LogDebug("Pobrano ścieżkę hierarchii dla jednostki {UnitId}: {PathLength} poziomów", unitId, path.Count);
                }

                return path;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ścieżki hierarchii dla jednostki {UnitId}", unitId);
                throw;
            }
        }

        /// <summary>
        /// Unieważnia cache jednostek organizacyjnych w sposób granularny.
        /// </summary>
        /// <param name="organizationalUnitId">ID jednostki organizacyjnej do unieważnienia</param>
        /// <param name="invalidateAll">Czy unieważnić wszystkie listy jednostek</param>
        private void InvalidateCache(string? organizationalUnitId = null, bool invalidateAll = false)
        {
            if (invalidateAll)
            {
                // Unieważnij wszystkie listy jednostek organizacyjnych
                _powerShellCacheService.Remove(AllOrganizationalUnitsRootOnlyCacheKey);
                _powerShellCacheService.Remove(AllOrganizationalUnitsAllCacheKey);
                _powerShellCacheService.Remove(OrganizationalUnitsHierarchyCacheKey);
                
                _logger.LogDebug("Unieważniono wszystkie listy jednostek organizacyjnych");
            }

            if (!string.IsNullOrEmpty(organizationalUnitId))
            {
                // Unieważnij cache dla konkretnej jednostki
                var unitCacheKeys = new[]
                {
                    $"{OrganizationalUnitByIdCacheKeyPrefix}{organizationalUnitId}_False_False",
                    $"{OrganizationalUnitByIdCacheKeyPrefix}{organizationalUnitId}_True_False",
                    $"{OrganizationalUnitByIdCacheKeyPrefix}{organizationalUnitId}_False_True",
                    $"{OrganizationalUnitByIdCacheKeyPrefix}{organizationalUnitId}_True_True",
                    $"{SubUnitsByParentIdCacheKeyPrefix}{organizationalUnitId}",
                    $"{DepartmentsByUnitIdCacheKeyPrefix}{organizationalUnitId}"
                };

                foreach (var key in unitCacheKeys)
                {
                    _powerShellCacheService.Remove(key);
                }

                _logger.LogDebug("Unieważniono cache dla jednostki organizacyjnej {OrganizationalUnitId}", organizationalUnitId);
            }
        }
    }
} 