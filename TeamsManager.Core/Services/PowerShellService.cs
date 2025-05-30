using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Services
{
    public class PowerShellService : IPowerShellService // Zakładam, że reszta klasy jest już zdefiniowana
    {
        private readonly ILogger<PowerShellService> _logger;
        private Runspace _runspace = null!;
        private bool _isConnected = false;
        private bool _disposed = false;

        public PowerShellService(ILogger<PowerShellService> logger)
        {
            _logger = logger;
            InitializeRunspace();
        }

        private void InitializeRunspace()
        {
            try
            {
                _runspace = RunspaceFactory.CreateRunspace();
                _runspace.Open();
                _logger.LogInformation("Środowisko Powershell zostało zainicjowane poprawnie");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się zainicjalizować środowiska Powershell");
                throw; // Rzucamy wyjątek dalej, aby aplikacja była świadoma problemu
            }
        }

        public async Task<bool> ConnectToTeamsAsync(string username, string password)
        {
            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Próbuję połączyć się z Microsoft Teams (async wrapper) dla użytkownika {Username}", username);
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        ps.AddScript("Import-Module MicrosoftTeams -ErrorAction Stop");
                        ps.AddScript($"$securePassword = ConvertTo-SecureString -String '{password}' -AsPlainText -Force"); // Poprawiona linia
                        ps.AddScript($"$credential = New-Object System.Management.Automation.PSCredential ('{username}', $securePassword)");
                        ps.AddScript("Connect-MicrosoftTeams -Credential $credential -ErrorAction Stop");

                        ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd Powershella podczas ConnectToTeamsAsync: {Error}", error.ToString());
                            }
                            _isConnected = false;
                            return false;
                        }
                        _isConnected = true;
                        _logger.LogInformation("Udało się połączyć z Microsoft Teams (async wrapper) jako {Username}", username);
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się połączyć z Microsoft Teams (async wrapper) dla użytkownika {Username}", username);
                    _isConnected = false;
                    return false;
                }
            });
        }

        public async Task<string?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility = TeamVisibility.Private, string? template = null)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można utworzyć zespołu: Nie połączono z Teams.");
                return null;
            }
            if (string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(ownerUpn))
            {
                _logger.LogError("Nie można utworzyć zespołu: DisplayName oraz OwnerUpn są wymagane.");
                return null;
            }

            _logger.LogInformation("Tworzenie zespołu w Microsoft Teams: DisplayName='{DisplayName}', Owner='{OwnerUpn}', Visibility='{Visibility}', Template='{Template}'",
                displayName, ownerUpn, visibility, template ?? "Brak");

            return await Task.Run(() =>
            {
                try
                {
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        // Użycie AddParameter dla bezpieczeństwa i czytelności
                        ps.AddCommand("New-Team")
                          .AddParameter("DisplayName", displayName)
                          .AddParameter("Owner", ownerUpn)
                          .AddParameter("Visibility", visibility.ToString()) // Enum jest konwertowany na string
                          .AddParameter("ErrorAction", "Stop");

                        if (!string.IsNullOrWhiteSpace(description))
                        {
                            ps.AddParameter("Description", description);
                        }
                        if (!string.IsNullOrEmpty(template))
                        {
                            ps.AddParameter("Template", template);
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
                          .AddParameter("Archived", false)
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
                          .AddParameter("Confirm", false) // Unikamy interaktywnego potwierdzenia
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
            if (role.ToLower() != "owner" && role.ToLower() != "member")
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
                          .AddParameter("Confirm", false)
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

        public async Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null)
        {
            if (!_isConnected) // Niektóre skrypty mogą nie wymagać połączenia z Teams, ale większość tak
            {
                _logger.LogWarning("Próba wykonania skryptu bez aktywnego połączenia z Teams. Skrypt może się nie powiesć, jeśli wymaga modułu Teams.");
                // Można zdecydować, czy pozwolić na wykonanie, czy zwrócić błąd/null
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
                        // Można dodać globalny ErrorAction, np. ps.AddScript("$ErrorActionPreference = 'Stop'"); na początku
                        // lub dodawać -ErrorAction Stop do każdego polecenia w skrypcie, jeśli to możliwe.

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd PowerShell podczas ExecuteScriptAsync: Skrypt='{Script}', Błąd='{Error}'", script, error.ToString());
                            }
                            // Zwracamy wyniki nawet jeśli były błędy, ale można też zwrócić null.
                            // To zależy od oczekiwań wołającego.
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

        public bool IsConnected => _isConnected;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing) // Dodajemy parametr `disposing`
        {
            if (!_disposed)
            {
                if (disposing) // Uwalniaj zasoby zarządzane tylko jeśli wywołane z Dispose()
                {
                    try
                    {
                        if (_isConnected) // Rozłączaj tylko jeśli faktycznie połączono
                        {
                            _logger.LogInformation("Próba rozłączenia z Microsoft Teams...");
                            using (var ps = PowerShell.Create())
                            {
                                ps.Runspace = _runspace;
                                ps.AddScript("Disconnect-MicrosoftTeams -ErrorAction SilentlyContinue"); // Kontynuuj nawet jeśli błąd
                                ps.Invoke();
                                // Tutaj nie sprawdzamy błędów, bo i tak zamykamy
                            }
                            _logger.LogInformation("Rozłączono z Microsoft Teams (lub próba rozłączenia zakończona).");
                            _isConnected = false;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Błąd podczas próby rozłączenia z Microsoft Teams w Dispose.");
                    }
                    finally // Runspace zamykamy i usuwamy zawsze
                    {
                        if (_runspace != null)
                        {
                            if (_runspace.RunspaceStateInfo.State == RunspaceState.Opened)
                            {
                                _runspace.Close();
                            }
                            _runspace.Dispose();
                            _runspace = null!; // Ustaw na null po usunięciu
                            _logger.LogInformation("Zasoby Runspace zostały zwolnione.");
                        }
                    }
                }
                // Tutaj można by zwalniać zasoby niezarządzane, jeśli takie byłyby.
                _disposed = true;
            }
        }
    }
}