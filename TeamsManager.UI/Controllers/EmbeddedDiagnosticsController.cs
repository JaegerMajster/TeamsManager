using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Configuration;
using TeamsManager.UI.Services.Auth;
using System;
using System.Linq;
using System.Collections.Generic;

namespace TeamsManager.UI.Controllers
{
    [ApiController]
    [Route("api/diagnostics")]
    public class EmbeddedDiagnosticsController : ControllerBase
    {
        private readonly ILogger<EmbeddedDiagnosticsController> _logger;
        private readonly IGraphConnectionService _graphConnectionService;
        private readonly IGraphService _graphService;
        private readonly AzureAdConfiguration? _azureAdConfig;
        private readonly EmbeddedOboTokenManager _oboTokenManager;

        public EmbeddedDiagnosticsController(
            ILogger<EmbeddedDiagnosticsController> logger,
            IGraphConnectionService graphConnectionService,
            IGraphService graphService,
            EmbeddedOboTokenManager oboTokenManager,
            AzureAdConfiguration? azureAdConfig = null)
        {
            _logger = logger;
            _graphConnectionService = graphConnectionService ?? throw new ArgumentNullException(nameof(graphConnectionService));
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            _oboTokenManager = oboTokenManager ?? throw new ArgumentNullException(nameof(oboTokenManager));
            _azureAdConfig = azureAdConfig;
        }

        [HttpGet("graph/status")]
        public async Task<IActionResult> GetGraphStatus()
        {
            _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Sprawdzanie stanu Graph API z tokenem OBO");
            
            try
            {
                // ✅ NAPRAWKA OBO: Pobierz token OBO z middleware
                var graphAccessToken = HttpContext.Items["GraphAccessToken"] as string;
                var userAccessToken = HttpContext.Items["UserAccessToken"] as string;
                
                if (string.IsNullOrEmpty(graphAccessToken))
                {
                    _logger.LogWarning("[EMBEDDED-DIAGNOSTICS] Brak tokenu OBO dla Graph API");
                    return Unauthorized(new { Error = "Brak tokenu dostępu", Message = "Wymagany Bearer token w Authorization header" });
                }

                _logger.LogDebug("[EMBEDDED-DIAGNOSTICS] Używanie tokenu OBO do sprawdzenia stanu Graph API");
                
                // TODO: Przekaż token OBO do GraphConnectionService
                // Na razie używamy standardowej metody, ale token będzie przekazany przez ModernHttpService
                var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                
                // Rozszerz informacje o dane z konfiguracji Azure AD
                var extendedHealthInfo = new
                {
                    healthInfo.IsConnected,
                    healthInfo.IsTokenValid,
                    healthInfo.Status,
                    healthInfo.LastChecked,
                    healthInfo.ResponseTimeMs,
                    healthInfo.GraphVersion,
                    healthInfo.LastError,
                    healthInfo.TokenExpiresAt,
                    healthInfo.IsHealthy,
                    // Dodatkowe informacje z konfiguracji
                    TenantId = _azureAdConfig?.TenantId ?? "Nieznany",
                    TenantName = _azureAdConfig?.TenantId ?? "Nieznany", 
                    ApplicationId = _azureAdConfig?.Api?.ClientId ?? "Nieznany",
                    HasConfiguration = _azureAdConfig != null,
                    ConfigurationValid = _azureAdConfig?.Api?.IsValid() == true,
                    // Informacje o tokenach OBO
                    HasUserToken = !string.IsNullOrEmpty(userAccessToken),
                    HasOboToken = !string.IsNullOrEmpty(graphAccessToken),
                    TokenFlow = "On-Behalf-Of (OBO)"
                };
                
                _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Status Graph API (OBO): {Status}, TenantId: {TenantId}, ApplicationId: {ApplicationId}", 
                    healthInfo.Status, extendedHealthInfo.TenantId, extendedHealthInfo.ApplicationId);
                return Ok(extendedHealthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMBEDDED-DIAGNOSTICS] Błąd sprawdzania stanu Graph API z tokenem OBO");
                return StatusCode(500, new { Error = "Internal server error", Message = ex.Message });
            }
        }

        [HttpPost("graph/permissions")]
        public async Task<IActionResult> CheckGraphPermissions()
        {
            _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Sprawdzanie uprawnień Graph API z tokenem OBO");
            
            try
            {
                // ✅ NAPRAWKA OBO: Pobierz token OBO z middleware
                var graphAccessToken = HttpContext.Items["GraphAccessToken"] as string;
                var userAccessToken = HttpContext.Items["UserAccessToken"] as string;
                
                if (string.IsNullOrEmpty(graphAccessToken))
                {
                    _logger.LogWarning("[EMBEDDED-DIAGNOSTICS] Brak tokenu OBO dla sprawdzenia uprawnień Graph API");
                    return Unauthorized(new { Error = "Brak tokenu dostępu", Message = "Wymagany Bearer token w Authorization header" });
                }

                _logger.LogDebug("[EMBEDDED-DIAGNOSTICS] Używanie tokenu OBO do sprawdzenia uprawnień Graph API");
                
                // TODO: Przekaż token OBO do GraphConnectionService
                // Na razie używamy standardowej metody, ale token będzie przekazany przez ModernHttpService
                var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                
                // Sprawdź wymagane uprawnienia dla TeamsManager
                var requiredPermissions = new[] 
                { 
                    "User.ReadWrite.All", 
                    "Directory.ReadWrite.All", 
                    "Group.ReadWrite.All",
                    "Team.ReadBasic.All",
                    "TeamMember.ReadWrite.All"
                };
                
                var hasAllPermissions = requiredPermissions.All(p => permissionInfo.HasPermission(p));
                var missingPermissions = requiredPermissions.Where(p => !permissionInfo.HasPermission(p)).ToList();
                var availablePermissions = requiredPermissions.Where(p => permissionInfo.HasPermission(p)).ToList();
                
                // Pobierz dane z rzeczywistej konfiguracji Azure AD
                var tenantId = _azureAdConfig?.TenantId ?? "Nieznany";
                var applicationId = _azureAdConfig?.Api?.ClientId ?? "Nieznany";
                // Nazwa tenanta może być uzyskana z domeny (np. ckziumm.edu.pl) lub pozostać jako TenantId
                var tenantName = tenantId.Contains(".") ? tenantId : $"Tenant-{tenantId}";
                
                var result = new GraphPermissionInfo
                {
                    HasRequiredPermissions = hasAllPermissions,
                    Status = hasAllPermissions ? GraphHealthStatus.Healthy : GraphHealthStatus.Critical,
                    AssignedPermissions = availablePermissions,
                    MissingPermissions = missingPermissions,
                    Errors = new List<string>(),
                    LastChecked = DateTime.UtcNow,
                    AuthenticationType = "Bearer (OBO)",
                    TokenExpiresAt = DateTime.UtcNow.AddHours(1), // Będzie zaktualizowane przez rzeczywisty serwis
                    TenantName = tenantName,
                    ApplicationId = applicationId
                };
                
                if (!hasAllPermissions)
                {
                    result.Errors.Add($"Brakujące uprawnienia: {string.Join(", ", missingPermissions)}");
                    result.Errors.Add($"Sprawdź konfigurację Azure AD (Tenant ID: {tenantId})");
                    result.Errors.Add($"Application ID: {applicationId}");
                    result.Errors.Add("Dodaj brakujące uprawnienia Graph API w Azure AD Portal");
                    result.Errors.Add("⚠️ Uprawnienia muszą być typu 'Delegated' dla przepływu OBO");
                }
                
                // Dodaj informacje o konfiguracji
                if (_azureAdConfig == null)
                {
                    result.Errors.Add("⚠️ Brak konfiguracji Azure AD w EmbeddedApiServer");
                }
                else if (_azureAdConfig.Api?.IsValid() != true)
                {
                    result.Errors.Add("⚠️ Nieprawidłowa konfiguracja Azure AD API");
                    if (string.IsNullOrEmpty(_azureAdConfig.Api?.ClientId))
                        result.Errors.Add("- Brakuje Client ID");
                    if (string.IsNullOrEmpty(_azureAdConfig.Api?.ClientSecret))
                        result.Errors.Add("- Brakuje Client Secret");
                    if (string.IsNullOrEmpty(_azureAdConfig.TenantId))
                        result.Errors.Add("- Brakuje Tenant ID");
                }
                
                if (!permissionInfo.IsValid)
                {
                    result.Errors.Add("Nieprawidłowe uprawnienia Graph API");
                    if (!string.IsNullOrEmpty(permissionInfo.ErrorMessage))
                    {
                        result.Errors.Add(permissionInfo.ErrorMessage);
                    }
                }
                
                // Dodaj informacje o przepływie OBO
                result.Errors.Add($"🔄 Przepływ uwierzytelniania: On-Behalf-Of (OBO)");
                result.Errors.Add($"👤 Token użytkownika: {(!string.IsNullOrEmpty(userAccessToken) ? "✅ Dostępny" : "❌ Niedostępny")}");
                result.Errors.Add($"🔑 Token OBO: {(!string.IsNullOrEmpty(graphAccessToken) ? "✅ Dostępny" : "❌ Niedostępny")}");
                
                _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Uprawnienia Graph API (OBO): {HasPermissions}", result.HasRequiredPermissions);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[EMBEDDED-DIAGNOSTICS] Błąd sprawdzania uprawnień Graph API z tokenem OBO");
                return StatusCode(500, new { Error = "Internal server error", Message = ex.Message });
            }
        }

        [HttpGet("health")]
        public IActionResult HealthCheck()
        {
            _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Health check");
            
            var userAccessToken = HttpContext.Items["UserAccessToken"] as string;
            var graphAccessToken = HttpContext.Items["GraphAccessToken"] as string;
            
            return Ok(new
            {
                Status = "Healthy",
                Message = "EmbeddedApiServer is running with OBO support",
                Timestamp = DateTime.UtcNow,
                Source = "EmbeddedApiServer",
                TokenFlow = "On-Behalf-Of (OBO)",
                HasUserToken = !string.IsNullOrEmpty(userAccessToken),
                HasOboToken = !string.IsNullOrEmpty(graphAccessToken)
            });
        }
    }
} 