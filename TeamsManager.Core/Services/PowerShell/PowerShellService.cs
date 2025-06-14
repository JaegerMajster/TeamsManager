using System;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.Core.Exceptions.PowerShell;
using TeamsManager.Core.Helpers.PowerShell;
using TeamsManager.Core.Models;

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
        public IPowerShellConnectionService Connection => _connectionService;

        /// <summary>
        /// Testuje połączenie i uprawnienia PowerShell/Graph
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Informacje diagnostyczne o połączeniu</returns>
        public async Task<PowerShellDiagnosticInfo> DiagnoseConnectionAsync(string apiAccessToken)
        {
            var diagnostic = new PowerShellDiagnosticInfo();
            var userUpn = _currentUserService.GetCurrentUserUpn();

            try
            {
                diagnostic.UserUpn = userUpn;
                diagnostic.HasApiToken = !string.IsNullOrEmpty(apiAccessToken);
                diagnostic.ApiTokenLength = apiAccessToken?.Length ?? 0;

                if (string.IsNullOrEmpty(userUpn))
                {
                    diagnostic.Errors.Add("Nie można określić UPN bieżącego użytkownika");
                    return diagnostic;
                }

                if (string.IsNullOrEmpty(apiAccessToken))
                {
                    diagnostic.Errors.Add("Brak tokenu dostępu API");
                    return diagnostic;
                }

                // Sprawdź czy można uzyskać Graph token
                var graphToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                diagnostic.HasGraphToken = !string.IsNullOrEmpty(graphToken);
                diagnostic.GraphTokenLength = graphToken?.Length ?? 0;

                if (string.IsNullOrEmpty(graphToken))
                {
                    diagnostic.Errors.Add("Nie udało się uzyskać Graph token");
                    return diagnostic;
                }

                // Sprawdź połączenie PowerShell
                diagnostic.IsConnected = _connectionService.IsConnected;
                
                if (!_connectionService.IsConnected)
                {
                    var connected = await _connectionService.ConnectWithAccessTokenAsync(graphToken);
                    diagnostic.IsConnected = connected;
                    
                    if (!connected)
                    {
                        diagnostic.Errors.Add("Nie udało się połączyć z Microsoft Graph");
                        return diagnostic;
                    }
                }

                // Sprawdź uprawnienia do tworzenia użytkowników
                diagnostic.HasUserCreationPermissions = await Users.ValidateUserCreationPermissionsAsync();
                
                if (!diagnostic.HasUserCreationPermissions)
                {
                    diagnostic.Errors.Add("Brak uprawnień do tworzenia użytkowników");
                }

                diagnostic.IsHealthy = diagnostic.HasApiToken && diagnostic.HasGraphToken && 
                                     diagnostic.IsConnected && diagnostic.HasUserCreationPermissions;

            }
            catch (Exception ex)
            {
                diagnostic.Errors.Add($"Błąd diagnostyki: {ex.Message}");
                _logger.LogError(ex, "Błąd podczas diagnostyki połączenia PowerShell");
            }

            return diagnostic;
        }

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
                _logger.LogDebug("Pobieranie Graph token dla użytkownika {UserUpn}", userUpn);
                var graphToken = await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
                
                if (string.IsNullOrEmpty(graphToken))
                {
                    var errorMessage = $"Nie udało się uzyskać Graph token dla operacji: {operationDescription ?? "Nieznana operacja"}";
                    _logger.LogError("ExecuteWithAutoConnectAsync: {Error}. UserUpn: {UserUpn}, ApiTokenLength: {TokenLength}", 
                        errorMessage, userUpn, apiAccessToken?.Length ?? 0);
                    
                    // Rzuć PowerShellConnectionException zamiast zwracać default
                    throw PowerShellConnectionException.ForTokenError(errorMessage);
                }

                _logger.LogDebug("Otrzymano Graph token o długości {TokenLength} znaków", graphToken.Length);

                // Upewnij się że mamy połączenie z Graph token
                if (!_connectionService.IsConnected)
                {
                    _logger.LogDebug("PowerShell nie jest połączony, próba nawiązania połączenia");
                    var connected = await _connectionService.ConnectWithAccessTokenAsync(graphToken);
                    if (!connected)
                    {
                        var errorMessage = "Nie udało się połączyć z Microsoft Graph";
                        _logger.LogError("ExecuteWithAutoConnectAsync: {Error}. GraphTokenLength: {TokenLength}", 
                            errorMessage, graphToken.Length);
                        
                        // Rzuć PowerShellConnectionException
                        throw PowerShellConnectionException.ForConnectionFailed(
                            errorMessage,
                            connectionUri: "https://graph.microsoft.com",
                            authenticationMethod: "AccessToken"
                        );
                    }
                    _logger.LogDebug("Pomyślnie nawiązano połączenie z Microsoft Graph");
                }
                else
                {
                    _logger.LogDebug("PowerShell jest już połączony z Microsoft Graph");
                }
                
                // Wykonaj operację bezpośrednio
                _logger.LogDebug("Wykonywanie operacji: {Operation}", operationDescription ?? "Nieznana operacja");
                var result = await operation();
                _logger.LogDebug("Operacja {Operation} zakończona pomyślnie", operationDescription ?? "Nieznana operacja");
                return result;
            }
            catch (PowerShellException ex)
            {
                // Przekaż własne wyjątki PowerShell bez zmian
                _logger.LogError(ex, "ExecuteWithAutoConnectAsync: Błąd PowerShell podczas operacji {Operation}. ExceptionType: {ExceptionType}", 
                    operationDescription ?? "Nieznana operacja", ex.GetType().Name);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ExecuteWithAutoConnectAsync: Nieoczekiwany błąd podczas operacji {Operation}. ExceptionType: {ExceptionType}, Message: {Message}", 
                    operationDescription ?? "Nieznana operacja", ex.GetType().Name, ex.Message);
                
                // Opakuj inne wyjątki w PowerShellCommandExecutionException
                throw new PowerShellCommandExecutionException(
                    $"Błąd podczas wykonania operacji: {operationDescription ?? "Nieznana operacja"}",
                    innerException: ex
                );
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