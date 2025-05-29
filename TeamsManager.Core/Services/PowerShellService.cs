using System;
using System.Linq;
using System.Management.Automation;
using System.Management.Automation.Runspaces;
using Microsoft.Extensions.Logging;

namespace TeamsManager.Core.Services
{
    public class PowerShellService : IDisposable
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

        public bool ConnectToTeams(string username, string password)
        {
            try
            {
                _logger.LogInformation("Próbuję połączyć się z Microsoft Teams");

                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = _runspace;

                    ps.AddScript("Import-Module MicrosoftTeams"); // Operacja idempotentna - ponowne wykonanie powoduje taki sam efekt
                    ps.AddScript($"$securePassword = ConvertTo-SecureString '{password}' -AsPlainText -Force");
                    ps.AddScript($"$credential = New-Object System.Management.Automation.PSCredential ('{username}', $securePassword)");
                    ps.AddScript("Connect-MicrosoftTeams -Credential $credential");

                    var results = ps.Invoke();

                    if (ps.HadErrors)
                    {
                        foreach (var error in ps.Streams.Error)
                        {
                            _logger.LogError("Błąd Powershella: {Error}", error.ToString());
                        }
                        return false;
                    }

                    _isConnected = true;
                    _logger.LogInformation("Udało sie połączyć z Microsoft Teams");
                    return true;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nie udało się połączyć z Microsoft Teams");
                return false;
            }
        }

        public string CreateTeam(string displayName, string description, string owner)
        {
            if (!_isConnected)
                throw new InvalidOperationException("Nie połączono z TEAMS. Wywołaj ConnectToTeams najpierw.");

            try
            {
                _logger.LogInformation("Tworzenie zespołu: {TeamName}", displayName);

                using (var ps = PowerShell.Create())
                {
                    ps.Runspace = _runspace;
                    ps.AddScript($"New-Team -DisplayName '{displayName}' -Description '{description}' -Owner '{owner}'");

                    var results = ps.Invoke();

                    if (ps.HadErrors)
                    {
                        foreach (var error in ps.Streams.Error)
                        {
                            _logger.LogError("Błąd Powershella: {Error}", error.ToString());
                        }
                        throw new Exception("Nie udało się utworzyć zespołu.");
                    }

                    var teamId = results.FirstOrDefault()?.Properties["GroupId"]?.Value?.ToString();
                    _logger.LogInformation("Utworzono zespół o ID: {TeamId}", teamId);

                    return teamId ?? string.Empty;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tworzenie zespołu się nie powiodło: {TeamName}", displayName);
                throw;
            }
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