using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z działami (Department).
    /// </summary>
    public interface IDepartmentService
    {
        /// <summary>
        /// Asynchronicznie pobiera dział na podstawie jego ID.
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <param name="includeSubDepartments">Czy dołączyć poddziały.</param>
        /// <param name="includeUsers">Czy dołączyć użytkowników przypisanych do działu.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt Department lub null, jeśli nie znaleziono.</returns>
        Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments, bool includeUsers, bool forceRefresh);

        /// <summary>
        /// Asynchronicznie pobiera dział na podstawie jego ID (wygodne przeciążenie).
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <returns>Obiekt Department lub null, jeśli nie znaleziono.</returns>
        Task<Department?> GetDepartmentByIdAsync(string departmentId);

        /// <summary>
        /// Asynchronicznie pobiera dział na podstawie jego ID z poddziałami (wygodne przeciążenie).
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <param name="includeSubDepartments">Czy dołączyć poddziały.</param>
        /// <returns>Obiekt Department lub null, jeśli nie znaleziono.</returns>
        Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments);

        /// <summary>
        /// Asynchronicznie pobiera dział na podstawie jego ID z poddziałami i użytkownikami (wygodne przeciążenie).
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <param name="includeSubDepartments">Czy dołączyć poddziały.</param>
        /// <param name="includeUsers">Czy dołączyć użytkowników przypisanych do działu.</param>
        /// <returns>Obiekt Department lub null, jeśli nie znaleziono.</returns>
        Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments, bool includeUsers);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne działy.
        /// </summary>
        /// <param name="onlyRootDepartments">Czy pobrać tylko działy najwyższego poziomu (bez rodzica).</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja wszystkich (lub głównych) aktywnych działów.</returns>
        Task<IEnumerable<Department>> GetAllDepartmentsAsync(bool onlyRootDepartments, bool forceRefresh);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne działy (wygodne przeciążenie).
        /// </summary>
        /// <returns>Kolekcja wszystkich aktywnych działów.</returns>
        Task<IEnumerable<Department>> GetAllDepartmentsAsync();

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne działy lub tylko główne (wygodne przeciążenie).
        /// </summary>
        /// <param name="onlyRootDepartments">Czy pobrać tylko działy najwyższego poziomu (bez rodzica).</param>
        /// <returns>Kolekcja wszystkich (lub głównych) aktywnych działów.</returns>
        Task<IEnumerable<Department>> GetAllDepartmentsAsync(bool onlyRootDepartments);

        /// <summary>
        /// Asynchronicznie tworzy nowy dział.
        /// </summary>
        /// <param name="name">Nazwa nowego działu.</param>
        /// <param name="description">Opis działu.</param>
        /// <param name="parentDepartmentId">Opcjonalny identyfikator działu nadrzędnego.</param>
        /// <param name="departmentCode">Opcjonalny kod działu.</param>
        /// <returns>Utworzony obiekt Department lub null, jeśli operacja się nie powiodła.</returns>
        Task<Department?> CreateDepartmentAsync(
            string name,
            string description,
            string? parentDepartmentId,
            string? departmentCode);

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego działu.
        /// </summary>
        /// <param name="departmentToUpdate">Obiekt Department z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateDepartmentAsync(Department departmentToUpdate);

        /// <summary>
        /// Asynchronicznie usuwa dział (logicznie).
        /// </summary>
        /// <param name="departmentId">Identyfikator działu do usunięcia.</param>
        /// <returns>True, jeśli usunięcie (dezaktywacja) się powiodło.</returns>
        Task<bool> DeleteDepartmentAsync(string departmentId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne poddziały dla danego działu nadrzędnego.
        /// </summary>
        /// <param name="parentDepartmentId">Identyfikator działu nadrzędnego.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja poddziałów.</returns>
        Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId, bool forceRefresh);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne poddziały dla danego działu nadrzędnego (wygodne przeciążenie).
        /// </summary>
        /// <param name="parentDepartmentId">Identyfikator działu nadrzędnego.</param>
        /// <returns>Kolekcja poddziałów.</returns>
        Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich aktywnych użytkowników przypisanych bezpośrednio do danego działu.
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja użytkowników.</returns>
        Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId, bool forceRefresh);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich aktywnych użytkowników przypisanych bezpośrednio do danego działu (wygodne przeciążenie).
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <returns>Kolekcja użytkowników.</returns>
        Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId);

        /// <summary>
        /// Odświeża cache działów (jeśli jest używany).
        /// </summary>
        Task RefreshCacheAsync();
    }
}