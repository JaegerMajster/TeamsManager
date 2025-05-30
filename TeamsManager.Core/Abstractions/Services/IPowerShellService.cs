using System.Collections.ObjectModel; // Dla PSObject
using System.Management.Automation;    // Dla PSObject
using System.Threading.Tasks;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu odpowiedzialnego za wykonywanie operacji PowerShell,
    /// w szczególności związanych z Microsoft Teams.
    /// </summary>
    public interface IPowerShellService : IDisposable
    {
        /// <summary>
        /// Sprawdza, czy połączenie z Microsoft Teams jest aktywne.
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Asynchronicznie nawiązuje połączenie z Microsoft Teams przy użyciu podanych poświadczeń.
        /// </summary>
        /// <param name="username">Nazwa użytkownika (UPN).</param>
        /// <param name="password">Hasło użytkownika.</param>
        /// <returns>True, jeśli połączenie zostało pomyślnie nawiązane; w przeciwnym razie false.</returns>
        Task<bool> ConnectToTeamsAsync(string username, string password);

        // Na razie niech ConnectToTeams będzie synchroniczne, jeśli implementacja PowerShellService jest synchroniczna.
        // Możemy później dodać wersje Async.
        // bool ConnectToTeams(string username, string password);


        /// <summary>
        /// Asynchronicznie tworzy nowy zespół w Microsoft Teams.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana zespołu.</param>
        /// <param name="description">Opis zespołu.</param>
        /// <param name="ownerUpn">UPN właściciela zespołu.</param>
        /// <param name="visibility">Widoczność zespołu (np. "Private", "Public"). Domyślnie "Private".</param>
        /// <param name="template">Opcjonalny szablon Microsoft Teams do użycia (np. "EDU_Class").</param>
        /// <returns>Zewnętrzny identyfikator (GroupId) utworzonego zespołu lub null w przypadku błędu.</returns>
        Task<string?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility = TeamVisibility.Private, string? template = null);
        // string CreateTeam(string displayName, string description, string owner); // Jeśli synchroniczna wersja jest w PowerShellService

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
        Task<bool> UnarchiveTeamAsync(string teamId); // Zmieniono nazwę na Unarchive dla spójności z cmdletem

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
        /// Asynchronicznie wykonuje dowolny skrypt PowerShell i zwraca wyniki.
        /// </summary>
        /// <param name="script">Skrypt do wykonania.</param>
        /// <param name="parameters">Opcjonalne parametry dla skryptu.</param>
        /// <returns>Kolekcja obiektów PSObject zwróconych przez skrypt lub null w przypadku błędu.</returns>
        Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null);

        // Można dodać inne metody, np. do zarządzania kanałami, aktualizacji właściwości zespołu itp.
    }
}