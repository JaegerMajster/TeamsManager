using TeamsManager.Core.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Data
{
    /// <summary>
    /// Interfejs repozytorium dla encji Team, rozszerzający IGenericRepository.
    /// </summary>
    public interface ITeamRepository : IGenericRepository<Team>
    {
        /// <summary>
        /// Asynchronicznie pobiera zespół na podstawie jego nazwy wyświetlanej.
        /// Uwaga: Nazwa wyświetlana może nie być unikalna. Rozważ zwracanie kolekcji lub pierwszego pasującego.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana zespołu.</param>
        /// <returns>Obiekt Team lub null, jeśli nie znaleziono (lub pierwszy pasujący).</returns>
        Task<Team?> GetTeamByNameAsync(string displayName); // Lub Task<IEnumerable<Team>>

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły, których właścicielem jest użytkownik o podanym UPN.
        /// </summary>
        /// <param name="ownerUpn">UPN właściciela.</param>
        /// <returns>Kolekcja zespołów należących do danego właściciela.</returns>
        Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły o statusie Aktywny.
        /// </summary>
        Task<IEnumerable<Team>> GetActiveTeamsAsync();

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły o statusie Zarchiwizowany.
        /// </summary>
        Task<IEnumerable<Team>> GetArchivedTeamsAsync();

        // Można tu dodać inne specyficzne metody, np.
        // Task<IEnumerable<Team>> GetTeamsBySchoolTypeAsync(string schoolTypeId);
        // Task<IEnumerable<Team>> GetTeamsBySchoolYearAsync(string schoolYearId);
        // Task<IEnumerable<Team>> GetTeamsUsingTemplateAsync(string templateId);
    }
}