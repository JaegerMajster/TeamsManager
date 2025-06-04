using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions.Services.PowerShell;

namespace TeamsManager.Core.Services.PowerShellServices
{
    /// <summary>
    /// Główny serwis fasadowy dla operacji PowerShell/Microsoft Graph
    /// Zoptymalizowany dla tenantów średniej wielkości (do 1000 użytkowników)
    /// </summary>
    public class PowerShellService : IPowerShellService
    {
        private readonly IPowerShellConnectionService _connectionService;
        private readonly ILogger<PowerShellService> _logger;
        private readonly ITokenManager _tokenManager;
        private readonly ICurrentUserService _currentUserService;

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
            ILogger<PowerShellService> logger,
            ITokenManager tokenManager,
            ICurrentUserService currentUserService)
        {
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));

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
                _logger.LogError(ex, "Błąd podczas łączenia z Microsoft Graph przez fasadę PowerShell");
                return false;
            }
        }

        /// <inheritdoc />
        public async Task<T?> ExecuteWithAutoConnectAsync<T>(string apiAccessToken, Func<Task<T>> operation, string? operationDescription = null)
        {
            if (string.IsNullOrEmpty(apiAccessToken))
            {
                _logger.LogWarning("ExecuteWithAutoConnectAsync: Token dostępu API jest pusty.");
                return default(T);
            }

            var userUpn = _currentUserService.GetCurrentUserUpn();
            if (string.IsNullOrEmpty(userUpn))
            {
                _logger.LogWarning("ExecuteWithAutoConnectAsync: Nie można określić UPN bieżącego użytkownika.");
                return default(T);
            }

            _logger.LogDebug("ExecuteWithAutoConnectAsync: {Operation} dla użytkownika {UserUpn}", 
                operationDescription ?? "Nieznana operacja", userUpn);

            try
            {
                // Pobierz Graph token przez TokenManager (OBO flow)
                var graphToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                
                if (string.IsNullOrEmpty(graphToken))
                {
                    _logger.LogError("ExecuteWithAutoConnectAsync: Nie udało się uzyskać Graph token dla operacji: {Operation}", 
                        operationDescription ?? "Nieznana operacja");
                    return default(T);
                }

                // Upewnij się że mamy połączenie z Graph token
                if (!_connectionService.IsConnected)
                {
                    var connected = await _connectionService.ConnectWithAccessTokenAsync(graphToken);
                    if (!connected)
                    {
                        _logger.LogError("ExecuteWithAutoConnectAsync: Nie udało się połączyć z Microsoft Graph");
                        return default(T);
                    }
                }
                
                // Wykonaj operację bezpośrednio
                return await operation();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteWithAutoConnectAsync: Błąd podczas wykonania operacji {Operation}", 
                    operationDescription ?? "Nieznana operacja");
                return default(T);
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