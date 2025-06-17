using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający zespołami i kanałami w Microsoft Teams przez Graph API
    /// Implementacja zarządzania zespołami z pełnym wsparciem dla Graph API endpoints
    /// </summary>
    public interface IGraphTeamManagementService
    {
        #region Team Operations

        /// <summary>
        /// Tworzy nowy zespół w Microsoft Teams
        /// Graph API Endpoint: POST /v1.0/teams
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana zespołu</param>
        /// <param name="description">Opis zespołu</param>
        /// <param name="ownerUpn">UPN właściciela zespołu</param>
        /// <param name="visibility">Widoczność zespołu (Private/Public)</param>
        /// <param name="template">Szablon zespołu (opcjonalny)</param>
        /// <returns>GraphTeam z ID utworzonego zespołu lub null w przypadku błędu</returns>
        Task<GraphTeam?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility = TeamVisibility.Private,
            string? template = null);

        /// <summary>
        /// Aktualizuje właściwości zespołu
        /// Graph API Endpoint: PATCH /v1.0/teams/{team-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="newDisplayName">Nowa nazwa (opcjonalna)</param>
        /// <param name="newDescription">Nowy opis (opcjonalny)</param>
        /// <param name="newVisibility">Nowa widoczność (opcjonalna)</param>
        /// <returns>True jeśli aktualizacja się powiodła</returns>
        Task<bool> UpdateTeamPropertiesAsync(
            string teamId,
            string? newDisplayName = null,
            string? newDescription = null,
            TeamVisibility? newVisibility = null);

        /// <summary>
        /// Archiwizuje zespół
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/archive
        /// </summary>
        /// <param name="teamId">ID zespołu do archiwizacji</param>
        /// <returns>True jeśli archiwizacja się powiodła</returns>
        Task<bool> ArchiveTeamAsync(string teamId);

        /// <summary>
        /// Przywraca zespół z archiwum
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/unarchive
        /// </summary>
        /// <param name="teamId">ID zespołu do przywrócenia</param>
        /// <returns>True jeśli przywrócenie się powiodło</returns>
        Task<bool> UnarchiveTeamAsync(string teamId);

        /// <summary>
        /// Usuwa zespół
        /// Graph API Endpoint: DELETE /v1.0/groups/{group-id}
        /// </summary>
        /// <param name="teamId">ID zespołu do usunięcia</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> DeleteTeamAsync(string teamId);

        /// <summary>
        /// Pobiera szczegóły zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>GraphTeam z danymi zespołu lub null</returns>
        Task<GraphTeam?> GetTeamAsync(string teamId);

        /// <summary>
        /// Pobiera wszystkie zespoły
        /// Graph API Endpoint: GET /v1.0/me/joinedTeams
        /// </summary>
        /// <returns>Lista zespołów lub null</returns>
        Task<List<GraphTeam>?> GetAllTeamsAsync();

        /// <summary>
        /// Pobiera zespoły należące do określonego właściciela
        /// Graph API Endpoint: GET /v1.0/users/{user-id}/ownedObjects
        /// </summary>
        /// <param name="ownerUpn">UPN właściciela</param>
        /// <returns>Lista zespołów lub null</returns>
        Task<List<GraphTeam>?> GetTeamsByOwnerAsync(string ownerUpn);

        #endregion

        #region Team Member Management - Critical P0 Methods

        /// <summary>
        /// Pobiera wszystkich członków zespołu z cache i walidacją (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <returns>Lista członków zespołu z rolami</returns>
        Task<List<GraphTeamMember>?> GetTeamMembersAsync(string teamId);

        /// <summary>
        /// Pobiera pojedynczego członka zespołu z walidacją (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>GraphTeamMember z informacjami o członku zespołu lub null jeśli nie jest członkiem</returns>
        Task<GraphTeamMember?> GetTeamMemberAsync(string teamId, string userUpn);

        /// <summary>
        /// Dodaje członka do zespołu
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/members
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="role">Rola: Owner lub Member</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> AddTeamMemberAsync(string teamId, string userUpn, string role = "Member");

        /// <summary>
        /// Usuwa członka z zespołu
        /// Graph API Endpoint: DELETE /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> RemoveTeamMemberAsync(string teamId, string userUpn);

        /// <summary>
        /// Zmienia rolę członka zespołu (Owner to Member) (P0-CRITICAL)
        /// Graph API Endpoint: PATCH /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="newRole">Nowa rola: Owner lub Member</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> UpdateTeamMemberRoleAsync(string teamId, string userUpn, string newRole);

        /// <summary>
        /// Weryfikuje uprawnienia Microsoft.Graph dla operacji Team Members
        /// Graph API Endpoint: GET /v1.0/me/memberOf (test permissions)
        /// </summary>
        /// <returns>True jeśli wymagane uprawnienia są dostępne</returns>
        Task<bool> VerifyGraphPermissionsAsync();

        #endregion

        #region Diagnostic Operations

        /// <summary>
        /// Testuje połączenie z Microsoft Graph
        /// Graph API Endpoint: GET /v1.0/me
        /// </summary>
        /// <returns>True jeśli połączenie aktywne</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// Waliduje uprawnienia Graph API
        /// Graph API Endpoint: GET /v1.0/me/memberOf (with required scopes check)
        /// </summary>
        /// <returns>Słownik uprawnień i ich statusu</returns>
        Task<Dictionary<string, bool>> ValidatePermissionsAsync();

        /// <summary>
        /// Pobiera informacje diagnostyczne o Graph API
        /// Graph API Endpoint: GET /v1.0/me + dodatowe testy
        /// </summary>
        /// <returns>GraphDiagnosticInfo z informacjami systemowymi lub null</returns>
        Task<GraphDiagnosticInfo?> GetSystemInfoAsync();

        /// <summary>
        /// Pobiera wersję Graph API i informacje o aplikacji
        /// Graph API Endpoint: GET /v1.0/$metadata (version info)
        /// </summary>
        /// <returns>GraphDiagnosticInfo z informacjami o wersji lub null</returns>
        Task<GraphDiagnosticInfo?> GetGraphVersionAsync();

        #endregion

        #region Channel Operations

        /// <summary>
        /// Tworzy nowy kanał w zespole
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/channels
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="displayName">Nazwa kanału</param>
        /// <param name="isPrivate">Czy kanał ma być prywatny</param>
        /// <param name="description">Opis kanału (opcjonalny)</param>
        /// <returns>GraphChannel z danymi kanału lub null</returns>
        Task<GraphChannel?> CreateTeamChannelAsync(
            string teamId, 
            string displayName, 
            bool isPrivate = false, 
            string? description = null);

        /// <summary>
        /// Aktualizuje właściwości kanału
        /// Graph API Endpoint: PATCH /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <param name="newDisplayName">Nowa nazwa (opcjonalna)</param>
        /// <param name="newDescription">Nowy opis (opcjonalny)</param>
        /// <returns>True jeśli aktualizacja się powiodła</returns>
        Task<bool> UpdateTeamChannelAsync(
            string teamId,
            string channelId,
            string? newDisplayName = null,
            string? newDescription = null);

        /// <summary>
        /// Usuwa kanał z zespołu
        /// Graph API Endpoint: DELETE /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> RemoveTeamChannelAsync(string teamId, string channelId);

        /// <summary>
        /// Pobiera wszystkie kanały zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/channels
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>Lista kanałów lub null</returns>
        Task<List<GraphChannel>?> GetTeamChannelsAsync(string teamId);

        /// <summary>
        /// Pobiera kanał zespołu po nazwie
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/channels?$filter=displayName eq '{name}'
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelDisplayName">Nazwa kanału</param>
        /// <returns>GraphChannel z danymi kanału lub null</returns>
        Task<GraphChannel?> GetTeamChannelAsync(string teamId, string channelDisplayName);

        /// <summary>
        /// Pobiera kanał zespołu po jego ID
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <returns>GraphChannel z danymi kanału lub null</returns>
        Task<GraphChannel?> GetTeamChannelByIdAsync(string teamId, string channelId);

        #endregion
    }
} 