// Plik: TeamsManager.Core/Abstractions/Services/ITeamService.cs
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
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
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
            string accessToken,
            string? teamTemplateId = null,
            string? schoolTypeId = null,
            string? schoolYearId = null,
            Dictionary<string, string>? additionalTemplateValues = null);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie aktywne zespoły.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Kolekcja wszystkich aktywnych zespołów.</returns>
        Task<IEnumerable<Team>> GetAllTeamsAsync(bool forceRefresh = false, string? accessToken = null);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły o statusie Aktywny.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Kolekcja aktywnych zespołów.</returns>
        Task<IEnumerable<Team>> GetActiveTeamsAsync(bool forceRefresh = false, string? accessToken = null);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły o statusie Zarchiwizowany.
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Kolekcja zarchiwizowanych zespołów.</returns>
        Task<IEnumerable<Team>> GetArchivedTeamsAsync(bool forceRefresh = false, string? accessToken = null);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły, których właścicielem jest użytkownik o podanym UPN.
        /// </summary>
        /// <param name="ownerUpn">UPN właściciela.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Kolekcja zespołów należących do danego właściciela.</returns>
        Task<IEnumerable<Team>> GetTeamsByOwnerAsync(string ownerUpn, bool forceRefresh = false, string? accessToken = null);

        /// <summary>
        /// Asynchronicznie pobiera zespół na podstawie jego ID, opcjonalnie dołączając powiązane encje.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="includeMembers">Czy dołączyć członków zespołu.</param>
        /// <param name="includeChannels">Czy dołączyć kanały zespołu.</param>
        /// <param name="forceRefresh">Czy wymusić odświeżenie danych z pominięciem cache.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Obiekt Team lub null, jeśli nie znaleziono.</returns>
        Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false, bool forceRefresh = false, string? accessToken = null);

        /// <summary>
        /// Asynchronicznie aktualizuje dane zespołu.
        /// </summary>
        /// <param name="team">Obiekt Team z zaktualizowanymi danymi.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>True, jeśli aktualizacja się powiodła.</returns>
        Task<bool> UpdateTeamAsync(Team team, string accessToken);

        /// <summary>
        /// Asynchronicznie archiwizuje zespół.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do archiwizacji.</param>
        /// <param name="reason">Powód archiwizacji.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>True, jeśli archiwizacja się powiodła.</returns>
        Task<bool> ArchiveTeamAsync(string teamId, string reason, string accessToken);

        /// <summary>
        /// Asynchronicznie przywraca zarchiwizowany zespół.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do przywrócenia.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>True, jeśli przywrócenie się powiodło.</returns>
        Task<bool> RestoreTeamAsync(string teamId, string accessToken);

        /// <summary>
        /// Asynchronicznie usuwa zespół (logicznie).
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu do usunięcia.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>True, jeśli usunięcie się powiodło.</returns>
        Task<bool> DeleteTeamAsync(string teamId, string accessToken);

        /// <summary>
        /// Asynchronicznie dodaje użytkownika jako członka do zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userUpn">UPN użytkownika do dodania.</param>
        /// <param name="role">Rola członka w zespole.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Utworzony obiekt TeamMember lub null, jeśli operacja się nie powiodła.</returns>
        Task<TeamMember?> AddMemberAsync(string teamId, string userUpn, TeamMemberRole role, string accessToken);

        /// <summary>
        /// Asynchronicznie usuwa członka z zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userId">Identyfikator użytkownika do usunięcia.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>True, jeśli usunięcie członka się powiodło.</returns>
        Task<bool> RemoveMemberAsync(string teamId, string userId, string accessToken);

        /// <summary>
        /// Asynchronicznie dodaje wielu użytkowników do zespołu (operacja masowa).
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userUpns">Lista UPN użytkowników do dodania.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika.</returns>
        Task<Dictionary<string, bool>> AddUsersToTeamAsync(string teamId, List<string> userUpns, string accessToken);

        /// <summary>
        /// Asynchronicznie usuwa wielu użytkowników z zespołu (operacja masowa).
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu.</param>
        /// <param name="userUpns">Lista UPN użytkowników do usunięcia.</param>
        /// <param name="reason">Powód usunięcia.</param>
        /// <param name="accessToken">Token dostępu OAuth 2.0 do Microsoft Graph API.</param>
        /// <returns>Słownik z wynikami operacji dla każdego użytkownika.</returns>
        Task<Dictionary<string, bool>> RemoveUsersFromTeamAsync(string teamId, List<string> userUpns, string reason, string accessToken);

        /// <summary>
        /// Odświeża cache zespołów (jeśli jest używany).
        /// </summary>
        Task RefreshCacheAsync();
    }
}