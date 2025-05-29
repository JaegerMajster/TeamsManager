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
        /// </summary>
        /// <param name="upn">UPN użytkownika.</param>
        /// <returns>Obiekt User lub null, jeśli nie znaleziono.</returns>
        Task<User?> GetUserByUpnAsync(string upn);

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