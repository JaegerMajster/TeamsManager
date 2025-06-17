using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Serwis zarządzający użytkownikami, członkostwem w zespołach i licencjami w Microsoft 365 przez Graph API
    /// Implementacja zarządzania użytkownikami z pełnym wsparciem dla Graph API endpoints
    /// </summary>
    public interface IGraphUserManagementService
    {
        #region User Operations

        /// <summary>
        /// Sprawdza czy system ma odpowiednie uprawnienia do tworzenia użytkowników
        /// Graph API Endpoint: GET /v1.0/me/memberOf (permissions check)
        /// </summary>
        /// <returns>True jeśli ma uprawnienia, false w przeciwnym razie</returns>
        Task<bool> ValidateUserCreationPermissionsAsync();

        /// <summary>
        /// Tworzy nowego użytkownika w Microsoft 365
        /// Graph API Endpoint: POST /v1.0/users
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana użytkownika</param>
        /// <param name="userPrincipalName">UPN użytkownika</param>
        /// <param name="password">Hasło użytkownika</param>
        /// <param name="usageLocation">Lokalizacja użytkownika (domyślnie PL)</param>
        /// <param name="licenseSkuIds">Lista ID licencji do przypisania</param>
        /// <param name="accountEnabled">Czy konto ma być aktywne</param>
        /// <param name="department">Nazwa działu (opcjonalna)</param>
        /// <returns>GraphUser z ID utworzonego użytkownika lub null w przypadku błędu</returns>
        Task<GraphUser?> CreateM365UserAsync(
            string displayName,
            string userPrincipalName,
            string password,
            string? usageLocation = null,
            List<string>? licenseSkuIds = null,
            bool accountEnabled = true,
            string? department = null);

        /// <summary>
        /// Ustawia stan konta użytkownika (włączone/wyłączone)
        /// Graph API Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        /// <param name="userPrincipalName">UPN użytkownika</param>
        /// <param name="isEnabled">Czy konto ma być włączone</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled);

        /// <summary>
        /// Trwale usuwa użytkownika z Microsoft 365 (hard delete)
        /// Graph API Endpoint: DELETE /v1.0/users/{user-id}
        /// UWAGA: Można usuwać tylko dezaktywowanych użytkowników (AccountEnabled = false)
        /// </summary>
        /// <param name="userPrincipalName">UPN użytkownika do usunięcia</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> DeleteM365UserAsync(string userPrincipalName);

        /// <summary>
        /// Aktualizuje UPN użytkownika
        /// Graph API Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        /// <param name="currentUpn">Obecny UPN</param>
        /// <param name="newUpn">Nowy UPN</param>
        /// <returns>True jeśli aktualizacja się powiodła</returns>
        Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn);

        /// <summary>
        /// Aktualizuje właściwości użytkownika
        /// Graph API Endpoint: PATCH /v1.0/users/{user-id}
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="department">Dział (opcjonalny)</param>
        /// <param name="jobTitle">Stanowisko (opcjonalne)</param>
        /// <param name="firstName">Imię (opcjonalne)</param>
        /// <param name="lastName">Nazwisko (opcjonalne)</param>
        /// <returns>True jeśli aktualizacja się powiodła</returns>
        Task<bool> UpdateM365UserPropertiesAsync(
            string userUpn,
            string? department = null,
            string? jobTitle = null,
            string? firstName = null,
            string? lastName = null);

        /// <summary>
        /// Pobiera wszystkich użytkowników
        /// Graph API Endpoint: GET /v1.0/users
        /// </summary>
        /// <param name="filter">Filtr OData (opcjonalny)</param>
        /// <returns>Lista użytkowników lub null</returns>
        Task<List<GraphUser>?> GetAllUsersAsync(string? filter = null);

        /// <summary>
        /// Pobiera użytkowników nieaktywnych przez określoną liczbę dni
        /// Graph API Endpoint: GET /v1.0/users?$filter=signInActivity/lastSignInDateTime le {date}
        /// </summary>
        /// <param name="daysInactive">Liczba dni nieaktywności</param>
        /// <returns>Lista nieaktywnych użytkowników lub null</returns>
        Task<List<GraphUser>?> GetInactiveUsersAsync(int daysInactive);

        /// <summary>
        /// Wyszukuje duplikaty użytkowników
        /// Graph API Endpoint: GET /v1.0/users (z analizą duplikatów)
        /// </summary>
        /// <returns>Lista duplikatów lub null</returns>
        Task<List<GraphUser>?> FindDuplicateUsersAsync();

        /// <summary>
        /// Pobiera szczegóły użytkownika z Microsoft 365 na podstawie jego unikalnego ID (ObjectId)
        /// Graph API Endpoint: GET /v1.0/users/{user-id}
        /// </summary>
        /// <param name="userId">Unikalny identyfikator użytkownika (ObjectId)</param>
        /// <returns>GraphUser z danymi użytkownika lub null</returns>
        Task<GraphUser?> GetM365UserByIdAsync(string userId);

        /// <summary>
        /// Pobiera wszystkich użytkowników, których konto jest włączone/wyłączone
        /// Graph API Endpoint: GET /v1.0/users?$filter=accountEnabled eq {value}
        /// </summary>
        /// <param name="accountEnabled">Czy konto ma być włączone (true) czy wyłączone (false)</param>
        /// <returns>Lista użytkowników lub null</returns>
        Task<List<GraphUser>?> GetM365UsersByAccountEnabledStateAsync(bool accountEnabled);

        /// <summary>
        /// Pobiera użytkownika M365 po UPN z cache i walidacją (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/users/{user-principal-name}
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>GraphUser z informacjami o użytkowniku M365 lub null jeśli nie istnieje</returns>
        Task<GraphUser?> GetM365UserAsync(string userUpn);

        /// <summary>
        /// Wyszukuje użytkowników M365 z walidacją i cache (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/users?$search="displayName:{searchTerm}" OR $filter=startswith(displayName,'{searchTerm}')
        /// </summary>
        /// <param name="searchTerm">Termin wyszukiwania (nazwa lub email)</param>
        /// <returns>Lista użytkowników pasujących do wyszukiwania</returns>
        Task<List<GraphUser>?> SearchM365UsersAsync(string searchTerm);

        /// <summary>
        /// Pobiera użytkowników z określonego działu
        /// Graph API Endpoint: GET /v1.0/users?$filter=department eq '{department}'
        /// </summary>
        /// <param name="department">Nazwa działu</param>
        /// <returns>Lista użytkowników</returns>
        Task<List<GraphUser>?> GetUsersByDepartmentAsync(string department);

        /// <summary>
        /// Wylogowuje użytkownika ze wszystkich sesji
        /// Graph API Endpoint: POST /v1.0/users/{user-id}/revokeSignInSessions
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> RevokeUserSignInSessionsAsync(string userUpn);

        #endregion

        #region Team Membership Operations

        /// <summary>
        /// Dodaje użytkownika do zespołu
        /// Graph API Endpoint: POST /v1.0/teams/{team-id}/members
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="role">Rola użytkownika (Owner/Member)</param>
        /// <returns>True jeśli dodanie się powiodło</returns>
        Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role);

        /// <summary>
        /// Usuwa użytkownika z zespołu
        /// Graph API Endpoint: DELETE /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn);

        /// <summary>
        /// Pobiera wszystkich członków zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>Lista członków lub null</returns>
        Task<List<GraphTeamMember>?> GetTeamMembersAsync(string teamId);

        /// <summary>
        /// Pobiera członka zespołu
        /// Graph API Endpoint: GET /v1.0/teams/{team-id}/members/{membership-id}
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>GraphTeamMember z danymi członka lub null</returns>
        Task<GraphTeamMember?> GetTeamMemberAsync(string teamId, string userUpn);

        #endregion

        #region License Operations

        /// <summary>
        /// Przypisuje licencję do użytkownika
        /// Graph API Endpoint: POST /v1.0/users/{user-id}/assignLicense
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="licenseSkuId">ID licencji SKU</param>
        /// <returns>True jeśli przypisanie się powiodło</returns>
        Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId);

        /// <summary>
        /// Usuwa licencję od użytkownika
        /// Graph API Endpoint: POST /v1.0/users/{user-id}/assignLicense (with removeLicenses)
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="licenseSkuId">ID licencji SKU</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId);

        /// <summary>
        /// Pobiera licencje użytkownika
        /// Graph API Endpoint: GET /v1.0/users/{user-id}/licenseDetails
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>Lista licencji lub null</returns>
        Task<List<License>?> GetUserLicensesAsync(string userUpn);

        /// <summary>
        /// Pobiera dostępne licencje M365 z cache (P0-CRITICAL)
        /// Graph API Endpoint: GET /v1.0/subscribedSkus
        /// </summary>
        /// <returns>Lista dostępnych licencji SKU</returns>
        Task<List<License>?> GetAvailableLicensesAsync();

        #endregion
    }
} 
