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
        /// <returns>Obiekt Department lub null, jeśli nie znaleziono.</returns>
        Task<Department?> GetDepartmentByIdAsync(string departmentId, bool includeSubDepartments = false, bool includeUsers = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie działy (tylko najwyższy poziom lub wszystkie, w zależności od implementacji).
        /// </summary>
        /// <param name="onlyRootDepartments">Czy pobrać tylko działy najwyższego poziomu (bez rodzica).</param>
        /// <returns>Kolekcja wszystkich (lub głównych) działów.</returns>
        Task<IEnumerable<Department>> GetAllDepartmentsAsync(bool onlyRootDepartments = false);

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
            string? parentDepartmentId = null,
            string? departmentCode = null);

        /// <summary>
        /// Asynchronicznie aktualizuje dane istniejącego działu.
        /// </summary>
        /// <param name="departmentToUpdate">Obiekt Department z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateDepartmentAsync(Department departmentToUpdate);

        /// <summary>
        /// Asynchronicznie usuwa dział (logicznie lub fizycznie).
        /// Należy uwzględnić logikę OnDelete.Restrict dla użytkowników i poddziałów.
        /// </summary>
        /// <param name="departmentId">Identyfikator działu do usunięcia.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> DeleteDepartmentAsync(string departmentId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie poddziały dla danego działu nadrzędnego.
        /// </summary>
        /// <param name="parentDepartmentId">Identyfikator działu nadrzędnego.</param>
        /// <returns>Kolekcja poddziałów.</returns>
        Task<IEnumerable<Department>> GetSubDepartmentsAsync(string parentDepartmentId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich aktywnych użytkowników przypisanych bezpośrednio do danego działu.
        /// </summary>
        /// <param name="departmentId">Identyfikator działu.</param>
        /// <returns>Kolekcja użytkowników.</returns>
        Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId);
    }
}