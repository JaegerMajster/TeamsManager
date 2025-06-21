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
                
                // Pobierz dane z GraphConnectionService
                var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                
                // ✅ NAPRAWKA: Konwertuj na GraphDiagnosticInfo dla okna diagnostyki
                var diagnosticInfo = new GraphDiagnosticInfo
                {
                    IsConnected = healthInfo.IsConnected,
                    IsAuthenticated = healthInfo.IsTokenValid, // ✅ KLUCZOWA NAPRAWKA
                    Status = healthInfo.Status,
                    ResponseTimeMs = healthInfo.ResponseTimeMs,
                    LastChecked = healthInfo.LastChecked,
                    Errors = string.IsNullOrEmpty(healthInfo.LastError) ? new List<string>() : new List<string> { healthInfo.LastError },
                    Warnings = new List<string>(),
                    AllTestsPassed = healthInfo.IsHealthy,
                    HasRequiredPermissions = permissionInfo.HasRequiredPermissions, // ✅ KLUCZOWA NAPRAWKA
                    GraphApiVersion = healthInfo.GraphVersion,
                    TenantId = _azureAdConfig?.TenantId ?? "Nieznany",
                    ApplicationId = _azureAdConfig?.Api?.ClientId ?? "Nieznany",
                    AdditionalInfo = new Dictionary<string, object>
                    {
                        ["TokenExpiresAt"] = healthInfo.TokenExpiresAt,
                        ["HasConfiguration"] = _azureAdConfig != null,
                        ["ConfigurationValid"] = _azureAdConfig?.Api?.IsValid() == true,
                        ["HasUserToken"] = !string.IsNullOrEmpty(userAccessToken),
                        ["HasOboToken"] = !string.IsNullOrEmpty(graphAccessToken),
                        ["TokenFlow"] = "On-Behalf-Of (OBO)",
                        ["AssignedPermissionsCount"] = permissionInfo.AssignedPermissions.Count,
                        ["MissingPermissionsCount"] = permissionInfo.MissingPermissions.Count
                    }
                };
                
                _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Status Graph API (OBO): {Status}, IsAuthenticated: {IsAuthenticated}, HasPermissions: {HasPermissions}", 
                    diagnosticInfo.Status, diagnosticInfo.IsAuthenticated, diagnosticInfo.HasRequiredPermissions);
                return Ok(diagnosticInfo);
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
                
                // Pobierz dane z request body (jeśli są)
                string[] requestedPermissions = null;
                try
                {
                    if (HttpContext.Request.ContentLength > 0)
                    {
                        using var reader = new System.IO.StreamReader(HttpContext.Request.Body);
                        var body = await reader.ReadToEndAsync();
                        if (!string.IsNullOrEmpty(body))
                        {
                            requestedPermissions = System.Text.Json.JsonSerializer.Deserialize<string[]>(body);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[EMBEDDED-DIAGNOSTICS] Błąd parsowania żądanych uprawnień z body");
                }
                
                // ✅ NAPRAWKA: Użyj rzeczywistych wyników z GraphConnectionService
                var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                
                // ✅ NAPRAWKA: Jeśli są żądane konkretne uprawnienia, sprawdź je
                if (requestedPermissions != null && requestedPermissions.Length > 0)
                {
                    var missingPermissions = requestedPermissions
                        .Where(p => !permissionInfo.AssignedPermissions.Contains(p))
                        .ToList();
                    
                    // Aktualizuj informacje o brakujących uprawnieniach
                    permissionInfo.MissingPermissions = missingPermissions;
                    permissionInfo.HasRequiredPermissions = missingPermissions.Count == 0;
                    
                    // Ustaw status na podstawie wyników
                    if (missingPermissions.Count == 0)
                    {
                        permissionInfo.Status = GraphHealthStatus.Healthy;
                    }
                    else if (permissionInfo.AssignedPermissions.Count > 0)
                    {
                        permissionInfo.Status = GraphHealthStatus.Warning;
                    }
                    else
                    {
                        permissionInfo.Status = GraphHealthStatus.Critical;
                    }
                    
                    _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Sprawdzono konkretne uprawnienia: Żądane={RequestedCount}, Przypisane={AssignedCount}, Brakujące={MissingCount}", 
                        requestedPermissions.Length, permissionInfo.AssignedPermissions.Count, missingPermissions.Count);
                }
                
                // Rozszerz informacje o dane z konfiguracji Azure AD
                var tenantId = _azureAdConfig?.TenantId ?? "Nieznany";
                var applicationId = _azureAdConfig?.Api?.ClientId ?? "Nieznany";
                var tenantName = tenantId.Contains(".") ? tenantId : $"Tenant-{tenantId}";
                
                // Aktualizuj informacje o konfiguracji
                permissionInfo.TenantName = tenantName;
                permissionInfo.ApplicationId = applicationId;
                permissionInfo.AuthenticationType = "Bearer (OBO)";
                
                // Dodaj informacje o przepływie OBO do listy błędów (jako informacje)
                var infoMessages = new List<string>();
                infoMessages.Add($"🔄 Przepływ uwierzytelniania: On-Behalf-Of (OBO)");
                infoMessages.Add($"👤 Token użytkownika: {(!string.IsNullOrEmpty(userAccessToken) ? "✅ Dostępny" : "❌ Niedostępny")}");
                infoMessages.Add($"🔑 Token OBO: {(!string.IsNullOrEmpty(graphAccessToken) ? "✅ Dostępny" : "❌ Niedostępny")}");
                infoMessages.Add($"📊 Potwierdzone uprawnienia ({permissionInfo.AssignedPermissions.Count}): {string.Join(", ", permissionInfo.AssignedPermissions)}");
                
                if (permissionInfo.MissingPermissions.Count > 0)
                {
                    infoMessages.Add($"⚠️ Brakujące uprawnienia ({permissionInfo.MissingPermissions.Count}): {string.Join(", ", permissionInfo.MissingPermissions)}");
                }
                
                // Dodaj informacje o konfiguracji
                if (_azureAdConfig == null)
                {
                    infoMessages.Add("⚠️ Brak konfiguracji Azure AD w EmbeddedApiServer");
                }
                else if (_azureAdConfig.Api?.IsValid() != true)
                {
                    infoMessages.Add("⚠️ Nieprawidłowa konfiguracja Azure AD API");
                    if (string.IsNullOrEmpty(_azureAdConfig.Api?.ClientId))
                        infoMessages.Add("- Brakuje Client ID");
                    if (string.IsNullOrEmpty(_azureAdConfig.Api?.ClientSecret))
                        infoMessages.Add("- Brakuje Client Secret");
                    if (string.IsNullOrEmpty(_azureAdConfig.TenantId))
                        infoMessages.Add("- Brakuje Tenant ID");
                }
                
                // ✅ NAPRAWKA: Nie nadpisuj Errors - dodaj tylko informacje
                permissionInfo.Errors.AddRange(infoMessages);
                
                _logger.LogInformation("[EMBEDDED-DIAGNOSTICS] Uprawnienia Graph API (OBO): {HasPermissions}, Status: {Status}, Przypisane: {AssignedCount}", 
                    permissionInfo.HasRequiredPermissions, permissionInfo.Status, permissionInfo.AssignedPermissions.Count);
                    
                return Ok(permissionInfo);
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