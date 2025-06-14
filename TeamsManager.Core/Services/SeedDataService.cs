using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Data;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za tworzenie i zarządzanie danymi początkowymi systemu
    /// </summary>
    public class SeedDataService
    {
        private readonly IGenericRepository<OrganizationalUnit> _organizationalUnitRepository;
        private readonly IGenericRepository<Department> _departmentRepository;
        private readonly ILogger<SeedDataService> _logger;

        // Stałe dla domyślnych danych
        public const string DEFAULT_OU_ID = "default-ou-kadra";
        public const string DEFAULT_DEPARTMENT_ID = "default-dept-administracja";
        public const string DEFAULT_OU_NAME = "Kadra";
        public const string DEFAULT_DEPARTMENT_NAME = "Administracja";

        public SeedDataService(
            IGenericRepository<OrganizationalUnit> organizationalUnitRepository,
            IGenericRepository<Department> departmentRepository,
            ILogger<SeedDataService> logger)
        {
            _organizationalUnitRepository = organizationalUnitRepository ?? throw new ArgumentNullException(nameof(organizationalUnitRepository));
            _departmentRepository = departmentRepository ?? throw new ArgumentNullException(nameof(departmentRepository));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Inicjalizuje dane początkowe systemu jeśli nie istnieją
        /// </summary>
        public async Task InitializeDefaultDataAsync()
        {
            _logger.LogInformation("Rozpoczynanie inicjalizacji danych początkowych systemu");

            try
            {
                // 1. Sprawdź czy domyślna jednostka organizacyjna istnieje
                var defaultOU = await _organizationalUnitRepository.GetByIdAsync(DEFAULT_OU_ID);
                if (defaultOU == null)
                {
                    _logger.LogInformation("Tworzenie domyślnej jednostki organizacyjnej: {Name}", DEFAULT_OU_NAME);
                    defaultOU = await CreateDefaultOrganizationalUnitAsync();
                }
                else
                {
                    _logger.LogDebug("Domyślna jednostka organizacyjna już istnieje: {Name}", defaultOU.Name);
                }

                // 2. Sprawdź czy domyślny dział istnieje
                var defaultDepartment = await _departmentRepository.GetByIdAsync(DEFAULT_DEPARTMENT_ID);
                if (defaultDepartment == null)
                {
                    _logger.LogInformation("Tworzenie domyślnego działu: {Name}", DEFAULT_DEPARTMENT_NAME);
                    defaultDepartment = await CreateDefaultDepartmentAsync(defaultOU.Id);
                }
                else
                {
                    _logger.LogDebug("Domyślny dział już istnieje: {Name}", defaultDepartment.Name);
                }

                _logger.LogInformation("Inicjalizacja danych początkowych zakończona pomyślnie");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas inicjalizacji danych początkowych");
                throw;
            }
        }

        /// <summary>
        /// Pobiera ID domyślnego działu dla nowych użytkowników
        /// </summary>
        public async Task<string> GetDefaultDepartmentIdAsync()
        {
            // Sprawdź czy domyślny dział istnieje
            var defaultDepartment = await _departmentRepository.GetByIdAsync(DEFAULT_DEPARTMENT_ID);
            if (defaultDepartment != null && defaultDepartment.IsActive)
            {
                return defaultDepartment.Id;
            }

            // Jeśli nie istnieje, zainicjalizuj dane początkowe
            await InitializeDefaultDataAsync();
            return DEFAULT_DEPARTMENT_ID;
        }

        /// <summary>
        /// Sprawdza czy element może być usunięty (nie jest domyślnym elementem systemu)
        /// </summary>
        public async Task<bool> CanDeleteOrganizationalUnitAsync(string unitId)
        {
            var unit = await _organizationalUnitRepository.GetByIdAsync(unitId);
            if (unit == null) return false;

            if (unit.IsSystemDefault)
            {
                _logger.LogWarning("Próba usunięcia domyślnej jednostki organizacyjnej: {UnitName} (ID: {UnitId})", unit.Name, unitId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Sprawdza czy dział może być usunięty (nie jest domyślnym działem systemu)
        /// </summary>
        public async Task<bool> CanDeleteDepartmentAsync(string departmentId)
        {
            var department = await _departmentRepository.GetByIdAsync(departmentId);
            if (department == null) return false;

            if (department.IsSystemDefault)
            {
                _logger.LogWarning("Próba usunięcia domyślnego działu: {DepartmentName} (ID: {DepartmentId})", department.Name, departmentId);
                return false;
            }

            return true;
        }

        /// <summary>
        /// Tworzy domyślną jednostkę organizacyjną
        /// </summary>
        private async Task<OrganizationalUnit> CreateDefaultOrganizationalUnitAsync()
        {
            var defaultOU = new OrganizationalUnit
            {
                Id = DEFAULT_OU_ID,
                Name = DEFAULT_OU_NAME,
                Description = "Domyślna jednostka organizacyjna dla kadry administracyjnej",
                Code = "KADRA",
                IsSystemDefault = true,
                IsActive = true,
                SortOrder = 0,
                CreatedBy = "System",
                CreatedDate = DateTime.UtcNow
            };

            await _organizationalUnitRepository.AddAsync(defaultOU);
            await _organizationalUnitRepository.SaveChangesAsync();

            _logger.LogInformation("Utworzono domyślną jednostkę organizacyjną: {Name} (ID: {Id})", defaultOU.Name, defaultOU.Id);
            return defaultOU;
        }

        /// <summary>
        /// Tworzy domyślny dział
        /// </summary>
        private async Task<Department> CreateDefaultDepartmentAsync(string organizationalUnitId)
        {
            var defaultDepartment = new Department
            {
                Id = DEFAULT_DEPARTMENT_ID,
                Name = DEFAULT_DEPARTMENT_NAME,
                Description = "Domyślny dział dla użytkowników administracyjnych i nowych użytkowników systemu",
                OrganizationalUnitId = organizationalUnitId,
                IsSystemDefault = true,
                IsActive = true,
                SortOrder = 0,
                CreatedBy = "System",
                CreatedDate = DateTime.UtcNow
            };

            await _departmentRepository.AddAsync(defaultDepartment);
            await _departmentRepository.SaveChangesAsync();

            _logger.LogInformation("Utworzono domyślny dział: {Name} (ID: {Id})", defaultDepartment.Name, defaultDepartment.Id);
            return defaultDepartment;
        }
    }
} 