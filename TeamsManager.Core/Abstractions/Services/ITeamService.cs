using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za logikę biznesową związaną z zespołami (Team).
    /// </summary>
    public interface ITeamService
    {
        /// <summary>
        /// Asynchronicznie tworzy nowy zespół.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana nowego zespołu.</param>
        /// <param name="description">Opis zespołu.</param>
        /// <param name="ownerUpn">UPN użytkownika, który zostanie właścicielem zespołu.</param>
        /// <param name="visibility">Widoczność zespołu.</param>
        /// <param name="teamTemplateId">Opcjonalny identyfikator szablonu zespołu do użycia.</param>
        /// <param name="schoolTypeId">Opcjonalny identyfikator typu szkoły.</param>
        /// <param name="schoolYearId">Opcjonalny identyfikator roku szkolnego.</param>
        /// <param name="additionalTemplateValues">Opcjonalny słownik wartości do wypełnienia placeholderów w szablonie.</param>
        /// <returns>Utworzony obiekt Team lub null, jeśli operacja się nie powiodła.</returns>
        Task<Team?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility,
            string? teamTemplateId = null,
            string? schoolTypeId = null,
            string? schoolYearId = null,
            Dictionary<string, string>? additionalTemplateValues = null);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne zespoły.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja wszystkich aktywnych zespołów.</returns>
        Task<IEnumerable<Team>> GetAllTeamsAsync(bool forceRefresh = false); // Nazwa sugeruje wszystkie, implementacja repozytorium może filtrować aktywne

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły o statusie Aktywny.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja aktywnych zespołów.</returns>
        Task<IEnumerable<Team>> GetActiveTeamsAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły o statusie Zarchiwizowany.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja zarchiwizowanych zespołów.</returns>
        Task<IEnumerable<Team>> GetArchivedTeamsAsync(bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły, których właścicielem jest użytkownik o podanym UPN.
        /// </summary>
        /// <param name="ownerUpn">UPN właściciela.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Kolekcja zespołów należących do danego właściciela.</returns>
        Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie pobiera zespół na podstawie jego ID, opcjonalnie dołączając powiązane encje.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="includeMembers">Czy dołączyć członków zespołu.</param>
        /// <param name="includeChannels">Czy dołączyć kanały zespołu.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <returns>Obiekt Team lub null, jeśli nie znaleziono.</returns>
        Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false, bool forceRefresh = false);

        /// <summary>
        /// Asynchronicznie aktualizuje dane zespołu.
        /// </summary>
        /// <param name="team">Obiekt Team z zaktualizowanymi danymi.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateTeamAsync(Team team);

        /// <summary>
        /// Asynchronicznie archiwizuje zespół.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do archiwizacji.</param>
        /// <param name="reason">Powód archiwizacji.</param>
        /// <returns>True, jeśli archiwizacja się powiodła.</returns>
        Task<bool> ArchiveTeamAsync(string teamId, string reason);

        /// <summary>
        /// Asynchronicznie przywraca zarchiwizowany zespół.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do przywrócenia.</param>
        /// <returns>True, jeśli przywrócenie się powiodło.</returns>
        Task<bool> RestoreTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie usuwa zespół (logicznie).
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do usunięcia.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> DeleteTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie dodaje użytkownika jako członka do zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userUpn">UPN użytkownika do dodania.</param>
        /// <param name="role">Rola członka w zespole.</param>
        /// <returns>Utworzony obiekt TeamMember lub null, jeśli operacja się nie powiodła.</returns>
        Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role);

        /// <summary>
        /// Asynchronicznie usuwa członka z zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userId">Identyfikator użytkownika do usunięcia.</param>
        /// <returns>True, jeśli usunięcie członka się powiodło.</returns>
        Task<bool> RemoveMemberAsync(string teamId, string userId);

        /// <summary>
        /// Odświeża cache zespołów (jeśli jest używany).
        /// </summary>
        Task RefreshCacheAsync();
    }
}