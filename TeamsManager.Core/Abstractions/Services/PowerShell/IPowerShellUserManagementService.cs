using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services.PowerShell
{
    /// <summary>
    /// Serwis zarządzający użytkownikami, członkostwem w zespołach i licencjami w Microsoft 365 przez PowerShell
    /// </summary>
    public interface IPowerShellUserManagementService
    {
        #region User Operations

        /// <summary>
        /// Tworzy nowego użytkownika w Microsoft 365
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana użytkownika</param>
        /// <param name="userPrincipalName">UPN użytkownika</param>
        /// <param name="password">Hasło użytkownika</param>
        /// <param name="usageLocation">Lokalizacja użytkownika (domyślnie PL)</param>
        /// <param name="licenseSkuIds">Lista ID licencji do przypisania</param>
        /// <param name="accountEnabled">Czy konto ma być aktywne</param>
        /// <returns>ID utworzonego użytkownika lub null w przypadku błędu</returns>
        Task<string?> CreateM365UserAsync(
            string displayName,
            string userPrincipalName,
            string password,
            string? usageLocation = null,
            List<string>? licenseSkuIds = null,
            bool accountEnabled = true);

        /// <summary>
        /// Ustawia stan konta użytkownika (włączone/wyłączone)
        /// </summary>
        /// <param name="userPrincipalName">UPN użytkownika</param>
        /// <param name="isEnabled">Czy konto ma być włączone</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled);

        /// <summary>
        /// Aktualizuje UPN użytkownika
        /// </summary>
        /// <param name="currentUpn">Obecny UPN</param>
        /// <param name="newUpn">Nowy UPN</param>
        /// <returns>True jeśli aktualizacja się powiodła</returns>
        Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn);

        /// <summary>
        /// Aktualizuje właściwości użytkownika
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
        /// </summary>
        /// <param name="filter">Filtr OData (opcjonalny)</param>
        /// <returns>Kolekcja użytkowników lub null</returns>
        Task<Collection<PSObject>?> GetAllUsersAsync(string? filter = null);

        /// <summary>
        /// Pobiera użytkowników nieaktywnych przez określoną liczbę dni
        /// </summary>
        /// <param name="daysInactive">Liczba dni nieaktywności</param>
        /// <returns>Kolekcja nieaktywnych użytkowników lub null</returns>
        Task<Collection<PSObject>?> GetInactiveUsersAsync(int daysInactive);

        /// <summary>
        /// Wyszukuje duplikaty użytkowników
        /// </summary>
        /// <returns>Kolekcja duplikatów lub null</returns>
        Task<Collection<PSObject>?> FindDuplicateUsersAsync();

        /// <summary>
        /// Pobiera szczegóły użytkownika z Microsoft 365 na podstawie jego unikalnego ID (ObjectId)
        /// </summary>
        /// <param name="userId">Unikalny identyfikator użytkownika (ObjectId)</param>
        /// <returns>Obiekt PSObject z danymi użytkownika lub null</returns>
        Task<PSObject?> GetM365UserByIdAsync(string userId);

        /// <summary>
        /// Pobiera wszystkich użytkowników, których konto jest włączone/wyłączone
        /// </summary>
        /// <param name="accountEnabled">Czy konto ma być włączone (true) czy wyłączone (false)</param>
        /// <returns>Kolekcja użytkowników lub null</returns>
        Task<Collection<PSObject>?> GetM365UsersByAccountEnabledStateAsync(bool accountEnabled);

        #endregion

        #region Team Membership Operations

        /// <summary>
        /// Dodaje użytkownika do zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="role">Rola użytkownika (Owner/Member)</param>
        /// <returns>True jeśli dodanie się powiodło</returns>
        Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role);

        /// <summary>
        /// Usuwa użytkownika z zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn);

        /// <summary>
        /// Pobiera wszystkich członków zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>Kolekcja członków lub null</returns>
        Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId);

        /// <summary>
        /// Pobiera członka zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>Obiekt PSObject z danymi członka lub null</returns>
        Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn);

        #endregion

        #region License Operations

        /// <summary>
        /// Przypisuje licencję do użytkownika
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="licenseSkuId">ID licencji SKU</param>
        /// <returns>True jeśli przypisanie się powiodło</returns>
        Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId);

        /// <summary>
        /// Usuwa licencję od użytkownika
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="licenseSkuId">ID licencji SKU</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId);

        /// <summary>
        /// Pobiera licencje użytkownika
        /// </summary>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>Kolekcja licencji lub null</returns>
        Task<Collection<PSObject>?> GetUserLicensesAsync(string userUpn);

        #endregion
    }
}