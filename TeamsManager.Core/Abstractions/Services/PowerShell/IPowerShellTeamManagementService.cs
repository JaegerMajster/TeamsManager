using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Abstractions.Services.PowerShell
{
    /// <summary>
    /// Serwis zarządzający zespołami i kanałami w Microsoft Teams przez PowerShell
    /// </summary>
    public interface IPowerShellTeamManagementService
    {
        #region Team Operations

        /// <summary>
        /// Tworzy nowy zespół w Microsoft Teams
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana zespołu</param>
        /// <param name="description">Opis zespołu</param>
        /// <param name="ownerUpn">UPN właściciela zespołu</param>
        /// <param name="visibility">Widoczność zespołu (Private/Public)</param>
        /// <param name="template">Szablon zespołu (opcjonalny)</param>
        /// <returns>ID utworzonego zespołu lub null w przypadku błędu</returns>
        Task<string?> CreateTeamAsync(
            string displayName,
            string description,
            string ownerUpn,
            TeamVisibility visibility = TeamVisibility.Private,
            string? template = null);

        /// <summary>
        /// Aktualizuje właściwości zespołu
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
        /// </summary>
        /// <param name="teamId">ID zespołu do archiwizacji</param>
        /// <returns>True jeśli archiwizacja się powiodła</returns>
        Task<bool> ArchiveTeamAsync(string teamId);

        /// <summary>
        /// Przywraca zespół z archiwum
        /// </summary>
        /// <param name="teamId">ID zespołu do przywrócenia</param>
        /// <returns>True jeśli przywrócenie się powiodło</returns>
        Task<bool> UnarchiveTeamAsync(string teamId);

        /// <summary>
        /// Usuwa zespół
        /// </summary>
        /// <param name="teamId">ID zespołu do usunięcia</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> DeleteTeamAsync(string teamId);

        /// <summary>
        /// Pobiera szczegóły zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>Obiekt PSObject z danymi zespołu lub null</returns>
        Task<PSObject?> GetTeamAsync(string teamId);

        /// <summary>
        /// Pobiera wszystkie zespoły
        /// </summary>
        /// <returns>Kolekcja zespołów lub null</returns>
        Task<Collection<PSObject>?> GetAllTeamsAsync();

        /// <summary>
        /// Pobiera zespoły należące do określonego właściciela
        /// </summary>
        /// <param name="ownerUpn">UPN właściciela</param>
        /// <returns>Kolekcja zespołów lub null</returns>
        Task<Collection<PSObject>?> GetTeamsByOwnerAsync(string ownerUpn);

        #endregion

        #region Team Member Management - Critical P0 Methods

        /// <summary>
        /// Pobiera wszystkich członków zespołu z cache i walidacją (P0-CRITICAL)
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <returns>Kolekcja członków zespołu z rolami</returns>
        Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId);

        /// <summary>
        /// Pobiera pojedynczego członka zespołu z walidacją (P0-CRITICAL)
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <returns>Informacje o członku zespołu lub null jeśli nie jest członkiem</returns>
        Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn);

        /// <summary>
        /// Zmienia rolę członka zespołu (Owner to Member) (P0-CRITICAL)
        /// </summary>
        /// <param name="teamId">ID zespołu (GUID)</param>
        /// <param name="userUpn">UPN użytkownika</param>
        /// <param name="newRole">Nowa rola: Owner lub Member</param>
        /// <returns>True jeśli operacja się powiodła</returns>
        Task<bool> UpdateTeamMemberRoleAsync(string teamId, string userUpn, string newRole);

        /// <summary>
        /// Weryfikuje uprawnienia Microsoft.Graph dla operacji Team Members
        /// ETAP 5/6: Diagnostyka uprawnień Graph API
        /// </summary>
        /// <returns>True jeśli wymagane uprawnienia są dostępne</returns>
        Task<bool> VerifyGraphPermissionsAsync();

        #endregion

        #region Diagnostic Operations

        /// <summary>
        /// [ETAP3] Testuje połączenie z Microsoft Graph
        /// </summary>
        /// <returns>True jeśli połączenie aktywne</returns>
        Task<bool> TestConnectionAsync();

        /// <summary>
        /// [ETAP3] Waliduje uprawnienia Graph API
        /// </summary>
        /// <returns>Słownik uprawnień i ich statusu</returns>
        Task<Dictionary<string, bool>> ValidatePermissionsAsync();

        #endregion

        #region Channel Operations

        /// <summary>
        /// Tworzy nowy kanał w zespole
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="displayName">Nazwa kanału</param>
        /// <param name="isPrivate">Czy kanał ma być prywatny</param>
        /// <param name="description">Opis kanału (opcjonalny)</param>
        /// <returns>Obiekt PSObject z danymi kanału lub null</returns>
        Task<PSObject?> CreateTeamChannelAsync(
            string teamId, 
            string displayName, 
            bool isPrivate = false, 
            string? description = null);

        /// <summary>
        /// Aktualizuje właściwości kanału
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
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <returns>True jeśli usunięcie się powiodło</returns>
        Task<bool> RemoveTeamChannelAsync(string teamId, string channelId);

        /// <summary>
        /// Pobiera wszystkie kanały zespołu
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <returns>Kolekcja kanałów lub null</returns>
        Task<Collection<PSObject>?> GetTeamChannelsAsync(string teamId);

        /// <summary>
        /// Pobiera kanał zespołu po nazwie
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelDisplayName">Nazwa kanału</param>
        /// <returns>Obiekt PSObject z danymi kanału lub null</returns>
        Task<PSObject?> GetTeamChannelAsync(string teamId, string channelDisplayName);

        /// <summary>
        /// Pobiera kanał zespołu po jego ID
        /// </summary>
        /// <param name="teamId">ID zespołu</param>
        /// <param name="channelId">ID kanału</param>
        /// <returns>Obiekt kanału lub null</returns>
        Task<PSObject?> GetTeamChannelByIdAsync(string teamId, string channelId);

        #endregion
    }
}