using TeamsManager.Core.Models;
using TeamsManager.Core.Enums; // Dla UserRole
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla encji User, rozszerzający IGenericRepository.
    /// </summary>
    public interface IUserRepository : IGenericRepository<User>
    {
        /// <summary>
        /// Asynchronicznie pobiera użytkownika na podstawie jego User Principal Name (UPN).
        /// UWAGA: Ta metoda NIE filtruje po IsActive - może zwrócić nieaktywnych użytkowników.
        /// Rozważ użycie GetActiveUserByUpnAsync() jeśli potrzebujesz tylko aktywnych użytkowników.
        /// </summary>
        /// <param name="upn">UPN użytkownika.</param>
        /// <returns>Obiekt User lub null, jeśli nie znaleziono.</returns>
        Task<User?> GetUserByUpnAsync(string upn);

        /// <summary>
        /// Asynchronicznie pobiera aktywnego użytkownika na podstawie jego User Principal Name (UPN).
        /// Zwraca tylko użytkowników z IsActive = true.
        /// Zawiera pełne dołączenie relacji (Department, TeamMemberships, SchoolTypes).
        /// </summary>
        /// <param name="upn">UPN użytkownika.</param>
        /// <returns>Aktywny obiekt User z pełnymi relacjami lub null, jeśli nie znaleziono aktywnego użytkownika.</returns>
        Task<User?> GetActiveUserByUpnAsync(string upn);

        /// <summary>
        /// Asynchronicznie pobiera aktywnego użytkownika na podstawie ID.
        /// Zwraca tylko użytkowników z IsActive = true.
        /// Zawiera pełne dołączenie relacji (Department, TeamMemberships, SchoolTypes).
        /// </summary>
        /// <param name="id">ID użytkownika.</param>
        /// <returns>Aktywny obiekt User z pełnymi relacjami lub null, jeśli nie znaleziono aktywnego użytkownika.</returns>
        Task<User?> GetActiveByIdAsync(string id);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich użytkowników z określoną rolą systemową.
        /// </summary>
        /// <param name="role">Rola systemowa użytkownika.</param>
        /// <returns>Kolekcja użytkowników z daną rolą.</returns>
        Task<IEnumerable<User>> GetUsersByRoleAsync(UserRole role);

        /// <summary>
        /// Asynchronicznie wyszukuje użytkowników na podstawie fragmentu imienia, nazwiska lub UPN.
        /// </summary>
        /// <param name="searchTerm">Fragment tekstu do wyszukania.</param>
        /// <returns>Kolekcja pasujących użytkowników.</returns>
        Task<IEnumerable<User>> SearchUsersAsync(string searchTerm);

        // Można tu dodać inne specyficzne metody, np.
        // Task<IEnumerable<User>> GetUsersInDepartmentAsync(string departmentId);
        // Task<IEnumerable<User>> GetSystemAdministratorsAsync();
    }
}