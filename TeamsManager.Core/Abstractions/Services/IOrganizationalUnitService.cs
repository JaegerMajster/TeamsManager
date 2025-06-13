using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z jednostkami organizacyjnymi (OrganizationalUnit).
    /// </summary>
    public interface IOrganizationalUnitService
    {
        /// <summary>
        /// Asynchronicznie pobiera jednostkę organizacyjną na podstawie jej ID.
        /// </summary>
        /// <param name="unitId">Identyfikator jednostki organizacyjnej.</param>
        /// <param name="includeSubUnits">Czy dołączyć podjednostki.</param>
        /// <param name="includeDepartments">Czy dołączyć działy przypisane do jednostki.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt OrganizationalUnit lub null, jeśli nie znaleziono.</returns>
        Task<OrganizationalUnit?> GetOrganizationalUnitByIdAsync(string unitId, bool includeSubUnits = false, bool includeDepartments = false, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne jednostki organizacyjne.
        /// </summary>
        /// <param name="onlyRootUnits">Czy pobrać tylko jednostki najwyższego poziomu (bez rodzica).</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja wszystkich (lub głównych) aktywnych jednostek organizacyjnych.</returns>
        Task<IEnumerable<OrganizationalUnit>> GetAllOrganizationalUnitsAsync(bool onlyRootUnits = false, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera podjednostki dla danej jednostki organizacyjnej.
        /// </summary>
        /// <param name="parentUnitId">Identyfikator jednostki nadrzędnej.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja podjednostek.</returns>
        Task<IEnumerable<OrganizationalUnit>> GetSubUnitsAsync(string parentUnitId, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera hierarchię jednostek organizacyjnych w formie drzewa.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja jednostek organizacyjnych z załadowanymi podjednostkami.</returns>
        Task<IEnumerable<OrganizationalUnit>> GetOrganizationalUnitsHierarchyAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie tworzy nową jednostkę organizacyjną.
        /// </summary>
        /// <param name="unit">Obiekt jednostki organizacyjnej do utworzenia.</param>
        /// <returns>Utworzona jednostka organizacyjna.</returns>
        Task<OrganizationalUnit> CreateOrganizationalUnitAsync(OrganizationalUnit unit);

        /// <summary>
        /// Asynchronicznie aktualizuje istniejącą jednostkę organizacyjną.
        /// </summary>
        /// <param name="unit">Obiekt jednostki organizacyjnej do aktualizacji.</param>
        /// <returns>Zaktualizowana jednostka organizacyjna.</returns>
        Task<OrganizationalUnit> UpdateOrganizationalUnitAsync(OrganizationalUnit unit);

        /// <summary>
        /// Asynchronicznie usuwa jednostkę organizacyjną.
        /// </summary>
        /// <param name="unitId">Identyfikator jednostki organizacyjnej do usunięcia.</param>
        /// <param name="forceDelete">Czy wymusić usunięcie nawet jeśli jednostka ma przypisane działy lub podjednostki.</param>
        /// <returns>True jeśli usunięto pomyślnie, false w przeciwnym razie.</returns>
        Task<bool> DeleteOrganizationalUnitAsync(string unitId, bool forceDelete = false);

        /// <summary>
        /// Asynchronicznie sprawdza czy jednostka organizacyjna może być usunięta.
        /// </summary>
        /// <param name="unitId">Identyfikator jednostki organizacyjnej.</param>
        /// <returns>True jeśli można usunąć, false w przeciwnym razie.</returns>
        Task<bool> CanDeleteOrganizationalUnitAsync(string unitId);

        /// <summary>
        /// Asynchronicznie pobiera działy przypisane do jednostki organizacyjnej.
        /// </summary>
        /// <param name="unitId">Identyfikator jednostki organizacyjnej.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja działów przypisanych do jednostki.</returns>
        Task<IEnumerable<Department>> GetDepartmentsByOrganizationalUnitAsync(string unitId, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie przenosi działy z jednej jednostki organizacyjnej do drugiej.
        /// </summary>
        /// <param name="departmentIds">Lista identyfikatorów działów do przeniesienia.</param>
        /// <param name="targetUnitId">Identyfikator docelowej jednostki organizacyjnej.</param>
        /// <returns>Liczba przeniesionych działów.</returns>
        Task<int> MoveDepartmentsToOrganizationalUnitAsync(IEnumerable<string> departmentIds, string targetUnitId);

        /// <summary>
        /// Asynchronicznie sprawdza czy nazwa jednostki organizacyjnej jest unikalna w ramach tej samej jednostki nadrzędnej.
        /// </summary>
        /// <param name="name">Nazwa do sprawdzenia.</param>
        /// <param name="parentUnitId">Identyfikator jednostki nadrzędnej (null dla jednostek głównych).</param>
        /// <param name="excludeUnitId">Identyfikator jednostki do wykluczenia z sprawdzania (dla aktualizacji).</param>
        /// <returns>True jeśli nazwa jest unikalna, false w przeciwnym razie.</returns>
        Task<bool> IsNameUniqueAsync(string name, string? parentUnitId = null, string? excludeUnitId = null);

        /// <summary>
        /// Asynchronicznie sprawdza czy jednostka organizacyjna może być przeniesiona pod inną jednostkę (sprawdza cykle).
        /// </summary>
        /// <param name="unitId">Identyfikator jednostki do przeniesienia.</param>
        /// <param name="newParentUnitId">Identyfikator nowej jednostki nadrzędnej.</param>
        /// <returns>True jeśli można przenieść, false w przeciwnym razie.</returns>
        Task<bool> CanMoveUnitAsync(string unitId, string? newParentUnitId);

        /// <summary>
        /// Asynchronicznie pobiera ścieżkę hierarchii dla jednostki organizacyjnej.
        /// </summary>
        /// <param name="unitId">Identyfikator jednostki organizacyjnej.</param>
        /// <returns>Lista jednostek organizacyjnych od głównej do podanej.</returns>
        Task<IEnumerable<OrganizationalUnit>> GetHierarchyPathAsync(string unitId);
    }
} 