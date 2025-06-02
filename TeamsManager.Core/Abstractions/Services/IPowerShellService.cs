// Plik: TeamsManager.Core/Abstractions/Services/IPowerShellService.cs
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models; // Potencjalnie niepotrzebne bezpośrednio w tym interfejsie
using System.Collections.Generic;
using System;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za wykonywanie operacji PowerShell,
    /// w szczególności związanych z Microsoft Teams i zarządzaniem użytkownikami M365.
    /// </summary>
    public interface IPowerShellService : IDisposable
    {
        /// <summary>
        /// Sprawdza, czy połączenie z Microsoft Graph (przez PowerShell) jest aktywne.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Asynchronicznie nawiązuje połączenie z Microsoft Graph API przy użyciu tokenu dostępu OAuth 2.0.
        /// </summary>
        /// <param name="accessToken">Token dostępu OAuth 2.0.</param>
        /// <param name="scopes">Opcjonalna lista scopes (uprawnień).</param>
        /// <returns>True, jeśli połączenie zostało pomyślnie nawiązane; w przeciwnym razie false.</returns>
        Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null);

        /// <summary>
        /// Asynchronicznie tworzy nowy zespół w Microsoft Teams.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana zespołu.</param>
        /// <param name="description">Opis zespołu.</param>
        /// <param name="ownerUpn">UPN właściciela zespołu.</param>
        /// <param name="visibility">Widoczność zespołu.</param>
        /// <param name="template">Opcjonalny szablon Microsoft Teams do użycia.</param>
        /// <returns>Zewnętrzny identyfikator (GroupId) utworzonego zespołu lub null w przypadku błędu.</returns>
        Task<string?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility = TeamVisibility.Private, string? template = null);

        /// <summary>
        /// Asynchronicznie aktualizuje właściwości istniejącego zespołu w Microsoft Teams.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="newDisplayName">Nowa nazwa wyświetlana zespołu (opcjonalna).</param>
        /// <param name="newDescription">Nowy opis zespołu (opcjonalny).</param>
        /// <param name="newVisibility">Nowa widoczność zespołu (opcjonalna).</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> UpdateTeamPropertiesAsync(string teamId, string? newDisplayName = null, string? newDescription = null, TeamVisibility? newVisibility = null);

        /// <summary>
        /// Asynchronicznie archiwizuje istniejący zespół w Microsoft Teams.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu do zarchiwizowania.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> ArchiveTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie przywraca zarchiwizowany zespół w Microsoft Teams.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu do przywrócenia.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> UnarchiveTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie usuwa zespół w Microsoft Teams.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu do usunięcia.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> DeleteTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie dodaje użytkownika do zespołu w Microsoft Teams.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="userUpn">UPN użytkownika do dodania.</param>
        /// <param name="role">Rola użytkownika w zespole ("Owner" lub "Member").</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role);

        /// <summary>
        /// Asynchronicznie usuwa użytkownika z zespołu w Microsoft Teams.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="userUpn">UPN użytkownika do usunięcia.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn);

        /// <summary>
        /// Asynchronicznie tworzy nowego użytkownika w Microsoft 365.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana użytkownika.</param>
        /// <param name="userPrincipalName">UPN nowego użytkownika.</param>
        /// <param name="password">Hasło dla nowego użytkownika.</param>
        /// <param name="usageLocation">Lokalizacja użycia (np. "PL").</param>
        /// <param name="licenseSkuIds">Lista ID SKU licencji do przypisania (opcjonalna).</param>
        /// <param name="accountEnabled">Czy konto ma być od razu włączone.</param>
        /// <returns>Zewnętrzny identyfikator (ObjectId) utworzonego użytkownika lub null w przypadku błędu.</returns>
        Task<string?> CreateM365UserAsync(string displayName, string userPrincipalName, string password, string usageLocation = "PL", List<string>? licenseSkuIds = null, bool accountEnabled = true);

        /// <summary>
        /// Asynchronicznie zmienia stan konta użytkownika w Microsoft 365 (włączone/wyłączone).
        /// </summary>
        /// <param name="userPrincipalName">UPN użytkownika.</param>
        /// <param name="isEnabled">True, aby włączyć konto; false, aby wyłączyć.</param>
        /// <returns>True, jeśli operacja się powiodła.</returns>
        Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled);

        /// <summary>
        /// Asynchronicznie aktualizuje User Principal Name (UPN) użytkownika w Microsoft 365.
        /// </summary>
        /// <param name="currentUpn">Bieżący UPN użytkownika.</param>
        /// <param name="newUpn">Nowy UPN dla użytkownika.</param>
        /// <returns>True, jeśli operacja się powiodła.</returns>
        Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn);

        /// <summary>
        /// Asynchronicznie aktualizuje wybrane właściwości użytkownika w Microsoft 365.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika do aktualizacji.</param>
        /// <param name="department">Nowy dział (opcjonalnie).</param>
        /// <param name="jobTitle">Nowe stanowisko (opcjonalnie).</param>
        /// <param name="firstName">Nowe imię (opcjonalnie).</param>
        /// <param name="lastName">Nowe nazwisko (opcjonalnie).</param>
        /// <returns>True, jeśli operacja się powiodła.</returns>
        Task<bool> UpdateM365UserPropertiesAsync(string userUpn, string? department = null, string? jobTitle = null, string? firstName = null, string? lastName = null);

        /// <summary>
        /// Asynchronicznie pobiera konkretny kanał zespołu na podstawie jego nazwy wyświetlanej.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="channelDisplayName">Nazwa wyświetlana kanału.</param>
        /// <returns>Obiekt PSObject reprezentujący kanał lub null, jeśli nie znaleziono lub w przypadku błędu.</returns>
        Task<PSObject?> GetTeamChannelAsync(string teamId, string channelDisplayName);

        /// <summary>
        /// Asynchronicznie tworzy nowy kanał w zespole.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="displayName">Nazwa wyświetlana nowego kanału.</param>
        /// <param name="isPrivate">Czy kanał ma być prywatny (domyślnie false - standardowy).</param>
        /// <param name="description">Opcjonalny opis kanału.</param>
        /// <returns>Obiekt PSObject reprezentujący utworzony kanał lub null w przypadku błędu.</returns>
        Task<PSObject?> CreateTeamChannelAsync(string teamId, string displayName, bool isPrivate = false, string? description = null);

        /// <summary>
        /// Asynchronicznie aktualizuje właściwości istniejącego kanału w zespole.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="channelId">Identyfikator (Graph ID) kanału, który ma być zaktualizowany.</param> // ZMIANA
        /// <param name="newDisplayName">Nowa nazwa wyświetlana kanału (opcjonalna).</param>
        /// <param name="newDescription">Nowy opis kanału (opcjonalny).</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> UpdateTeamChannelAsync(string teamId, string channelId, string? newDisplayName = null, string? newDescription = null); // ZMIANA

        /// <summary>
        /// Asynchronicznie usuwa kanał z zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="channelId">Identyfikator (Graph ID) kanału do usunięcia.</param> // ZMIANA
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> RemoveTeamChannelAsync(string teamId, string channelId); // ZMIANA

        /// <summary>
        /// Asynchronicznie pobiera wszystkie kanały dla określonego zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <returns>Kolekcja obiektów PSObject reprezentujących kanały lub null w przypadku błędu.</returns>
        Task<Collection<PSObject>?> GetTeamChannelsAsync(string teamId);

        Task<PSObject?> GetTeamAsync(string teamId);
        Task<Collection<PSObject>?> GetAllTeamsAsync();
        Task<Collection<PSObject>?> GetTeamsByOwnerAsync(string ownerUpn);
        Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId);
        Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn);
        Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId);
        Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId);
        Task<Collection<PSObject>?> GetUserLicensesAsync(string userUpn);
        Task<Collection<PSObject>?> GetAllUsersAsync(string? filter = null);
        Task<Collection<PSObject>?> GetInactiveUsersAsync(int daysInactive);
        Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(string teamId, List<string> userUpns, string role = "Member");
        Task<Dictionary<string, bool>> BulkArchiveTeamsAsync(List<string> teamIds);
        Task<Dictionary<string, bool>> BulkRemoveUsersFromTeamAsync(string teamId, List<string> userUpns);
        Task<Dictionary<string, bool>> BulkUpdateUserPropertiesAsync(Dictionary<string, Dictionary<string, string>> userUpdates);
        Task<Collection<PSObject>?> FindDuplicateUsersAsync();
        Task<bool> ArchiveTeamAndDeactivateExclusiveUsersAsync(string teamId);
        Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null);
    }
}