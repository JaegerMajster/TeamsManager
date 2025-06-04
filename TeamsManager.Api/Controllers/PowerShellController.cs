using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Asp.Versioning;
using TeamsManager.Core.Abstractions;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.Api.Controllers
{
    [ApiController]
    [ApiVersion("1.0")]
    [Route("api/v{version:apiVersion}/[controller]")]
    public class PowerShellController : ControllerBase
    {
        private readonly IPowerShellService _powerShellService;
        private readonly ICurrentUserService _currentUserService;
        private readonly ILogger<PowerShellController> _logger;

        public PowerShellController(
            IPowerShellService powerShellService,
            ICurrentUserService currentUserService,
            ILogger<PowerShellController> logger)
        {
            _powerShellService = powerShellService ?? throw new ArgumentNullException(nameof(powerShellService));
            _currentUserService = currentUserService ?? throw new ArgumentNullException(nameof(currentUserService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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
                
                // Użyj ExecuteWithAutoConnectAsync do testowego połączenia
                var connectionResult = await _powerShellService.ExecuteWithAutoConnectAsync(
                    apiAccessToken,
                    async () => 
                    {
                        // Prosta operacja testowa - sprawdź czy PowerShell jest połączony
                        return _powerShellService.IsConnected;
                    },
                    "Test połączenia PowerShell"
                );
                
                _logger.LogInformation("Wynik testu połączenia PowerShell: {Result}", connectionResult);
                
                return Ok(new
                {
                    Message = connectionResult == true ? 
                        "Pomyślnie nawiązano połączenie z Microsoft Graph przez PowerShell" :
                        "Nie udało się nawiązać połączenia z Microsoft Graph przez PowerShell",
                    IsConnected = connectionResult == true,
                    ConnectionAttempted = true,
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