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
    public class PowerShellService : IPowerShellService
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
                throw;
            }
        }

        public async Task<bool> ConnectToTeamsAsync(string username, string password)
        {
            // Możesz opakować istniejącą logikę synchroniczną w Task.Run,
            // ale idealnie byłoby przepisać logikę z użyciem ps.InvokeAsync(), jeśli to możliwe.
            // Na razie proste opakowanie:
            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Próbuję połączyć się z Microsoft Teams (async wrapper)");
                    // ... (Twoja obecna logika z Import-Module, ConvertTo-SecureString, New-Object PSCredential, Connect-MicrosoftTeams) ...
                    // Upewnij się, że poprawnie zwracasz true/false
                    // Przykład z Twojego kodu:
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        // ... (skrypty) ...
                        ps.AddScript("Import-Module MicrosoftTeams -ErrorAction Stop"); // Dodaj ErrorAction Stop
                        ps.AddScript($"$securePassword = ConvertTo-SecureString '{password}' -AsPlainText -Force");
                        ps.AddScript($"$credential = New-Object System.Management.Automation.PSCredential ('{username}', $securePassword)");
                        ps.AddScript("Connect-MicrosoftTeams -Credential $credential -ErrorAction Stop"); // Dodaj ErrorAction Stop

                        var results = ps.Invoke(); // Invoke jest synchroniczne

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
                        _logger.LogInformation("Udało się połączyć z Microsoft Teams (async wrapper)");
                        return true;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Nie udało się połączyć z Microsoft Teams (async wrapper)");
                    _isConnected = false;
                    return false;
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

            // Symulacja wywołania PowerShell
            // W rzeczywistości tutaj byłby kod podobny do CreateTeamAsync,
            // używający using (var ps = PowerShell.Create()) { ... }
            // i skryptu: Set-TeamArchivedState -GroupId $teamId -Archived $true
            await Task.Delay(100); // Symulacja operacji asynchronicznej
            bool success = true;   // Załóżmy na razie sukces

            if (success)
            {
                _logger.LogInformation("Zespół o ID: {TeamId} pomyślnie zarchiwizowany w Microsoft Teams (symulacja).", teamId);
                return true;
            }
            else
            {
                _logger.LogError("Nie udało się zarchiwizować zespołu o ID: {TeamId} w Microsoft Teams (symulacja).", teamId);
                return false;
            }
        }

        public async Task<bool> UnarchiveTeamAsync(string teamId)
        {
            _logger.LogWarning("Metoda UnarchiveTeamAsync nie została jeszcze zaimplementowana.");
            // TODO: Implementacja z Set-TeamArchivedState -GroupId $teamId -Archived $false
            await Task.Delay(10); // Symulacja
            return await Task.FromResult(false); // Placeholder
        }

        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            _logger.LogWarning("Metoda DeleteTeamAsync nie została jeszcze zaimplementowana.");
            // TODO: Implementacja z Remove-Team -GroupId $teamId
            await Task.Delay(10);
            return await Task.FromResult(false); // Placeholder
        }

        public async Task<bool> AddUserToTeamAsync(string teamId, string userUpn, string role)
        {
            _logger.LogWarning("Metoda AddUserToTeamAsync nie została jeszcze zaimplementowana.");
            // TODO: Implementacja z Add-TeamUser -GroupId $teamId -User $userUpn -Role $role
            await Task.Delay(10);
            return await Task.FromResult(false); // Placeholder
        }

        public async Task<bool> RemoveUserFromTeamAsync(string teamId, string userUpn)
        {
            _logger.LogWarning("Metoda RemoveUserFromTeamAsync nie została jeszcze zaimplementowana.");
            // TODO: Implementacja z Remove-TeamUser -GroupId $teamId -User $userUpn
            await Task.Delay(10);
            return await Task.FromResult(false); // Placeholder
        }

        public async Task<Collection<PSObject>?> ExecuteScriptAsync(string script, Dictionary<string, object>? parameters = null)
        {
            _logger.LogWarning("Metoda ExecuteScriptAsync nie została jeszcze w pełni zaimplementowana.");
            // TODO: Implementacja ogólnego wykonywania skryptów
            await Task.Delay(10);
            return await Task.FromResult<Collection<PSObject>?>(null); // Placeholder
        }

        public async Task<string?> CreateTeamAsync(string displayName, string description, string ownerUpn, TeamVisibility visibility = TeamVisibility.Private, string? template = null)
        {
            if (!_isConnected)
            {
                _logger.LogError("Nie można utworzyć zespołu: Nie połączono z Teams.");
                // Można rzucić InvalidOperationException lub zwrócić null
                return null;
            }

            return await Task.Run(() =>
            {
                try
                {
                    _logger.LogInformation("Tworzenie zespołu (async wrapper): {TeamName}, Właściciel: {OwnerUpn}", displayName, ownerUpn);
                    using (var ps = PowerShell.Create())
                    {
                        ps.Runspace = _runspace;
                        // Zbuduj skrypt dynamicznie
                        var script = $"New-Team -DisplayName '{displayName}' -Description '{description}' -Owner '{ownerUpn}' -Visibility '{visibility}'";
                        if (!string.IsNullOrEmpty(template))
                        {
                            script += $" -Template '{template}'";
                        }
                        ps.AddScript(script + " -ErrorAction Stop");

                        var results = ps.Invoke();

                        if (ps.HadErrors)
                        {
                            foreach (var error in ps.Streams.Error)
                            {
                                _logger.LogError("Błąd Powershella podczas CreateTeamAsync: {Error}", error.ToString());
                            }
                            return null;
                        }

                        var teamId = results.FirstOrDefault()?.Properties["GroupId"]?.Value?.ToString();
                        if (string.IsNullOrEmpty(teamId))
                        {
                            _logger.LogError("Nie udało się uzyskać GroupId dla nowo utworzonego zespołu: {DisplayName}", displayName);
                            return null;
                        }
                        _logger.LogInformation("Utworzono zespół o ID: {TeamId}", teamId);
                        return teamId;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Tworzenie zespołu (async wrapper) się nie powiodło: {TeamName}", displayName);
                    return null;
                }
            });
        }

        public bool IsConnected => _isConnected;

        public void Dispose()
        {
            if (!_disposed)
            {
                try
                {
                    if (_isConnected)
                    {
                        using (var ps = PowerShell.Create())
                        {
                            ps.Runspace = _runspace;
                            ps.AddScript("Disconnect-MicrosoftTeams");
                            ps.Invoke();
                        }
                        _logger.LogInformation("Rozłączono z Microsoft Teams");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas rozłączania");
                }
                finally
                {
                    _runspace?.Close();
                    _runspace?.Dispose();
                    _disposed = true;
                }
            }
        }
    }
}