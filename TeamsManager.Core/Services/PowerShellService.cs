using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShell
{
    /// <summary>
    /// Główny serwis fasadowy dla operacji PowerShell/Microsoft Graph
    /// Zoptymalizowany dla tenantów średniej wielkości (do 1000 użytkowników)
    /// </summary>
    public class PowerShellService : IPowerShellService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly ILogger<PowerShellService> _logger;

        // Lazy initialization dla serwisów domenowych
        private readonly Lazy<IPowerShellTeamManagementService> _teamService;
        private readonly Lazy<IPowerShellUserManagementService> _userService;
        private readonly Lazy<IPowerShellBulkOperationsService> _bulkOperationsService;

        private bool _disposed = false;

        /// <summary>
        /// Konstruktor serwisu PowerShell
        /// </summary>
        public PowerShellService(
            IPowerShellConnectionService connectionService,
            IServiceProvider serviceProvider,
            ILogger<PowerShellService> logger)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Lazy initialization pozwala uniknąć cyklicznych zależności
            // i ładuje serwisy tylko gdy są rzeczywiście potrzebne
            _teamService = new Lazy<IPowerShellTeamManagementService>(() =>
                serviceProvider.GetRequiredService<IPowerShellTeamManagementService>());
            _userService = new Lazy<IPowerShellUserManagementService>(() =>
                serviceProvider.GetRequiredService<IPowerShellUserManagementService>());
            _bulkOperationsService = new Lazy<IPowerShellBulkOperationsService>(() =>
                serviceProvider.GetRequiredService<IPowerShellBulkOperationsService>());

            _logger.LogInformation("PowerShell Service zainicjalizowany");
        }

        /// <inheritdoc />
        public bool IsConnected => _connectionService.IsConnected;

        /// <inheritdoc />
        public IPowerShellTeamManagementService Teams => _teamService.Value;

        /// <inheritdoc />
        public IPowerShellUserManagementService Users => _userService.Value;

        /// <inheritdoc />
        public IPowerShellBulkOperationsService BulkOperations => _bulkOperationsService.Value;

        /// <inheritdoc />
        public async Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null)
        {
            _logger.LogInformation("Łączenie z Microsoft Graph przez fasadę PowerShell");

            try
            {
                var result = await _connectionService.ConnectWithAccessTokenAsync(accessToken, scopes);

                if (result)
                {
                    _logger.LogInformation("Pomyślnie połączono z Microsoft Graph");
                }
                else
                {
                    _logger.LogWarning("Nie udało się połączyć z Microsoft Graph");
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas łączenia z Microsoft Graph");
                throw;
            }
        }

        /// <summary>
        /// Zwalnia zasoby
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Zwalnia zasoby
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                try
                {
                    // Dispose connection service który zarządza runspace
                    _connectionService?.Dispose();

                    // Dispose semaphore w bulk operations jeśli był utworzony
                    if (_bulkOperationsService.IsValueCreated &&
                        _bulkOperationsService.Value is IDisposable disposableBulk)
                    {
                        disposableBulk.Dispose();
                    }

                    _logger.LogInformation("PowerShell Service został poprawnie zamknięty");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Błąd podczas zamykania PowerShell Service");
                }
            }

            _disposed = true;
        }
    }
}