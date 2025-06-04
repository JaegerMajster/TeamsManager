using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Auth;

namespace TeamsManager.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class PowerShellController : ControllerBase
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ITokenManager _tokenManager;
        private readonly ILogger<PowerShellController> _logger;
        
        // Scopes dla PowerShell Microsoft Graph
        private readonly string[] _graphPowerShellScopes = new[] { 
            "https://graph.microsoft.com/User.Read", 
            "https://graph.microsoft.com/Group.ReadWrite.All", 
            "https://graph.microsoft.com/Directory.ReadWrite.All" 
        };

        public PowerShellController(
            IPowerShellService powerShellService,
            ICurrentUserService currentUserService,
            ITokenManager tokenManager,
            ILogger<PowerShellController> logger)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _tokenManager = tokenManager ?? throw new ArgumentNullException(nameof(tokenManager));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Metoda pomocnicza do uzyskania tokenu Graph przez TokenManager
        /// </summary>
        private async Task<string?> GetGraphTokenOnBehalfOfUserAsync(string apiAccessToken)
        {
            if (string.IsNullOrEmpty(apiAccessToken))
            {
                _logger.LogWarning("GetGraphTokenOnBehalfOfUserAsync: Token dostępu API jest pusty.");
                return null;
            }

            try
            {
                var userUpn = _currentUserService.GetCurrentUserUpn();
                if (string.IsNullOrEmpty(userUpn))
                {
                    _logger.LogWarning("GetGraphTokenOnBehalfOfUserAsync: Nie można określić UPN bieżącego użytkownika.");
                    return null;
                }
                return await _tokenManager.GetValidAccessTokenAsync(userUpn, apiAccessToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas uzyskiwania tokenu przez TokenManager");
                return null;
            }
        }

        /// <summary>
        /// Testuje połączenie PowerShell Service z Microsoft Graph
        /// </summary>
        [HttpPost("test-connection")]
        [Authorize]
        public async Task<IActionResult> TestConnection()
        {
            try
            {
                _logger.LogInformation("Wywołano endpoint /api/PowerShell/test-connection");

                // Pobierz informacje o użytkowniku
                var userUpn = _currentUserService.GetCurrentUserUpn();
                var userId = _currentUserService.GetCurrentUserId();

                _logger.LogInformation("Test PowerShell dla użytkownika: {UserUpn} (ID: {UserId})", userUpn, userId);

                // Sprawdź obecny stan połączenia
                var isCurrentlyConnected = _powerShellService.IsConnected;
                
                _logger.LogInformation("PowerShell Service - obecny stan połączenia: {IsConnected}", isCurrentlyConnected);

                // Jeśli nie jest połączony, spróbuj połączyć
                bool connectionAttempted = false;
                if (!isCurrentlyConnected)
                {
                    _logger.LogInformation("Próba nawiązania połączenia PowerShell z Microsoft Graph...");
                    
                    // Pobierz token lokalnego API z nagłówka Authorization
                    var authorizationHeader = Request.Headers["Authorization"].FirstOrDefault();
                    if (string.IsNullOrEmpty(authorizationHeader) || !authorizationHeader.StartsWith("Bearer "))
                    {
                        _logger.LogWarning("Brak tokenu Authorization w nagłówku żądania");
                        return BadRequest(new { 
                            Message = "Brak tokenu dostępu w nagłówku Authorization", 
                            IsConnected = false,
                            ConnectionAttempted = false
                        });
                    }

                    var apiAccessToken = authorizationHeader.Substring("Bearer ".Length).Trim();
                    
                    // POPRAWKA: Uzyskaj token Graph przez OBO zamiast używania tokenu lokalnego API
                    var graphAccessToken = await GetGraphTokenOnBehalfOfUserAsync(apiAccessToken);
                    
                    if (string.IsNullOrEmpty(graphAccessToken))
                    {
                        _logger.LogError("Nie udało się uzyskać tokenu Microsoft Graph przez OBO");
                        return StatusCode(StatusCodes.Status500InternalServerError, new
                        {
                            Message = "Nie udało się uzyskać tokenu Microsoft Graph. Może być wymagana zgoda administratora dla PowerShell scopes.",
                            IsConnected = false,
                            ConnectionAttempted = false,
                            UserUpn = userUpn,
                            RequiredScopes = _graphPowerShellScopes,
                            Timestamp = DateTime.UtcNow
                        });
                    }
                    
                    // Spróbuj połączyć z Graph używając tokenu Graph (nie lokalnego API)
                    var connectionResult = await _powerShellService.ConnectWithAccessTokenAsync(graphAccessToken, _graphPowerShellScopes);
                    connectionAttempted = true;
                    
                    _logger.LogInformation("Wynik próby połączenia PowerShell: {Result}", connectionResult);
                    
                    return Ok(new
                    {
                        Message = connectionResult ? 
                            "Pomyślnie nawiązano połączenie z Microsoft Graph przez PowerShell" :
                            "Nie udało się nawiązać połączenia z Microsoft Graph przez PowerShell",
                        IsConnected = connectionResult,
                        ConnectionAttempted = connectionAttempted,
                        UserUpn = userUpn,
                        UserId = userId,
                        UsedScopes = _graphPowerShellScopes,
                        Timestamp = DateTime.UtcNow
                    });
                }

                // Już połączony
                return Ok(new
                {
                    Message = "PowerShell Service jest już połączony z Microsoft Graph",
                    IsConnected = true,
                    ConnectionAttempted = connectionAttempted,
                    UserUpn = userUpn,
                    UserId = userId,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testowania połączenia PowerShell");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Message = "Błąd podczas testowania połączenia PowerShell",
                    Error = ex.Message,
                    IsConnected = false,
                    ConnectionAttempted = false,
                    Timestamp = DateTime.UtcNow
                });
            }
        }

        /// <summary>
        /// Sprawdza stan PowerShell Service bez próby połączenia
        /// </summary>
        [HttpGet("status")]
        [Authorize]
        public IActionResult GetStatus()
        {
            try
            {
                var userUpn = _currentUserService.GetCurrentUserUpn();
                var isConnected = _powerShellService.IsConnected;

                _logger.LogInformation("Sprawdzenie stanu PowerShell Service dla użytkownika: {UserUpn}, Status: {IsConnected}", userUpn, isConnected);

                return Ok(new
                {
                    IsConnected = isConnected,
                    UserUpn = userUpn,
                    Timestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania stanu PowerShell Service");
                return StatusCode(StatusCodes.Status500InternalServerError, new
                {
                    Message = "Błąd podczas sprawdzania stanu PowerShell Service",
                    Error = ex.Message,
                    Timestamp = DateTime.UtcNow
                });
            }
        }
    }
} 