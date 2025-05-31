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
using System.Text;

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

            return await Task.Run(() =>
            {
                SecureString? securePassword = null;
                try
                {
                    _logger.LogInformation("Próba połączenia z Microsoft Teams dla użytkownika {Username}", username);

                    // Bezpieczniejsze tworzenie SecureString z automatycznym czyszczeniem
                    securePassword = new SecureString();
                    foreach (char c in password)
                    {
                        securePassword.AppendChar(c);
                    }
                    securePassword.MakeReadOnly();

                    // Pozwól GC naturalnie wyczyścić pamięć po zakończeniu metody
                    // SecureString jest głównym mechanizmem bezpieczeństwa dla haseł

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript("Import-Module MicrosoftTeams -ErrorAction Stop;");

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
                finally
                {
                    // Zawsze wyczyść SecureString
                    securePassword?.Dispose();
                }
            });
        }


        /// <inheritdoc />
        private async Task<Collection<PSObject>?> ExecuteCommandAsync(string commandName, Dictionary<string, object>? parameters = null)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można wykonać komendy '{CommandName}': środowisko PowerShell nie jest poprawnie zainicjowane lub otwarte.", commandName);
                return null;
            }

            _logger.LogDebug("Wykonywanie komendy PowerShell: {CommandName} z parametrami: {Parameters}",
                commandName, parameters != null ? string.Join(", ", parameters.Keys) : "brak");

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        var command = ps.AddCommand(commandName)
                                       .AddParameter("ErrorAction", "Stop");

                        if (parameters != null)
                        {
                            foreach (var param in parameters)
                            {
                                command.AddParameter(param.Key, param.Value);
                            }
                        }

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas wykonywania komendy '{CommandName}': {Error}", commandName, error.ToString());
                            }
                            return null;
                        }

                        _logger.LogDebug("Komenda '{CommandName}' wykonana pomyślnie. Liczba zwróconych obiektów: {Count}", commandName, results.Count);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się wykonać komendy PowerShell: {CommandName}", commandName);
                    return null;
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
                            // Obsługa szablonu EDU_Class dla zespołów edukacyjnych
                            if (template.Equals("EDU_Class", StringComparison.OrdinalIgnoreCase))
                            {
                                command.AddParameter("Template", "EDU_Class");
                                _logger.LogInformation("Używanie szablonu EDU_Class dla zespołu edukacyjnego.");
                            }
                            else
                            {
                                // Dla innych szablonów przekaż wartość bez zmian
                                command.AddParameter("Template", template);
                                _logger.LogWarning("Używanie niestandardowego szablonu: {Template}. Zalecany szablon dla zespołów edukacyjnych to 'EDU_Class'.", template);
                            }
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

                        _logger.LogInformation("Utworzono zespół '{DisplayName}' w Microsoft Teams o ID: {TeamId}{TeamType}",
                            displayName, teamId, template == "EDU_Class" ? " (typ: edukacyjny)" : "");

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
            if (!_isConnected)
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

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;

                        // Najpierw importuj moduł AzureAD jeśli potrzebny
                        ps.AddScript("if (!(Get-Module -ListAvailable -Name AzureAD)) { Import-Module AzureAD }");
                        ps.Invoke();
                        ps.Commands.Clear();

                        // Utwórz PasswordProfile
                        ps.AddScript(@"
                    $PasswordProfile = New-Object -TypeName Microsoft.Open.AzureAD.Model.PasswordProfile
                    $PasswordProfile.Password = '" + password.Replace("'", "''") + @"'
                    $PasswordProfile.ForceChangePasswordNextLogin = $false
                ");
                        ps.Invoke();
                        ps.Commands.Clear();

                        // Utwórz użytkownika
                        var command = ps.AddCommand("New-AzureADUser")
                                       .AddParameter("DisplayName", displayName)
                                       .AddParameter("UserPrincipalName", userPrincipalName)
                                       .AddParameter("PasswordProfile", "$PasswordProfile")
                                       .AddParameter("AccountEnabled", accountEnabled)
                                       .AddParameter("UsageLocation", usageLocation)
                                       .AddParameter("MailNickName", userPrincipalName.Split('@')[0])
                                       .AddParameter("ErrorAction", "Stop");

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas CreateM365UserAsync dla UPN {UserPrincipalName}: {Error}", userPrincipalName, error.ToString());
                            }
                            return null;
                        }

                        var userId = results.FirstOrDefault()?.Properties["ObjectId"]?.Value?.ToString();
                        if (string.IsNullOrEmpty(userId))
                        {
                            _logger.LogError("Nie udało się uzyskać ObjectId dla nowo utworzonego użytkownika: {UserPrincipalName}", userPrincipalName);
                            return null;
                        }

                        // Przypisz licencje jeśli podano
                        if (licenseSkuIds != null && licenseSkuIds.Count > 0)
                        {
                            ps.Commands.Clear();
                            foreach (var skuId in licenseSkuIds)
                            {
                                ps.AddCommand("Set-AzureADUserLicense")
                                  .AddParameter("ObjectId", userId)
                                  .AddParameter("AssignedLicenses", new { AddLicenses = new[] { new { SkuId = skuId } } })
                                  .AddParameter("ErrorAction", "Continue");
                                ps.Invoke();
                                ps.Commands.Clear();
                            }
                        }

                        _logger.LogInformation("Utworzono użytkownika M365 '{UserPrincipalName}' o ID: {UserId}", userPrincipalName, userId);
                        return userId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się utworzyć użytkownika M365: {UserPrincipalName}", userPrincipalName);
                    return null;
                }
            });
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

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Set-AzureADUser")
                          .AddParameter("ObjectId", userPrincipalName)
                          .AddParameter("AccountEnabled", isEnabled)
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas SetM365UserAccountStateAsync dla UPN {UserPrincipalName}: {Error}", userPrincipalName, error.ToString());
                            }
                            return false;
                        }

                        _logger.LogInformation("Pomyślnie zmieniono stan konta użytkownika {UserPrincipalName} na: {IsEnabled}", userPrincipalName, isEnabled ? "włączone" : "wyłączone");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zmienić stanu konta użytkownika M365: {UserPrincipalName}", userPrincipalName);
                    return false;
                }
            });
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

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddCommand("Set-AzureADUser")
                          .AddParameter("ObjectId", currentUpn)
                          .AddParameter("UserPrincipalName", newUpn)
                          .AddParameter("ErrorAction", "Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas UpdateM365UserPrincipalNameAsync dla UPN {CurrentUpn}: {Error}", currentUpn, error.ToString());
                            }
                            return false;
                        }

                        _logger.LogInformation("Pomyślnie zaktualizowano UPN użytkownika z '{CurrentUpn}' na '{NewUpn}'", currentUpn, newUpn);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zaktualizować UPN użytkownika M365 z '{CurrentUpn}' na '{NewUpn}'", currentUpn, newUpn);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> GetTeamChannelsAsync(string teamId)
        {
            // Sprawdzenie połączenia
            if (!_isConnected)
            {
                _logger.LogError("Nie można pobrać kanałów: Nie połączono z Teams. TeamID: {TeamId}", teamId);
                return null;
            }
            // Walidacja parametru
            if (string.IsNullOrWhiteSpace(teamId))
            {
                _logger.LogError("Nie można pobrać kanałów: TeamID nie może być puste.");
                return null;
            }

            _logger.LogInformation("Pobieranie wszystkich kanałów dla zespołu ID: {TeamId}", teamId);
            // Skrypt PowerShell do wykonania
            string script = $"Get-TeamChannel -GroupId \"{teamId}\"";

            // Wywołanie generycznej metody ExecuteScriptAsync
            return await ExecuteScriptAsync(script);
        }

        /// <inheritdoc />
        public async Task<PSObject?> GetTeamChannelAsync(string teamId, string channelDisplayName)
        {
            // Sprawdzenie połączenia
            if (!_isConnected)
            {
                _logger.LogError("Nie można pobrać kanału: Nie połączono z Teams. TeamID: {TeamId}, Channel: {ChannelDisplayName}", teamId, channelDisplayName);
                return null;
            }
            // Walidacja parametrów
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelDisplayName))
            {
                _logger.LogError("Nie można pobrać kanału: TeamID oraz ChannelDisplayName są wymagane. TeamID: '{TeamId}', ChannelDisplayName: '{ChannelDisplayName}'", teamId, channelDisplayName);
                return null;
            }

            _logger.LogInformation("Pobieranie kanału '{ChannelDisplayName}' dla zespołu ID: {TeamId}", channelDisplayName, teamId);
            // Skrypt PowerShell do wykonania
            string script = $"Get-TeamChannel -GroupId \"{teamId}\" -DisplayName \"{channelDisplayName}\"";

            // Wywołanie generycznej metody ExecuteScriptAsync
            var result = await ExecuteScriptAsync(script);
            // Get-TeamChannel -DisplayName zwraca pojedynczy obiekt (lub nic, jeśli nie znaleziono)
            return result?.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> RemoveTeamChannelAsync(string teamId, string channelDisplayName)
        {
            // Sprawdzenie połączenia
            if (!_isConnected)
            {
                _logger.LogError("Nie można usunąć kanału: Nie połączono z Teams. TeamID: {TeamId}, Kanał: {ChannelDisplayName}", teamId, channelDisplayName);
                return false;
            }
            // Walidacja parametrów
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(channelDisplayName))
            {
                _logger.LogError("Nie można usunąć kanału: TeamID oraz ChannelDisplayName są wymagane. TeamID: '{TeamId}', ChannelDisplayName: '{ChannelDisplayName}'", teamId, channelDisplayName);
                return false;
            }

            // Ostrzeżenie przed próbą usunięcia kanału "Ogólny" (General)
            // To sprawdzenie może być również realizowane na poziomie logiki biznesowej serwisu (np. TeamService),
            // ale dodatkowe zabezpieczenie w PowerShellService nie zaszkodzi.
            if (channelDisplayName.Equals("Ogólny", StringComparison.OrdinalIgnoreCase) ||
                channelDisplayName.Equals("General", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Próba usunięcia kanału ogólnego ('{ChannelDisplayName}') dla zespołu ID: {TeamId}. Ta operacja jest niedozwolona przez Microsoft Teams i cmdlet prawdopodobnie zwróci błąd.", channelDisplayName, teamId);
                // Można zdecydować o zwróceniu false od razu, lub pozwolić PowerShellowi zwrócić błąd.
                // Dla spójności z zachowaniem cmdlet, pozwólmy na próbę i obsłużmy błąd z PowerShell.
            }

            _logger.LogInformation("Usuwanie kanału '{ChannelDisplayName}' z zespołu ID: {TeamId}", channelDisplayName, teamId);

            // Skrypt PowerShell do wykonania - parametr -Confirm:$false jest używany do uniknięcia interaktywnego potwierdzenia
            string script = $"Remove-TeamChannel -GroupId \"{teamId}\" -DisplayName \"{channelDisplayName}\" -Confirm:$false";

            var results = await ExecuteScriptAsync(script);

            // Remove-TeamChannel zwykle nie zwraca obiektu przy sukcesie.
            // Sukces jest sygnalizowany brakiem błędów.
            // Zakładamy, że ExecuteScriptAsync zwróci null, jeśli wystąpił błąd wykonania skryptu lub błąd z samego cmdleta.
            if (results == null)
            {
                _logger.LogError("Wywołanie ExecuteScriptAsync dla RemoveTeamChannelAsync nie powiodło się lub zwróciło błędy. TeamID: {TeamId}, Kanał: {ChannelDisplayName}", teamId, channelDisplayName);
                return false;
            }

            _logger.LogInformation("Pomyślnie wykonano (lub próbowano wykonać) usunięcie kanału '{ChannelDisplayName}' w zespole ID: {TeamId}", channelDisplayName, teamId);
            return true; // Jeśli doszliśmy tutaj, zakładamy, że operacja została zainicjowana poprawnie.
        }

        /// <inheritdoc />
        public async Task<PSObject?> CreateTeamChannelAsync(string teamId, string displayName, bool isPrivate = false, string? description = null)
        {
            // Sprawdzenie połączenia
            if (!_isConnected)
            {
                _logger.LogError("Nie można utworzyć kanału: Nie połączono z Teams. TeamID: {TeamId}", teamId);
                return null;
            }
            // Walidacja parametrów
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(displayName))
            {
                _logger.LogError("Nie można utworzyć kanału: TeamID oraz DisplayName są wymagane. TeamID: '{TeamId}', DisplayName: '{DisplayName}'", teamId, displayName);
                return null;
            }

            _logger.LogInformation("Tworzenie kanału '{DisplayName}' w zespole ID: {TeamId}. Prywatny: {IsPrivate}", displayName, teamId, isPrivate);

            // Użyj ExecuteScriptAsync z parametrami dla bezpieczeństwa
            var parameters = new Dictionary<string, object>
            {
                { "TeamId", teamId },
                { "DisplayName", displayName }
            };

            if (isPrivate)
            {
                parameters.Add("MembershipType", "Private");
            }

            if (!string.IsNullOrWhiteSpace(description))
            {
                parameters.Add("Description", description);
            }

            // Zamiast budować skrypt, użyj parametryzowanego podejścia
            var result = await ExecuteCommandAsync("New-TeamChannel", parameters);
            return result?.FirstOrDefault();
        }

        /// <inheritdoc />
        public async Task<bool> UpdateTeamChannelAsync(string teamId, string currentDisplayName, string? newDisplayName = null, string? newDescription = null)
        {
            // Sprawdzenie połączenia
            if (!_isConnected)
            {
                _logger.LogError("Nie można zaktualizować kanału: Nie połączono z Teams. TeamID: {TeamId}, Kanał: {CurrentDisplayName}", teamId, currentDisplayName);
                return false;
            }

            // Walidacja parametrów
            if (string.IsNullOrWhiteSpace(teamId) || string.IsNullOrWhiteSpace(currentDisplayName))
            {
                _logger.LogError("Nie można zaktualizować kanału: TeamID oraz CurrentDisplayName są wymagane. TeamID: '{TeamId}', CurrentDisplayName: '{CurrentDisplayName}'", teamId, currentDisplayName);
                return false;
            }

            // Sprawdzenie czy są jakiekolwiek zmiany do wprowadzenia
            if (newDisplayName == null && newDescription == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla kanału '{CurrentChannelName}' w zespole ID: {TeamId}.", currentDisplayName, teamId);
                return true;
            }

            _logger.LogInformation("Aktualizowanie kanału '{CurrentChannelName}' w zespole ID: {TeamId}. Nowa nazwa: '{NewDisplayName}', Nowy opis: '{NewDescription}'",
                currentDisplayName, teamId, newDisplayName ?? "bez zmian", newDescription ?? "bez zmian");

            return await Task.Run(() =>
            {
                try
                {
                    var scriptBuilder = new StringBuilder($"Set-TeamChannel -GroupId \"{teamId}\" -CurrentName \"{currentDisplayName}\"");

                    if (!string.IsNullOrWhiteSpace(newDisplayName))
                    {
                        scriptBuilder.Append($" -NewName \"{newDisplayName.Replace("\"", "\"\"")}\"");
                    }

                    if (newDescription != null)
                    {
                        scriptBuilder.Append($" -Description \"{newDescription.Replace("\"", "\"\"")}\"");
                    }

                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript(scriptBuilder.ToString());
                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas UpdateTeamChannelAsync dla TeamID {TeamId}, Kanał {CurrentChannelName}: {Error}",
                                    teamId, currentDisplayName, error.ToString());
                            }
                            return false;
                        }

                        _logger.LogInformation("Pomyślnie zaktualizowano kanał '{CurrentChannelName}' w zespole ID: {TeamId}", currentDisplayName, teamId);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zaktualizować kanału '{CurrentChannelName}' w zespole ID: {TeamId}", currentDisplayName, teamId);
                    return false;
                }
            });
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

            // Sprawdzenie czy są jakiekolwiek zmiany do wprowadzenia
            if (department == null && jobTitle == null && firstName == null && lastName == null)
            {
                _logger.LogInformation("Brak właściwości do aktualizacji dla użytkownika: {UserUpn}", userUpn);
                return true;
            }

            _logger.LogInformation("Aktualizacja właściwości użytkownika M365: UPN='{UserUpn}', Dział='{Department}', Stanowisko='{JobTitle}', Imię='{FirstName}', Nazwisko='{LastName}'",
                userUpn, department ?? "bez zmian", jobTitle ?? "bez zmian", firstName ?? "bez zmian", lastName ?? "bez zmian");

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        var command = ps.AddCommand("Set-AzureADUser")
                                       .AddParameter("ObjectId", userUpn)
                                       .AddParameter("ErrorAction", "Stop");

                        if (!string.IsNullOrWhiteSpace(department))
                        {
                            command.AddParameter("Department", department);
                        }
                        if (!string.IsNullOrWhiteSpace(jobTitle))
                        {
                            command.AddParameter("JobTitle", jobTitle);
                        }
                        if (!string.IsNullOrWhiteSpace(firstName))
                        {
                            command.AddParameter("GivenName", firstName);
                        }
                        if (!string.IsNullOrWhiteSpace(lastName))
                        {
                            command.AddParameter("Surname", lastName);
                        }

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas UpdateM365UserPropertiesAsync dla UPN {UserUpn}: {Error}", userUpn, error.ToString());
                            }
                            return false;
                        }

                        _logger.LogInformation("Pomyślnie zaktualizowano właściwości użytkownika: {UserUpn}", userUpn);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się zaktualizować właściwości użytkownika M365: {UserUpn}", userUpn);
                    return false;
                }
            });
        }

        /// <inheritdoc />
        public async Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null)
        {
            if (_runspace == null || _runspace.RunspaceStateInfo.State != RunspaceState.Opened)
            {
                _logger.LogError("Nie można wykonać skryptu: środowisko PowerShell nie jest poprawnie zainicjowane lub otwarte.");
                return null;
            }

            if (!_isConnected)
            {
                _logger.LogWarning("Próba wykonania skryptu bez aktywnego połączenia z Teams. Skrypt może się nie powiesć, jeśli wymaga modułu Teams.");
            }

            if (string.IsNullOrWhiteSpace(script))
            {
                _logger.LogError("Nie można wykonać skryptu: Skrypt jest pusty.");
                return null;
            }

            // Sanityzacja skryptu - podstawowa ochrona przed injection
            if (script.Contains("`;") || script.Contains("$(") || script.Contains("${"))
            {
                _logger.LogWarning("Wykryto potencjalnie niebezpieczne znaki w skrypcie. Skrypt: {Script}", script);
            }

            _logger.LogDebug("Wykonywanie skryptu PowerShell: {Script}", script.Length > 100 ? script.Substring(0, 100) + "..." : script);

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

                        // Zbierz wszystkie strumienie dla lepszego debugowania
                        if (ps.Streams.Error.Count > 0)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell: {Error}", error.ToString());
                            }
                        }

                        if (ps.Streams.Warning.Count > 0)
                        {
                            foreach (var warning in ps.Streams.Warning)
                            {
                                _logger.LogWarning("Ostrzeżenie PowerShell: {Warning}", warning.ToString());
                            }
                        }

                        if (ps.Streams.Information.Count > 0)
                        {
                            foreach (var info in ps.Streams.Information)
                            {
                                _logger.LogInformation("Informacja PowerShell: {Info}", info.ToString());
                            }
                        }

                        // Zwróć null tylko jeśli były błędy krytyczne
                        if (ps.HadErrors && results.Count == 0)
                        {
                            _logger.LogError("Skrypt zakończył się błędami i nie zwrócił żadnych wyników.");
                            return null;
                        }

                        _logger.LogDebug("Skrypt wykonany. Liczba zwróconych obiektów: {Count}, Błędy: {ErrorCount}",
                            results.Count, ps.Streams.Error.Count);
                        return results;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się wykonać skryptu PowerShell");
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