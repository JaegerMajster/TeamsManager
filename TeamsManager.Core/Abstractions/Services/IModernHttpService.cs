using System.Threading.Tasks;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net.Http;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs nowoczesnego HTTP service wykorzystującego Microsoft.Extensions.Http.Resilience
    /// Zastępuje stare wzorce resilience
    /// </summary>
    public interface IModernHttpService
    {
        /// <summary>
        /// Wykonuje żądanie GET zwracające HttpResponseMessage
        /// </summary>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>HttpResponseMessage</returns>
        Task<HttpResponseMessage> GetAsync(string url, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie POST zwracające HttpResponseMessage
        /// </summary>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="content">Zawartość do wysłania (JSON string)</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>HttpResponseMessage</returns>
        Task<HttpResponseMessage> PostAsync(string url, string content, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie PATCH zwracające HttpResponseMessage
        /// </summary>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="content">Zawartość do wysłania (JSON string)</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>HttpResponseMessage</returns>
        Task<HttpResponseMessage> PatchAsync(string url, string content, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie DELETE zwracające HttpResponseMessage
        /// </summary>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>HttpResponseMessage</returns>
        Task<HttpResponseMessage> DeleteAsync(string url, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie GET do Microsoft Graph API z automatycznym resilience
        /// </summary>
        /// <typeparam name="T">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="endpoint">Endpoint Graph API (np. "v1.0/groups")</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zdeserializowany obiekt lub null w przypadku błędu</returns>
        Task<T?> GetFromGraphAsync<T>(string endpoint, string? accessToken = null) where T : class;

        /// <summary>
        /// Wykonuje żądanie POST do Microsoft Graph API z automatycznym resilience
        /// </summary>
        /// <typeparam name="TRequest">Typ danych do wysłania</typeparam>
        /// <typeparam name="TResponse">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="data">Dane do wysłania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zdeserializowany obiekt odpowiedzi lub null w przypadku błędu</returns>
        Task<TResponse?> PostToGraphAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest data, 
            string? accessToken = null) 
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Wykonuje żądanie PATCH do Microsoft Graph API z automatycznym resilience
        /// </summary>
        /// <typeparam name="TRequest">Typ danych do wysłania</typeparam>
        /// <typeparam name="TResponse">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="data">Dane do aktualizacji</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zdeserializowany obiekt odpowiedzi lub null w przypadku błędu</returns>
        Task<TResponse?> PatchToGraphAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest data, 
            string? accessToken = null) 
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Wykonuje żądanie DELETE do Microsoft Graph API z automatycznym resilience
        /// </summary>
        /// <param name="endpoint">Endpoint Graph API</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> DeleteFromGraphAsync(string endpoint, string? accessToken = null);

        /// <summary>
        /// Wykonuje żądanie GET do zewnętrznego API z resilience
        /// </summary>
        /// <typeparam name="T">Typ oczekiwanej odpowiedzi</typeparam>
        /// <param name="url">Pełny URL do API</param>
        /// <returns>Zdeserializowany obiekt lub null w przypadku błędu</returns>
        Task<T?> GetFromExternalApiAsync<T>(string url) where T : class;

        /// <summary>
        /// Sprawdza dostępność Microsoft Graph API
        /// </summary>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli API jest dostępne</returns>
        Task<bool> CheckGraphApiHealthAsync(string? accessToken = null);

        /// <summary>
        /// Tworzy nowy zespół Microsoft Teams
        /// Endpoint: POST /v1.0/teams
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania zespołu</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi zespołu</typeparam>
        /// <param name="teamData">Dane zespołu do utworzenia</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Utworzony zespół lub null w przypadku błędu</returns>
        Task<TResponse?> CreateTeamAsync<TRequest, TResponse>(
            TRequest teamData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Aktualizuje zespół Microsoft Teams
        /// Endpoint: PATCH /v1.0/teams/{team-id}
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania aktualizacji</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi zespołu</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="updateData">Dane do aktualizacji</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zaktualizowany zespół lub null w przypadku błędu</returns>
        Task<TResponse?> UpdateTeamAsync<TRequest, TResponse>(
            string teamId, 
            TRequest updateData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Pobiera zespół Microsoft Teams
        /// Endpoint: GET /v1.0/teams/{team-id}
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi zespołu</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zespół lub null jeśli nie znaleziono</returns>
        Task<TResponse?> GetTeamAsync<TResponse>(
            string teamId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera wszystkie zespoły Microsoft Teams
        /// Endpoint: GET /v1.0/groups?$filter=resourceProvisioningOptions/Any(x:x eq 'Team')
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji zespołów</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista zespołów lub null w przypadku błędu</returns>
        Task<TResponse?> GetAllTeamsAsync<TResponse>(string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Archiwizuje zespół Microsoft Teams
        /// Endpoint: POST /v1.0/teams/{team-id}/archive
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> ArchiveTeamAsync(string teamId, string? accessToken = null);

        /// <summary>
        /// Przywraca zespół Microsoft Teams z archiwum
        /// Endpoint: POST /v1.0/teams/{team-id}/unarchive
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> UnarchiveTeamAsync(string teamId, string? accessToken = null);

        /// <summary>
        /// Usuwa zespół Microsoft Teams
        /// Endpoint: DELETE /v1.0/groups/{group-id}
        /// </summary>
        /// <param name="teamId">ID zespołu (Group ID)</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> DeleteTeamAsync(string teamId, string? accessToken = null);

        /// <summary>
        /// Pobiera członków zespołu Microsoft Teams
        /// Endpoint: GET /v1.0/teams/{team-id}/members
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi członków</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista członków lub null w przypadku błędu</returns>
        Task<TResponse?> GetTeamMembersAsync<TResponse>(
            string teamId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Dodaje członka do zespołu Microsoft Teams
        /// Endpoint: POST /v1.0/teams/{team-id}/members
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania członka</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi członka</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="memberData">Dane członka do dodania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Dodany członek lub null w przypadku błędu</returns>
        Task<TResponse?> AddTeamMemberAsync<TRequest, TResponse>(
            string teamId, 
            TRequest memberData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Usuwa członka z zespołu Microsoft Teams
        /// Endpoint: DELETE /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="membershipId">ID członkostwa</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> RemoveTeamMemberAsync(
            string teamId, 
            string membershipId, 
            string? accessToken = null);

        /// <summary>
        /// Pobiera kanały zespołu Microsoft Teams
        /// Endpoint: GET /v1.0/teams/{team-id}/channels
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kanałów</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista kanałów lub null w przypadku błędu</returns>
        Task<TResponse?> GetTeamChannelsAsync<TResponse>(
            string teamId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Tworzy kanał w zespole Microsoft Teams
        /// Endpoint: POST /v1.0/teams/{team-id}/channels
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania kanału</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi kanału</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelData">Dane kanału do utworzenia</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Utworzony kanał lub null w przypadku błędu</returns>
        Task<TResponse?> CreateTeamChannelAsync<TRequest, TResponse>(
            string teamId, 
            TRequest channelData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Aktualizuje kanał zespołu Microsoft Teams
        /// Endpoint: PATCH /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania aktualizacji</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi kanału</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <param name="updateData">Dane do aktualizacji</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zaktualizowany kanał lub null w przypadku błędu</returns>
        Task<TResponse?> UpdateTeamChannelAsync<TRequest, TResponse>(
            string teamId, 
            string channelId, 
            TRequest updateData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Usuwa kanał z zespołu Microsoft Teams
        /// Endpoint: DELETE /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> DeleteTeamChannelAsync(
            string teamId, 
            string channelId, 
            string? accessToken = null);

        /// <summary>
        /// Pobiera kanał zespołu Microsoft Teams
        /// Endpoint: GET /v1.0/teams/{team-id}/channels/{channel-id}
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kanału</typeparam>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Kanał lub null jeśli nie znaleziono</returns>
        Task<TResponse?> GetTeamChannelAsync<TResponse>(
            string teamId, 
            string channelId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Tworzy nowego użytkownika
        /// Endpoint: POST /v1.0/users
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania użytkownika</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi użytkownika</typeparam>
        /// <param name="userData">Dane użytkownika do utworzenia</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Utworzony użytkownik lub null w przypadku błędu</returns>
        Task<TResponse?> CreateUserAsync<TRequest, TResponse>(
            TRequest userData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Aktualizuje użytkownika
        /// Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania aktualizacji</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi użytkownika</typeparam>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="updateData">Dane do aktualizacji</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zaktualizowany użytkownik lub null w przypadku błędu</returns>
        Task<TResponse?> UpdateUserAsync<TRequest, TResponse>(
            string userId, 
            TRequest updateData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Pobiera użytkownika
        /// Endpoint: GET /v1.0/users/{user-id}
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi użytkownika</typeparam>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Użytkownik lub null jeśli nie znaleziono</returns>
        Task<TResponse?> GetUserAsync<TResponse>(
            string userId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera wszystkich użytkowników
        /// Endpoint: GET /v1.0/users
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji użytkowników</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista użytkowników lub null w przypadku błędu</returns>
        Task<TResponse?> GetAllUsersAsync<TResponse>(string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Usuwa użytkownika
        /// Endpoint: DELETE /v1.0/users/{user-id}
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> DeleteUserAsync(string userId, string? accessToken = null);

        /// <summary>
        /// Przypisuje licencję użytkownikowi
        /// Endpoint: POST /v1.0/users/{user-id}/assignLicense
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania licencji</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi użytkownika</typeparam>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="licenseData">Dane licencji do przypisania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Użytkownik z przypisaną licencją lub null w przypadku błędu</returns>
        Task<TResponse?> AssignUserLicenseAsync<TRequest, TResponse>(
            string userId, 
            TRequest licenseData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Pobiera licencje użytkownika
        /// Endpoint: GET /v1.0/users/{user-id}/licenseDetails
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi licencji</typeparam>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista licencji lub null w przypadku błędu</returns>
        Task<TResponse?> GetUserLicensesAsync<TResponse>(
            string userId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Unieważnia wszystkie sesje logowania użytkownika
        /// Endpoint: POST /v1.0/users/{user-id}/revokeSignInSessions
        /// </summary>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> RevokeUserSignInSessionsAsync(string userId, string? accessToken = null);

        /// <summary>
        /// Pobiera użytkowników z określonego działu
        /// Endpoint: GET /v1.0/users?$filter=department eq '{department}'
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji użytkowników</typeparam>
        /// <param name="department">Nazwa działu</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista użytkowników z działu lub null w przypadku błędu</returns>
        Task<TResponse?> GetUsersByDepartmentAsync<TResponse>(
            string department, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera użytkowników nieaktywnych przez określoną liczbę dni
        /// Endpoint: GET /v1.0/users?$filter=signInActivity/lastSignInDateTime le {date}
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji użytkowników</typeparam>
        /// <param name="daysInactive">Liczba dni nieaktywności</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista nieaktywnych użytkowników lub null w przypadku błędu</returns>
        Task<TResponse?> GetInactiveUsersAsync<TResponse>(
            int daysInactive, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera zespoły użytkownika (członkostwo)
        /// Endpoint: GET /v1.0/users/{user-id}/joinedTeams
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji zespołów</typeparam>
        /// <param name="userId">ID użytkownika</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista zespołów użytkownika lub null w przypadku błędu</returns>
        Task<TResponse?> GetUserTeamsAsync<TResponse>(
            string userId, 
            string? accessToken = null) 
            where TResponse : class;

        // ===== GROUPS API METHODS =====

        /// <summary>
        /// Tworzy nową grupę
        /// Endpoint: POST /v1.0/groups
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania grupy</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi grupy</typeparam>
        /// <param name="groupData">Dane grupy do utworzenia</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Utworzona grupa lub null w przypadku błędu</returns>
        Task<TResponse?> CreateGroupAsync<TRequest, TResponse>(
            TRequest groupData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Aktualizuje grupę
        /// Endpoint: PATCH /v1.0/groups/{group-id}
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania aktualizacji</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi grupy</typeparam>
        /// <param name="groupId">ID grupy</param>
        /// <param name="updateData">Dane do aktualizacji</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Zaktualizowana grupa lub null w przypadku błędu</returns>
        Task<TResponse?> UpdateGroupAsync<TRequest, TResponse>(
            string groupId, 
            TRequest updateData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Pobiera grupę
        /// Endpoint: GET /v1.0/groups/{group-id}
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi grupy</typeparam>
        /// <param name="groupId">ID grupy</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Grupa lub null jeśli nie znaleziono</returns>
        Task<TResponse?> GetGroupAsync<TResponse>(
            string groupId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera wszystkie grupy
        /// Endpoint: GET /v1.0/groups
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji grup</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista grup lub null w przypadku błędu</returns>
        Task<TResponse?> GetAllGroupsAsync<TResponse>(string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Usuwa grupę
        /// Endpoint: DELETE /v1.0/groups/{group-id}
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> DeleteGroupAsync(string groupId, string? accessToken = null);

        // ===== GROUP MEMBERS MANAGEMENT =====

        /// <summary>
        /// Pobiera członków grupy
        /// Endpoint: GET /v1.0/groups/{group-id}/members
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi członków</typeparam>
        /// <param name="groupId">ID grupy</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista członków lub null w przypadku błędu</returns>
        Task<TResponse?> GetGroupMembersAsync<TResponse>(
            string groupId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Dodaje członka do grupy
        /// Endpoint: POST /v1.0/groups/{group-id}/members/$ref
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <param name="userId">ID użytkownika do dodania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> AddGroupMemberAsync(
            string groupId, 
            string userId, 
            string? accessToken = null);

        /// <summary>
        /// Usuwa członka z grupy
        /// Endpoint: DELETE /v1.0/groups/{group-id}/members/{user-id}/$ref
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <param name="userId">ID użytkownika do usunięcia</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> RemoveGroupMemberAsync(
            string groupId, 
            string userId, 
            string? accessToken = null);

        // ===== GROUP OWNERS MANAGEMENT =====

        /// <summary>
        /// Pobiera właścicieli grupy
        /// Endpoint: GET /v1.0/groups/{group-id}/owners
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi właścicieli</typeparam>
        /// <param name="groupId">ID grupy</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista właścicieli lub null w przypadku błędu</returns>
        Task<TResponse?> GetGroupOwnersAsync<TResponse>(
            string groupId, 
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Dodaje właściciela do grupy
        /// Endpoint: POST /v1.0/groups/{group-id}/owners/$ref
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <param name="userId">ID użytkownika do dodania jako właściciel</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> AddGroupOwnerAsync(
            string groupId, 
            string userId, 
            string? accessToken = null);

        /// <summary>
        /// Usuwa właściciela z grupy
        /// Endpoint: DELETE /v1.0/groups/{group-id}/owners/{user-id}/$ref
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <param name="userId">ID użytkownika do usunięcia jako właściciel</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli operacja zakończyła się sukcesem</returns>
        Task<bool> RemoveGroupOwnerAsync(
            string groupId, 
            string userId, 
            string? accessToken = null);

        // ===== GROUP FILTERING OPERATIONS =====

        /// <summary>
        /// Pobiera grupy Microsoft 365 (Unified Groups)
        /// Endpoint: GET /v1.0/groups?$filter=groupTypes/any(c:c eq 'Unified')
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji grup</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista grup Microsoft 365 lub null w przypadku błędu</returns>
        Task<TResponse?> GetMicrosoft365GroupsAsync<TResponse>(string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera grupy zabezpieczeń (Security Groups)
        /// Endpoint: GET /v1.0/groups?$filter=securityEnabled eq true
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji grup</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista grup zabezpieczeń lub null w przypadku błędu</returns>
        Task<TResponse?> GetSecurityGroupsAsync<TResponse>(string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera grupy dystrybucyjne (Distribution Groups)
        /// Endpoint: GET /v1.0/groups?$filter=mailEnabled eq true and securityEnabled eq false
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji grup</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Lista grup dystrybucyjnych lub null w przypadku błędu</returns>
        Task<TResponse?> GetDistributionGroupsAsync<TResponse>(string? accessToken = null) 
            where TResponse : class;

        // ===== GROUP TEAMS RELATIONSHIP =====

        /// <summary>
        /// Sprawdza czy grupa ma zespół Teams
        /// Endpoint: GET /v1.0/teams/{group-id} (używa try-catch dla 404)
        /// </summary>
        /// <param name="groupId">ID grupy</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli grupa ma zespół Teams</returns>
        Task<bool> GroupHasTeamAsync(string groupId, string? accessToken = null);

        // ===== BATCH OPERATIONS =====

        /// <summary>
        /// Wykonuje równoległe żądania GET wykorzystując Graph Batch API
        /// Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi</typeparam>
        /// <param name="endpoints">Lista endpointów do wywołania równolegle</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <param name="batchSize">Maksymalny rozmiar batcha (domyślnie 20)</param>
        /// <returns>Lista wyników batch operations</returns>
        Task<IEnumerable<TResponse?>> ExecuteParallelGetRequestsAsync<TResponse>(
            IEnumerable<string> endpoints, 
            string? accessToken = null,
            int batchSize = 20) 
            where TResponse : class;

        /// <summary>
        /// Wykonuje równoległe żądania POST wykorzystując Graph Batch API
        /// Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi</typeparam>
        /// <param name="operations">Lista operacji POST do wykonania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <param name="batchSize">Maksymalny rozmiar batcha (domyślnie 20)</param>
        /// <returns>Lista wyników batch operations</returns>
        Task<IEnumerable<TResponse?>> ExecuteParallelPostRequestsAsync<TRequest, TResponse>(
            IEnumerable<(string endpoint, TRequest data)> operations, 
            string? accessToken = null,
            int batchSize = 20) 
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Wykonuje równoległe żądania PATCH wykorzystując Graph Batch API
        /// Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi</typeparam>
        /// <param name="operations">Lista operacji PATCH do wykonania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <param name="batchSize">Maksymalny rozmiar batcha (domyślnie 20)</param>
        /// <returns>Lista wyników batch operations</returns>
        Task<IEnumerable<TResponse?>> ExecuteParallelPatchRequestsAsync<TRequest, TResponse>(
            IEnumerable<(string endpoint, TRequest data)> operations, 
            string? accessToken = null,
            int batchSize = 20) 
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Wykonuje równoległe żądania DELETE wykorzystując Graph Batch API
        /// Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <param name="endpoints">Lista endpointów do usunięcia</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <param name="batchSize">Maksymalny rozmiar batcha (domyślnie 20)</param>
        /// <returns>Lista wyników operacji DELETE (true/false)</returns>
        Task<IEnumerable<bool>> ExecuteParallelDeleteRequestsAsync(
            IEnumerable<string> endpoints, 
            string? accessToken = null,
            int batchSize = 20);

        // ===== BULK OPERATIONS WITH PROGRESS REPORTING =====

        /// <summary>
        /// Wykonuje bulk operations na użytkownikach z progress reporting
        /// Wykorzystuje SemaphoreSlim dla kontroli współbieżności (5 concurrent operations)
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi</typeparam>
        /// <param name="operations">Lista operacji użytkowników</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Wyniki bulk operations z szczegółowymi statystykami</returns>
        Task<(int TotalOperations, int SuccessfulOperations, int FailedOperations, 
               IEnumerable<TResponse?> Results, IEnumerable<string> Errors, DateTime CompletedAt)> 
            ExecuteBulkUserOperationsAsync<TRequest, TResponse>(
                IEnumerable<(string operationType, string endpoint, TRequest? data)> operations,
                IProgress<(int completed, int total, string currentOperation)>? progress = null,
                string? accessToken = null)
                where TRequest : class 
                where TResponse : class;

        /// <summary>
        /// Wykonuje bulk operations na zespołach z progress reporting
        /// Wykorzystuje SemaphoreSlim dla kontroli współbieżności (3 concurrent operations)
        /// Obsługuje specjalne operacje: ARCHIVE, UNARCHIVE z rate limiting (500ms delay)
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi</typeparam>
        /// <param name="operations">Lista operacji zespołów</param>
        /// <param name="progress">Progress reporter</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Wyniki bulk operations z szczegółowymi statystykami</returns>
        Task<(int TotalOperations, int SuccessfulOperations, int FailedOperations, 
               IEnumerable<TResponse?> Results, IEnumerable<string> Errors, DateTime CompletedAt)> 
            ExecuteBulkTeamOperationsAsync<TRequest, TResponse>(
                IEnumerable<(string operationType, string endpoint, TRequest? data)> operations,
                IProgress<(int completed, int total, string currentOperation)>? progress = null,
                string? accessToken = null)
                where TRequest : class 
                where TResponse : class;

        // ===== MAIL API METHODS =====

        /// <summary>
        /// Wysyła email przez Microsoft Graph Mail API
        /// Endpoint: POST /v1.0/me/sendMail
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania email</typeparam>
        /// <param name="emailData">Dane email do wysłania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli email został wysłany pomyślnie</returns>
        Task<bool> SendMailAsync<TRequest>(
            TRequest emailData, 
            string? accessToken = null)
            where TRequest : class;

        /// <summary>
        /// Wysyła email w imieniu określonego użytkownika przez Microsoft Graph Mail API
        /// Endpoint: POST /v1.0/users/{user-id}/sendMail
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania email</typeparam>
        /// <param name="userId">ID użytkownika w imieniu którego wysyłamy email</param>
        /// <param name="emailData">Dane email do wysłania</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>True jeśli email został wysłany pomyślnie</returns>
        Task<bool> SendMailOnBehalfOfUserAsync<TRequest>(
            string userId,
            TRequest emailData, 
            string? accessToken = null)
            where TRequest : class;

        /// <summary>
        /// Tworzy draft email przez Microsoft Graph Mail API
        /// Endpoint: POST /v1.0/me/messages
        /// </summary>
        /// <typeparam name="TRequest">Typ żądania email</typeparam>
        /// <typeparam name="TResponse">Typ odpowiedzi message</typeparam>
        /// <param name="emailData">Dane email do utworzenia jako draft</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Utworzony draft message lub null w przypadku błędu</returns>
        Task<TResponse?> CreateDraftEmailAsync<TRequest, TResponse>(
            TRequest emailData, 
            string? accessToken = null)
            where TRequest : class 
            where TResponse : class;

        /// <summary>
        /// Pobiera wiadomości email z skrzynki użytkownika
        /// Endpoint: GET /v1.0/me/messages
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi kolekcji wiadomości</typeparam>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <param name="filter">Opcjonalny filtr OData</param>
        /// <param name="select">Opcjonalne pola do pobrania</param>
        /// <param name="top">Maksymalna liczba wiadomości do pobrania</param>
        /// <returns>Lista wiadomości email lub null w przypadku błędu</returns>
        Task<TResponse?> GetMailMessagesAsync<TResponse>(
            string? accessToken = null,
            string? filter = null,
            string? select = null,
            int? top = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera konkretną wiadomość email
        /// Endpoint: GET /v1.0/me/messages/{message-id}
        /// </summary>
        /// <typeparam name="TResponse">Typ odpowiedzi wiadomości</typeparam>
        /// <param name="messageId">ID wiadomości</param>
        /// <param name="accessToken">Token dostępu (opcjonalny)</param>
        /// <returns>Wiadomość email lub null jeśli nie znaleziono</returns>
        Task<TResponse?> GetMailMessageAsync<TResponse>(
            string messageId,
            string? accessToken = null) 
            where TResponse : class;

        /// <summary>
        /// Pobiera aktualny token dostępu.
        /// Używane w GraphBulkOperationsService i innych serwisach.
        /// </summary>
        /// <returns>Token dostępu lub null jeśli niedostępny</returns>
        Task<string?> GetAccessTokenAsync();

        /// <summary>
        /// ✅ NAPRAWKA OBO: Ustawia token OBO do użycia w żądaniach Graph API
        /// </summary>
        /// <param name="oboAccessToken">Token OBO otrzymany z middleware</param>
        void SetOboToken(string oboAccessToken);

        // ===== WERSJE GENERIC METOD HTTP =====

        /// <summary>
        /// Wykonuje żądanie GET zwracające obiekt typu T
        /// </summary>
        /// <typeparam name="T">Typ zwracanego obiektu</typeparam>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>Obiekt typu T lub null</returns>
        Task<T?> GetAsync<T>(string url, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie POST z obiektem typu TRequest zwracające obiekt typu TResponse
        /// </summary>
        /// <typeparam name="TRequest">Typ obiektu żądania</typeparam>
        /// <typeparam name="TResponse">Typ obiektu odpowiedzi</typeparam>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="content">Obiekt do wysłania</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>Obiekt typu TResponse lub null</returns>
        Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest content, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie PATCH z obiektem typu T
        /// </summary>
        /// <typeparam name="T">Typ obiektu żądania</typeparam>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="content">Obiekt do wysłania</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>Task</returns>
        Task PatchAsync<T>(string url, T content, Dictionary<string, string>? headers = null);

        /// <summary>
        /// Wykonuje żądanie PUT z obiektem typu T
        /// </summary>
        /// <typeparam name="T">Typ obiektu żądania</typeparam>
        /// <param name="url">Pełny URL lub endpoint</param>
        /// <param name="content">Obiekt do wysłania</param>
        /// <param name="headers">Opcjonalne nagłówki</param>
        /// <returns>Task</returns>
        Task PutAsync<T>(string url, T content, Dictionary<string, string>? headers = null);
    }
} 