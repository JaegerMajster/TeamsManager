// Plik: TeamsManager.Core/Abstractions/Services/IPowerShellService.cs
using System.Collections.ObjectModel;
using System.Management.Automation;
using System.Threading.Tasks;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models;
using System.Security;
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
        /// To jest preferowana metoda połączenia.
        /// </summary>
        /// <param name="accessToken">Token dostępu OAuth 2.0.</param>
        /// <param name="scopes">Opcjonalna lista scopes (uprawnień), o które prosi token. Używane przez Connect-MgGraph do weryfikacji lub wymuszenia re-autoryzacji.</param>
        /// <returns>True, jeśli połączenie zostało pomyślnie nawiązane; w przeciwnym razie false.</returns>
        Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null);

        // UWAGA: Stara metoda ConnectToTeamsAsync(string username, string password) została usunięta
        // na rzecz ConnectWithAccessTokenAsync w celu ujednolicenia uwierzytelniania przez OAuth.
        // Jeśli napotkasz odwołania do starej metody, należy je odpowiednio zaktualizować.

        /// <summary>
        /// Asynchronicznie tworzy nowy zespół w Microsoft Teams.
        /// </summary>
        /// <param name="displayName">Nazwa wyświetlana zespołu.</param>
        /// <param name="description">Opis zespołu.</param>
        /// <param name="ownerUpn">UPN właściciela zespołu.</param>
        /// <param name="visibility">Widoczność zespołu.</param>
        /// <param name="template">Opcjonalny szablon Microsoft Teams do użycia (np. "EDU_Class", lub ID szablonu z Teams Admin Center).</param>
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
        /// <param name="password">Hasło dla nowego użytkownika (zostanie przekonwertowane na SecureString).</param>
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
        /// <param name="currentDisplayName">Bieżąca nazwa wyświetlana kanału, który ma być zaktualizowany.</param>
        /// <param name="newDisplayName">Nowa nazwa wyświetlana kanału (opcjonalna).</param>
        /// <param name="newDescription">Nowy opis kanału (opcjonalny).</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> UpdateTeamChannelAsync(string teamId, string currentDisplayName, string? newDisplayName = null, string? newDescription = null);

        /// <summary>
        /// Asynchronicznie usuwa kanał z zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <param name="channelDisplayName">Nazwa wyświetlana kanału do usunięcia.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> RemoveTeamChannelAsync(string teamId, string channelDisplayName);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie kanały dla określonego zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator (GroupId) zespołu.</param>
        /// <returns>Kolekcja obiektów PSObject reprezentujących kanały lub null w przypadku błędu.</returns>
        Task<Collection<PSObject>?> GetTeamChannelsAsync(string teamId);

        // --- NOWE METODY ZADAŃ A.1-B.1 (uzupełnienie) ---

        /// <summary>
        /// Asynchronicznie pobiera pojedynczy zespół na podstawie jego identyfikatora.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu (GroupId).</param>
        /// <returns>Obiekt PSObject z informacjami o zespole lub null.</returns>
        Task<PSObject?> GetTeamAsync(string teamId);

        /// <summary>
        /// Asynchronicznie pobiera wszystkie zespoły.
        /// </summary>
        /// <returns>Kolekcja obiektów PSObject z listą wszystkich zespołów lub null.</returns>
        Task<Collection<PSObject>?> GetAllTeamsAsync();

        /// <summary>
        /// Asynchronicznie pobiera zespoły, których właścicielem jest podany użytkownik (UPN).
        /// </summary>
        /// <param name="ownerUpn">User Principal Name (UPN) właściciela.</param>
        /// <returns>Kolekcja obiektów PSObject z zespołami właściciela lub null.</returns>
        Task<Collection<PSObject>?> GetTeamsByOwnerAsync(string ownerUpn);

        /// <summary>
        /// Asynchronicznie pobiera wszystkich członków zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu (GroupId).</param>
        /// <returns>Kolekcja obiektów PSObject z listą członków lub null.</returns>
        Task<Collection<PSObject>?> GetTeamMembersAsync(string teamId);

        /// <summary>
        /// Asynchronicznie pobiera konkretnego członka zespołu.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu (GroupId).</param>
        /// <param name="userUpn">User Principal Name (UPN) użytkownika.</param>
        /// <returns>Obiekt PSObject z informacjami o członku lub null.</returns>
        Task<PSObject?> GetTeamMemberAsync(string teamId, string userUpn);

        /// <summary>
        /// Asynchronicznie przypisuje licencję do użytkownika M365.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika.</param>
        /// <param name="licenseSkuId">ID SKU licencji do przypisania.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> AssignLicenseToUserAsync(string userUpn, string licenseSkuId);

        /// <summary>
        /// Asynchronicznie usuwa licencję od użytkownika M365.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika.</param>
        /// <param name="licenseSkuId">ID SKU licencji do usunięcia.</param>
        /// <returns>True, jeśli operacja się powiodła; w przeciwnym razie false.</returns>
        Task<bool> RemoveLicenseFromUserAsync(string userUpn, string licenseSkuId);

        /// <summary>
        /// Asynchronicznie pobiera szczegóły licencji przypisanych do użytkownika M365.
        /// </summary>
        /// <param name="userUpn">UPN użytkownika.</param>
        /// <returns>Kolekcja obiektów PSObject z licencjami użytkownika lub null.</returns>
        Task<Collection<PSObject>?> GetUserLicensesAsync(string userUpn);

        // --- NOWE METODY ZADAŃ (uzupełnienie - punkt 1) ---

        /// <summary>
        /// Asynchronicznie pobiera wszystkich użytkowników M365, opcjonalnie z filtrem.
        /// </summary>
        /// <param name="filter">Opcjonalny filtr OData do zastosowania (np. "startswith(DisplayName,'Jan')", "AccountEnabled eq false").</param>
        /// <returns>Kolekcja obiektów PSObject z informacjami o użytkownikach lub null.</returns>
        Task<Collection<PSObject>?> GetAllUsersAsync(string? filter = null);

        /// <summary>
        /// Asynchronicznie pobiera użytkowników, którzy nie logowali się przez określoną liczbę dni.
        /// Wymaga uprawnienia `AuditLog.Read.All` lub `Directory.Read.All` z rozszerzonymi właściwościami.
        /// </summary>
        /// <param name="daysInactive">Liczba dni braku aktywności (logowania).</param>
        /// <returns>Kolekcja obiektów PSObject z informacjami o nieaktywnych użytkownikach lub null.</returns>
        Task<Collection<PSObject>?> GetInactiveUsersAsync(int daysInactive);

        // --- NOWE METODY ZADAŃ (uzupełnienie - punkt 2) ---

        /// <summary>
        /// Asynchronicznie dodaje wielu użytkowników do zespołu.
        /// Ta metoda wywołuje `AddUserToTeamAsync` dla każdego użytkownika.
        /// </summary>
        /// <param name="teamId">Identyfikator zespołu (GroupId).</param>
        /// <param name="userUpns">Lista UPN-ów użytkowników do dodania.</param>
        /// <param name="role">Rola, w jakiej użytkownicy mają zostać dodani ("Member" lub "Owner").</param>
        /// <returns>Słownik, gdzie kluczem jest UPN użytkownika, a wartością bool informująca o sukcesie operacji dla tego użytkownika.</returns>
        Task<Dictionary<string, bool>> BulkAddUsersToTeamAsync(string teamId, List<string> userUpns, string role = "Member");

        // --- NOWE METODY ZADAŃ (uzupełnienie - punkt 3) ---

        /// <summary>
        /// Asynchronicznie znajduje użytkowników z duplikującymi się nazwami wyświetlanymi lub innymi polami.
        /// </summary>
        /// <returns>Kolekcja obiektów PSObject reprezentujących grupy duplikatów.</returns>
        Task<Collection<PSObject>?> FindDuplicateUsersAsync();

        // --- NOWE METODY ZADAŃ (uzupełnienie - punkt 4) ---
        // Decyzja: Metoda ExecuteBatchOperationAsync (transakcyjność na poziomie aplikacji) nie jest metodą PowerShellService.
        // Jest to metoda ogólnego zarządzania asynchronicznymi operacjami w serwisach aplikacyjnych,
        // która może wywoływać metody PowerShellService, ale sama nie jest operacją PowerShell.
        // Dlatego nie będziemy jej dodawać do IPowerShellService.
        // Może zostać zaimplementowana w warstwie TeamsManager.Core w osobnym pomocniku lub w serwisie nadrzędnym.
    }
}