using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;
using TeamsManager.Core.Models; // Dodano dla User, jeśli potrzebne w przyszłości
using System.Security; // Dla SecureString
using System.Collections.Generic; // Dla Dictionary
using System.Threading.Tasks;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Serwis odpowiedzialny za interakcję z PowerShell, głównie w kontekście Microsoft Teams i Azure AD.
    /// </summary>
    public class PowerShellService : IPowerShellService
    {
        private readonly ILogger<PowerShellService> _logger;
        private Runspace? _runspace; // Zmieniono na nullable
        private bool _isConnected = false;
        private bool _disposed = false;

        /// <summary>
        /// Konstruktor serwisu PowerShell.
        /// </summary>
        /// <param name="logger">Rejestrator zdarzeń.</param>
        public PowerShellService(ILogger<PowerShellService> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            InitializeRunspace();
        }

        private void InitializeRunspace()
        {
            try
            {
                // Use minimal initial session state to avoid loading unnecessary snap-ins
                var initialSessionState = InitialSessionState.CreateDefault2();
                _runspace = RunspaceFactory.CreateRunspace(initialSessionState);
                _runspace.Open();
                _logger.LogInformation("Środowisko PowerShell zostało zainicjowane poprawnie.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się zainicjalizować środowiska PowerShell.");
                _runspace = null;
                // For testing purposes, we'll create a basic runspace if the default one fails
                try
                {
                    _runspace = RunspaceFactory.CreateRunspace();
                    _runspace.Open();
                    _logger.LogInformation("Środowisko PowerShell zostało zainicjowane w trybie podstawowym.");
                }
                catch
                {
                    _runspace = null;
                }
            }
        }

        /// <inheritdoc />
        public bool IsConnected => _isConnected;

        /// <inheritdoc />
        public async Task<bool> ConnectToTeamsAsync(string username, string password)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można połączyć z Teams: środowisko PowerShell nie jest poprawnie zainicjowane lub otwarte.");
                return false;
            }

            return await Task.Run(() => // Uruchomienie w osobnym wątku, aby nie blokować UI
            {
                try
                {
                    _logger.LogInformation("Próba połączenia z Microsoft Teams dla użytkownika {Username}", username);
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        // Import modułu MicrosoftTeams. Błąd tutaj może oznaczać, że moduł nie jest zainstalowany.
                        ps.AddScript("Import-Module MicrosoftTeams -ErrorAction Stop;");

                        // Bezpieczne tworzenie obiektu PSCredential
                        SecureString securePassword = new SecureString();
                        foreach (char c in password)
                        {
                            securePassword.AppendChar(c);
                        }
                        securePassword.MakeReadOnly();
                        var credential = new PSCredential(username, securePassword);

                        ps.AddCommand("Connect-MicrosoftTeams")
                          .AddParameter("Credential", credential)
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            _isConnected = false;
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas ConnectToTeamsAsync: {Error}", error.ToString());
                            }
                            return false;
                        }
                        _isConnected = true;
                        _logger.LogInformation("Pomyślnie połączono z Microsoft Teams jako {Username}", username);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się połączyć z Microsoft Teams dla użytkownika {Username}", username);
                    _isConnected = false;
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<string?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility = TeamVisibility.Private, string? template = null)
        {
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogError("Nie można utworzyć zespołu: Nazwa wyświetlana (DisplayName) oraz właściciel (OwnerUpn) są wymagane.");
                return null;
            }

            if (!_isConnected)
            {
                _logger.LogError("Nie można utworzyć zespołu: Nie połączono z Teams.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogError("Nie można utworzyć zespołu: Nazwa wyświetlana (DisplayName) oraz właściciel (OwnerUpn) są wymagane.");
                return null;
            }

            _logger.LogInformation("Tworzenie zespołu w Microsoft Teams: Nazwa='{DisplayName}', Właściciel='{OwnerUpn}', Widoczność='{Visibility}', Szablon='{Template}'",
                displayName, ownerUpn, visibility, template ?? "Brak");

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        var command = ps.AddCommand("New-Team")
                                        .AddParameter("DisplayName", displayName)
                                        .AddParameter("Owner", ownerUpn)
                                        .AddParameter("Visibility", visibility.ToString()) // Enum konwertowany na string
                                        .AddParameter("ErrorAction", "Stop");

                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            command.AddParameter("Description", description);
                        }
                        if (!string.IsNullOrEmpty(template))
                        {
                            // W zależności od tego, czego oczekuje New-Team (nazwa szablonu czy ID)
                            command.AddParameter("Template", template);
                        }

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas CreateTeamAsync dla DisplayName '{DisplayName}': {Error}", displayName, error.ToString());
                            }
                            return null;
                        }

                        // New-Team zwraca obiekt zespołu, którego właściwość GroupId to ID zespołu
                        var teamId = results.FirstOrDefault()?.Properties["GroupId"]?.Value?.ToString();
                        if (string.IsNullOrEmpty(teamId))
                        {
                            _logger.LogError("Nie udało się uzyskać GroupId dla nowo utworzonego zespołu: {DisplayName}", displayName);
                            return null;
                        }
                        _logger.LogInformation("Utworzono zespół '{DisplayName}' w Microsoft Teams o ID: {TeamId}", displayName, teamId);
                        return teamId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tworzenie zespołu '{DisplayName}' w Microsoft Teams nie powiodło się.", displayName);
                    return null;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> UpdateTeamPropertiesAsync(string teamId, string? newDisplayName = null, string? newDescription = null, TeamVisibility? newVisibility = null)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można zaktualizować zespołu: Nie połączono z Teams. TeamID: {TeamId}", teamId);
                return false;
            }
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("Nie można zaktualizować zespołu: TeamID nie może być puste.");
                return false;
            }
            if (newDisplayName == null && newDescription == null && newVisibility == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla zespołu TeamID: {TeamId}.", teamId);
                return true; // Nie ma nic do zrobienia, uznajemy za sukces
            }

            _logger.LogInformation("Aktualizowanie właściwości zespołu ID: {TeamId}. Nowa nazwa: '{NewDisplayName}', Nowy opis: '{NewDescription}', Nowa widoczność: '{NewVisibility}'",
                teamId, newDisplayName ?? "bez zmian", newDescription ?? "bez zmian", newVisibility?.ToString() ?? "bez zmian");

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        var command = ps.AddCommand("Set-Team")
                                        .AddParameter("GroupId", teamId)
                                        .AddParameter("ErrorAction", "Stop");

                        if (newDisplayName != null) command.AddParameter("DisplayName", newDisplayName);
                        if (newDescription != null) command.AddParameter("Description", newDescription);
                        if (newVisibility.HasValue) command.AddParameter("Visibility", newVisibility.Value.ToString());

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas UpdateTeamPropertiesAsync dla TeamID {TeamId}: {Error}", teamId, error.ToString());
                            }
                            return false;
                        }
                        _logger.LogInformation("Pomyślnie zaktualizowano właściwości zespołu ID: {TeamId}.", teamId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zaktualizować właściwości zespołu o ID: {TeamId}.", teamId);
                    return false;
                }
            });
        }


        /// <inheritdoc />
        public async Task<bool> ArchiveTeamAsync(string teamId)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można zarchiwizować zespołu: Nie połączono z Teams. TeamID: {TeamId}", teamId);
                return false;
            }
            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("Nie można zarchiwizować zespołu: TeamID nie może być puste.");
                return false;
            }

            _logger.LogInformation("Rozpoczynanie archiwizacji zespołu o ID: {TeamId} w Microsoft Teams.", teamId);
            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Set-TeamArchivedState")
                          .AddParameter("GroupId", teamId)
                          .AddParameter("Archived", true)
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas ArchiveTeamAsync dla TeamID {TeamId}: {Error}", teamId, error.ToString());
                            }
                            return false;
                        }
                        _logger.LogInformation("Zespół o ID: {TeamId} pomyślnie zarchiwizowany w Microsoft Teams.", teamId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zarchiwizować zespołu o ID: {TeamId} w Microsoft Teams.", teamId);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> UnarchiveTeamAsync(string teamId)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można przywrócić zespołu z archiwum: Nie połączono z Teams. TeamID: {TeamId}", teamId);
                return false;
            }
            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("Nie można przywrócić zespołu z archiwum: TeamID nie może być puste.");
                return false;
            }

            _logger.LogInformation("Rozpoczynanie przywracania zespołu o ID: {TeamId} z archiwum w Microsoft Teams.", teamId);
            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Set-TeamArchivedState")
                          .AddParameter("GroupId", teamId)
                          .AddParameter("Archived", false) // Ustawienie na false przywraca zespół
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas UnarchiveTeamAsync dla TeamID {TeamId}: {Error}", teamId, error.ToString());
                            }
                            return false;
                        }
                        _logger.LogInformation("Zespół o ID: {TeamId} pomyślnie przywrócony z archiwum w Microsoft Teams.", teamId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się przywrócić zespołu o ID: {TeamId} z archiwum w Microsoft Teams.", teamId);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można usunąć zespołu: Nie połączono z Teams. TeamID: {TeamId}", teamId);
                return false;
            }
            if (string.IsNullOrEmpty(teamId))
            {
                _logger.LogError("Nie można usunąć zespołu: TeamID nie może być puste.");
                return false;
            }

            _logger.LogInformation("Rozpoczynanie usuwania zespołu o ID: {TeamId} w Microsoft Teams.", teamId);
            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Remove-Team")
                          .AddParameter("GroupId", teamId)
                          .AddParameter("Confirm", false)
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas DeleteTeamAsync dla TeamID {TeamId}: {Error}", teamId, error.ToString());
                            }
                            return false;
                        }
                        _logger.LogInformation("Zespół o ID: {TeamId} pomyślnie usunięty w Microsoft Teams.", teamId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się usunąć zespołu o ID: {TeamId} w Microsoft Teams.", teamId);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można dodać użytkownika do zespołu: Nie połączono z Teams. TeamID: {TeamId}, User: {UserUpn}", teamId, userUpn);
                return false;
            }
            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn) || string.IsNullOrEmpty(role))
            {
                _logger.LogError("Nie można dodać użytkownika do zespołu: TeamID, UserUPN oraz Role są wymagane. TeamID: {TeamId}, User: {UserUpn}, Role: {Role}", teamId, userUpn, role);
                return false;
            }
            if (!role.Equals("Owner", StringComparison.OrdinalIgnoreCase) && !role.Equals("Member", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogError("Nie można dodać użytkownika do zespołu: Nieprawidłowa rola '{Role}'. Dopuszczalne wartości to 'Owner' lub 'Member'.", role);
                return false;
            }

            _logger.LogInformation("Dodawanie użytkownika {UserUpn} do zespołu ID: {TeamId} z rolą {Role} w Microsoft Teams.", userUpn, teamId, role);
            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Add-TeamUser")
                          .AddParameter("GroupId", teamId)
                          .AddParameter("User", userUpn)
                          .AddParameter("Role", role)
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas AddUserToTeamAsync dla TeamID {TeamId}, User {UserUpn}: {Error}", teamId, userUpn, error.ToString());
                            }
                            return false;
                        }
                        _logger.LogInformation("Użytkownik {UserUpn} pomyślnie dodany do zespołu ID: {TeamId} z rolą {Role} w Microsoft Teams.", userUpn, teamId, role);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się dodać użytkownika {UserUpn} do zespołu ID: {TeamId} w Microsoft Teams.", userUpn, teamId);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można usunąć użytkownika z zespołu: Nie połączono z Teams. TeamID: {TeamId}, User: {UserUpn}", teamId, userUpn);
                return false;
            }
            if (string.IsNullOrEmpty(teamId) || string.IsNullOrEmpty(userUpn))
            {
                _logger.LogError("Nie można usunąć użytkownika z zespołu: TeamID oraz UserUPN są wymagane. TeamID: {TeamId}, User: {UserUpn}", teamId, userUpn);
                return false;
            }

            _logger.LogInformation("Usuwanie użytkownika {UserUpn} z zespołu ID: {TeamId} w Microsoft Teams.", userUpn, teamId);
            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Remove-TeamUser")
                          .AddParameter("GroupId", teamId)
                          .AddParameter("User", userUpn)
                          .AddParameter("Confirm", false) // Unikanie interaktywnego potwierdzenia
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas RemoveUserFromTeamAsync dla TeamID {TeamId}, User {UserUpn}: {Error}", teamId, userUpn, error.ToString());
                            }
                            return false;
                        }
                        _logger.LogInformation("Użytkownik {UserUpn} pomyślnie usunięty z zespołu ID: {TeamId} w Microsoft Teams.", userUpn, teamId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się usunąć użytkownika {UserUpn} z zespołu ID: {TeamId} w Microsoft Teams.", userUpn, teamId);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<string?> CreateM365UserAsync(string displayName, string userPrincipalName, string password, string usageLocation = "PL", List<string>? licenseSkuIds = null, bool accountEnabled = true)
        {
            if (!_isConnected) // Połączenie z Teams może nie być wymagane, ale połączenie z AzureAD/Graph tak. Zakładamy, że Connect-MicrosoftTeams wystarczy.
            {
                _logger.LogError("Nie można utworzyć użytkownika M365: Nie połączono (wymagane połączenie z usługami M365 via Teams module).");
                return null;
            }
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(userPrincipalName) || string.IsNullOrWhiteSpace(password))
            {
                _logger.LogError("Nie można utworzyć użytkownika M365: DisplayName, UserPrincipalName oraz Password są wymagane.");
                return null;
            }
            _logger.LogInformation("Tworzenie użytkownika M365: UPN='{UserPrincipalName}', DisplayName='{DisplayName}'", userPrincipalName, displayName);
            // Rzeczywista implementacja wymagałaby użycia np. New-MgUser z Microsoft.Graph.Users lub starszych cmdlets.
            // Poniżej jest tylko placeholder/symulacja.
            await Task.Delay(100); // Symulacja operacji asynchronicznej
            _logger.LogWarning("Symulacja tworzenia użytkownika M365 (CreateM365UserAsync) - Rzeczywista implementacja PowerShell jest wymagana.");
            // Zwracamy fikcyjne ID, jeśli operacja "się udała"
            return Guid.NewGuid().ToString(); // Fikcyjny ObjectId
        }

        /// <inheritdoc />
        public async Task<bool> SetM365UserAccountStateAsync(string userPrincipalName, bool isEnabled)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można zmienić stanu konta użytkownika M365: Nie połączono.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(userPrincipalName))
            {
                _logger.LogError("Nie można zmienić stanu konta użytkownika M365: UserPrincipalName jest wymagany.");
                return false;
            }
            _logger.LogInformation("Zmiana stanu konta użytkownika M365: UPN='{UserPrincipalName}', Włączone={IsEnabled}", userPrincipalName, isEnabled);
            // Rzeczywista implementacja: Set-MgUser -UserPrincipalName $userPrincipalName -AccountEnabled $isEnabled lub Set-MsolUser -UserPrincipalName $userPrincipalName -BlockCredential !$isEnabled
            await Task.Delay(50);
            _logger.LogWarning("Symulacja zmiany stanu konta użytkownika M365 (SetM365UserAccountStateAsync) - Rzeczywista implementacja PowerShell jest wymagana.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateM365UserPrincipalNameAsync(string currentUpn, string newUpn)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można zaktualizować UPN użytkownika M365: Nie połączono.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(currentUpn) || string.IsNullOrWhiteSpace(newUpn))
            {
                _logger.LogError("Nie można zaktualizować UPN użytkownika M365: currentUpn i newUpn są wymagane.");
                return false;
            }
            _logger.LogInformation("Aktualizacja UPN użytkownika M365 z '{CurrentUpn}' na '{NewUpn}'", currentUpn, newUpn);
            // Rzeczywista implementacja: Set-MsolUserPrincipalName -UserPrincipalName $currentUpn -NewUserPrincipalName $newUpn
            // lub odpowiednik Graph SDK: Update-MgUser -UserId $currentUpn -UserPrincipalName $newUpn
            await Task.Delay(50);
            _logger.LogWarning("Symulacja aktualizacji UPN użytkownika M365 (UpdateM365UserPrincipalNameAsync) - Rzeczywista implementacja PowerShell jest wymagana.");
            return true;
        }

        /// <inheritdoc />
        public async Task<bool> UpdateM365UserPropertiesAsync(string userUpn, string? department = null, string? jobTitle = null, string? firstName = null, string? lastName = null)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można zaktualizować właściwości użytkownika M365: Nie połączono.");
                return false;
            }
            if (string.IsNullOrWhiteSpace(userUpn))
            {
                _logger.LogError("Nie można zaktualizować właściwości użytkownika M365: userUpn jest wymagany.");
                return false;
            }
            _logger.LogInformation("Aktualizacja właściwości użytkownika M365: UPN='{UserUpn}', Dział='{Department}', Stanowisko='{JobTitle}'", userUpn, department ?? "bez zmian", jobTitle ?? "bez zmian");
            // Rzeczywista implementacja: Set-User -Identity $userUpn -Department $department -JobTitle $jobTitle ... lub Update-MgUser
            await Task.Delay(50);
            _logger.LogWarning("Symulacja aktualizacji właściwości użytkownika M365 (UpdateM365UserPropertiesAsync) - Rzeczywista implementacja PowerShell jest wymagana.");
            return true;
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można wykonać skryptu: środowisko PowerShell nie jest poprawnie zainicjowane lub otwarte.");
                return null;
            }
            // Połączenie z Teams może nie być konieczne dla wszystkich skryptów, ale ostrzeżenie jest na miejscu.
            if (!_isConnected)
            {
                _logger.LogWarning("Próba wykonania skryptu bez aktywnego połączenia z Teams. Skrypt może się nie powiesć, jeśli wymaga modułu Teams.");
            }
            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogError("Nie można wykonać skryptu: Skrypt jest pusty.");
                return null;
            }

            _logger.LogInformation("Wykonywanie generycznego skryptu PowerShell: {Script}", script);
            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript(script);

                        if (parameters != null)
                        {
                            ps.AddParameters(parameters);
                        }

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas ExecuteScriptAsync: Skrypt='{Script}', Błąd='{Error}'", script, error.ToString());
                            }
                        }
                        _logger.LogInformation("Pomyślnie wykonano (lub próbowano wykonać) skrypt. Liczba zwróconych obiektów: {Count}", results?.Count ?? 0);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się wykonać skryptu PowerShell: {Script}", script);
                    return null;
                }
            });
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Zwalnia zasoby zarządzane i niezarządzane.
        /// </summary>
        /// <param name="disposing">True, jeśli wywołane przez Dispose(); false, jeśli przez finalizator.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    try
                    {
                        if (_isConnected && _runspace != null && _runspace.RunspaceStateInfo.State == RunspaceState.Opened)
                        {
                            _logger.LogInformation("Próba rozłączenia z Microsoft Teams...");
                            try
                            {
                                using (var ps = PowerShell.Create())
                                {
                                    ps.Runspace = _runspace;
                                    ps.AddScript("Disconnect-MicrosoftTeams -ErrorAction SilentlyContinue");
                                    ps.Invoke();
                                }
                                _logger.LogInformation("Rozłączono z Microsoft Teams (lub próba rozłączenia zakończona).");
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Błąd podczas próby rozłączenia z Microsoft Teams w Dispose.");
                            }
                        }
                    }
                    finally
                    {
                        try
                        {
                            if (_runspace != null)
                            {
                                if (_runspace.RunspaceStateInfo.State == RunspaceState.Opened)
                                {
                                    _runspace.Close();
                                }
                                _runspace.Dispose();
                                _logger.LogInformation("Zasoby Runspace zostały zwolnione.");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Błąd podczas zamykania Runspace.");
                        }
                        finally
                        {
                            _runspace = null;
                            _isConnected = false;
                        }
                    }
                }
                _disposed = true;
            }
        }
    }
}