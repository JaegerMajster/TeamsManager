using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
// using TeamsManager.Api.Extensions; // Usunięte - nie potrzebne w embedded

namespace TeamsManager.UI.Controllers
{
    /// <summary>
    /// Kontroler do diagnostyki systemu Graph API
    /// </summary>
    [ApiController]
    [Route("api/advanced-diagnostics")]
    [Authorize]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IGraphConnectionService _graphConnectionService;
        private readonly IGraphService _graphService;
        private readonly ILogger<DiagnosticsController> _logger;

        public DiagnosticsController(
            IGraphConnectionService graphConnectionService,
            IGraphService graphService,
            ILogger<DiagnosticsController> logger)
        {
            _graphConnectionService = graphConnectionService ?? throw new ArgumentNullException(nameof(graphConnectionService));
            _graphService = graphService ?? throw new ArgumentNullException(nameof(graphService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Wykonuje podstawową diagnostykę połączenia Graph API
        /// </summary>
        /// <returns>Informacje diagnostyczne Graph API</returns>
        [HttpGet("connection")]
        public async Task<ActionResult<GraphDiagnosticInfo>> DiagnoseConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie diagnostyki połączenia Graph API");
                
                var diagnostic = await _graphConnectionService.GetDiagnosticInfoAsync();
                
                _logger.LogInformation("Diagnostyka zakończona. Stan: {Status}", diagnostic.Status);
                
                return Ok(diagnostic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas diagnostyki połączenia Graph API");
                return StatusCode(500, new { error = "Błąd podczas diagnostyki połączenia Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Wykonuje rozszerzoną diagnostykę z testowaniem konkretnych endpointów Graph API
        /// </summary>
        /// <param name="testEndpoints">Lista endpointów do przetestowania</param>
        /// <param name="includePermissions">Czy sprawdzić uprawnienia</param>
        /// <returns>Szczegółowe informacje diagnostyczne Graph API</returns>
        [HttpPost("connection/extended")]
        public async Task<ActionResult<GraphDiagnosticInfo>> DiagnoseConnectionExtendedAsync(
            [FromBody] string[]? testEndpoints = null,
            [FromQuery] bool includePermissions = true)
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie rozszerzonej diagnostyki Graph API");
                
                var diagnostic = await _graphConnectionService.GetDiagnosticInfoAsync();
                
                if (testEndpoints != null && testEndpoints.Any())
                {
                    foreach (var endpoint in testEndpoints)
                    {
                        try
                        {
                            var availability = await _graphConnectionService.CheckEndpointAvailabilityAsync(endpoint);
                            diagnostic.AdditionalInfo[$"Endpoint_{endpoint}"] = availability.IsAvailable;
                            
                            if (!availability.IsAvailable)
                            {
                                diagnostic.Errors.Add($"Endpoint {endpoint} nie jest dostępny: {availability.ErrorMessage}");
                            }
                        }
                        catch (Exception ex)
                        {
                            diagnostic.Errors.Add($"Błąd sprawdzania endpointu {endpoint}: {ex.Message}");
                        }
                    }
                }

                _logger.LogInformation("Rozszerzona diagnostyka zakończona. Stan: {Status}", diagnostic.Status);
                
                return Ok(diagnostic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas rozszerzonej diagnostyki Graph API");
                return StatusCode(500, new { error = "Błąd podczas rozszerzonej diagnostyki Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia Graph API dla konkretnych operacji
        /// </summary>
        /// <param name="permissions">Lista uprawnień do sprawdzenia</param>
        /// <returns>Informacje o uprawnieniach Graph API</returns>
        [HttpPost("permissions")]
        public async Task<ActionResult<GraphPermissionInfo>> ValidatePermissionsAsync([FromBody] string[] permissions)
        {
            try
            {
                if (permissions == null || !permissions.Any())
                {
                    return BadRequest(new { error = "Lista uprawnień nie może być pusta" });
                }

                _logger.LogInformation("Sprawdzanie uprawnień Graph API: {Permissions}", string.Join(", ", permissions));
                
                var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                
                var hasAllPermissions = permissions.All(p => permissionInfo.HasPermission(p));
                permissionInfo.HasRequiredPermissions = hasAllPermissions;
                
                if (!hasAllPermissions)
                {
                    var missingPermissions = permissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
                    permissionInfo.Errors.Add($"Brakujące uprawnienia: {string.Join(", ", missingPermissions)}");
                }
                
                _logger.LogInformation("Sprawdzenie uprawnień zakończone. IsValid: {IsValid}", permissionInfo.IsValid);
                
                return Ok(permissionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień Graph API");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania uprawnień Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Pobiera podstawowe informacje o stanie połączenia Graph API
        /// </summary>
        /// <returns>Informacje o stanie połączenia Graph API</returns>
        [HttpGet("health")]
        public async Task<ActionResult<GraphConnectionHealthInfo>> GetConnectionHealthAsync()
        {
            try
            {
                var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                return Ok(healthInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania informacji o stanie połączenia Graph API");
                return StatusCode(500, new { error = "Błąd podczas pobierania stanu połączenia Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia wymagane do tworzenia użytkowników Graph API
        /// </summary>
        /// <returns>Informacje o uprawnieniach do tworzenia użytkowników</returns>
        [HttpGet("permissions/user-creation")]
        public async Task<ActionResult<GraphPermissionInfo>> ValidateUserCreationPermissionsAsync()
        {
            try
            {
                var requiredPermissions = new[] 
                { 
                    "User.ReadWrite.All", 
                    "Directory.ReadWrite.All",
                    "Group.ReadWrite.All"
                };

                var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                
                // Sprawdź czy wszystkie wymagane uprawnienia są dostępne
                var hasAllPermissions = requiredPermissions.All(p => permissionInfo.HasPermission(p));
                permissionInfo.HasRequiredPermissions = hasAllPermissions;
                
                if (!hasAllPermissions)
                {
                    var missingPermissions = requiredPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
                    permissionInfo.Errors.Add($"Brakujące uprawnienia do tworzenia użytkowników: {string.Join(", ", missingPermissions)}");
                }
                
                return Ok(permissionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień do tworzenia użytkowników Graph API");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania uprawnień Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia wymagane do zarządzania zespołami Graph API
        /// </summary>
        /// <returns>Informacje o uprawnieniach do zarządzania zespołami</returns>
        [HttpGet("permissions/team-management")]
        public async Task<ActionResult<GraphPermissionInfo>> ValidateTeamManagementPermissionsAsync()
        {
            try
            {
                var requiredPermissions = new[] 
                { 
                    "Group.ReadWrite.All", 
                    "Directory.ReadWrite.All",
                    "TeamMember.ReadWrite.All"
                };

                var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                
                // Sprawdź czy wszystkie wymagane uprawnienia są dostępne
                var hasAllPermissions = requiredPermissions.All(p => permissionInfo.HasPermission(p));
                permissionInfo.HasRequiredPermissions = hasAllPermissions;
                
                if (!hasAllPermissions)
                {
                    var missingPermissions = requiredPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
                    permissionInfo.Errors.Add($"Brakujące uprawnienia do zarządzania zespołami: {string.Join(", ", missingPermissions)}");
                }
                
                return Ok(permissionInfo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania uprawnień do zarządzania zespołami Graph API");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania uprawnień Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Testuje wykonanie konkretnej operacji Graph API
        /// </summary>
        /// <param name="operationName">Nazwa operacji do przetestowania</param>
        /// <param name="requiredPermissions">Wymagane uprawnienia</param>
        /// <returns>Wynik testu operacji</returns>
        [HttpPost("test-operation")]
        public async Task<ActionResult<object>> TestOperationAsync(
            [FromQuery] string operationName,
            [FromBody] string[]? requiredPermissions = null)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(operationName))
                {
                    return BadRequest(new { error = "Nazwa operacji jest wymagana" });
                }

                _logger.LogInformation("Testowanie operacji Graph API: {OperationName}", operationName);

                var permissions = requiredPermissions ?? Array.Empty<string>();
                
                // Sprawdź uprawnienia jeśli podano
                GraphPermissionInfo? permissionInfo = null;
                if (permissions.Any())
                {
                    permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                    var hasAllPermissions = permissions.All(p => permissionInfo.HasPermission(p));
                    
                    if (!hasAllPermissions)
                    {
                        var missingPermissions = permissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
                        return Ok(new
                        {
                            OperationName = operationName,
                            CanExecute = false,
                            Reason = "Niewystarczające uprawnienia",
                            MissingPermissions = missingPermissions,
                            PermissionInfo = permissionInfo
                        });
                    }
                }

                // Sprawdź podstawowy stan połączenia
                var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                if (!healthInfo.IsConnected)
                {
                    return Ok(new
                    {
                        OperationName = operationName,
                        CanExecute = false,
                        Reason = "Brak połączenia z Microsoft Graph",
                        HealthInfo = healthInfo
                    });
                }

                // Wykonaj test połączenia Graph API
                var testResult = await _graphConnectionService.TestConnectionAsync();
                var canExecute = testResult.AllTestsPassed;

                return Ok(new
                {
                    OperationName = operationName,
                    CanExecute = canExecute,
                    Reason = canExecute ? "Operacja może być wykonana" : "Test operacji nie powiódł się",
                    PermissionInfo = permissionInfo,
                    HealthInfo = healthInfo,
                    TestResult = testResult,
                    TestTimestamp = DateTime.UtcNow
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testowania operacji Graph API {OperationName}", operationName);
                return StatusCode(500, new { error = "Błąd podczas testowania operacji Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Pobiera pełny raport diagnostyczny systemu Graph API
        /// </summary>
        /// <returns>Kompleksowy raport diagnostyczny Graph API</returns>
        [HttpGet("full-report")]
        public async Task<ActionResult<object>> GetFullDiagnosticReportAsync()
        {
            try
            {
                _logger.LogInformation("Generowanie pełnego raportu diagnostycznego Graph API");

                var healthTask = _graphConnectionService.GetConnectionHealthAsync();
                var diagnosticTask = _graphConnectionService.GetDiagnosticInfoAsync();
                var permissionTask = _graphConnectionService.GetPermissionInfoAsync();

                await Task.WhenAll(healthTask, diagnosticTask, permissionTask);

                var healthInfo = await healthTask;
                var diagnostic = await diagnosticTask;
                var permissionInfo = await permissionTask;

                var report = new
                {
                    GeneratedAt = DateTime.UtcNow,
                    OverallStatus = diagnostic.Status.ToString(),
                    Summary = new
                    {
                        IsHealthy = diagnostic.Status == GraphHealthStatus.Healthy,
                        HasCriticalIssues = diagnostic.Status == GraphHealthStatus.Critical,
                        ErrorCount = diagnostic.Errors.Count,
                        PermissionsValid = permissionInfo.IsValid,
                        IsConnected = healthInfo.IsConnected,
                        TokenValid = healthInfo.IsTokenValid,
                        ResponseTimeMs = healthInfo.ResponseTimeMs
                    },
                    ConnectionHealth = healthInfo,
                    DetailedDiagnostic = diagnostic,
                    Permissions = permissionInfo,
                    Recommendations = GenerateRecommendations(diagnostic, permissionInfo)
                };

                return Ok(report);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas generowania pełnego raportu diagnostycznego Graph API");
                return StatusCode(500, new { error = "Błąd podczas generowania raportu Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Generuje rekomendacje na podstawie diagnostyki Graph API
        /// </summary>
        private List<string> GenerateRecommendations(GraphDiagnosticInfo diagnostic, GraphPermissionInfo permissionInfo)
        {
            var recommendations = new List<string>();

            if (!diagnostic.IsConnected)
            {
                recommendations.Add("Sprawdź połączenie z Microsoft Graph API");
            }

            if (!diagnostic.IsAuthenticated)
            {
                recommendations.Add("Sprawdź uwierzytelnienie i token dostępu Graph API");
            }

            if (!diagnostic.HasRequiredPermissions)
            {
                recommendations.Add("Sprawdź uprawnienia aplikacji w Azure AD");
            }

            if (!diagnostic.AllTestsPassed)
            {
                recommendations.Add("Sprawdź podstawową funkcjonalność Graph API");
            }

            if (!permissionInfo.IsValid)
            {
                recommendations.Add("Sprawdź uprawnienia aplikacji w Azure AD Portal");
            }

            if (diagnostic.Errors.Any())
            {
                recommendations.Add("Przejrzyj szczegóły błędów w sekcji diagnostycznej");
            }

            if (diagnostic.ResponseTimeMs > 2000)
            {
                recommendations.Add("Sprawdź wydajność połączenia z Graph API - czas odpowiedzi przekracza 2 sekundy");
            }

            if (diagnostic.RateLimitInfo?.IsLimitReached == true)
            {
                recommendations.Add("Osiągnięto limit żądań Graph API - rozważ implementację throttling");
            }

            if (diagnostic.Status == GraphHealthStatus.Critical)
            {
                recommendations.Add("System Graph API wymaga natychmiastowej uwagi - status krytyczny");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("System Graph API działa prawidłowo");
            }

            return recommendations;
        }

        /// <summary>
        /// Sprawdza status konfiguracji Graph API
        /// </summary>
        /// <returns>Status konfiguracji Graph API</returns>
        [HttpGet("configuration/status")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> GetConfigurationStatusAsync()
        {
            try
            {
                _logger.LogInformation("Sprawdzanie statusu konfiguracji Graph API");
                
                var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                var diagnosticInfo = await _graphConnectionService.GetDiagnosticInfoAsync();
                
                var configurationStatus = new
                {
                    OverallStatus = diagnosticInfo.Status.ToString(),
                    IsConfigured = healthInfo.IsConnected && diagnosticInfo.IsAuthenticated,
                    ConnectionHealth = healthInfo.IsConnected,
                    TokenValid = healthInfo.IsTokenValid,
                    HasRequiredPermissions = diagnosticInfo.HasRequiredPermissions,
                    GraphApiVersion = diagnosticInfo.GraphApiVersion ?? "v1.0",
                    TenantId = diagnosticInfo.TenantId,
                    ApplicationId = diagnosticInfo.ApplicationId,
                    LastChecked = DateTime.UtcNow,
                    Issues = diagnosticInfo.Errors.Count > 0 ? diagnosticInfo.Errors : new List<string>()
                };
                
                _logger.LogInformation("Status konfiguracji sprawdzony. Status: {OverallStatus}, Skonfigurowane: {IsConfigured}",
                    configurationStatus.OverallStatus, configurationStatus.IsConfigured);
                
                return Ok(configurationStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania statusu konfiguracji Graph API");
                return StatusCode(500, new { error = "Błąd podczas sprawdzania statusu konfiguracji Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Odświeża token dostępu Graph API
        /// </summary>
        /// <param name="forceRefresh">Czy wymusić odświeżenie tokenu</param>
        /// <returns>Wynik odświeżenia tokenu</returns>
        [HttpPost("token/refresh")]
        [AllowAnonymous]
        public async Task<ActionResult<object>> RefreshTokenAsync([FromQuery] bool forceRefresh = false)
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie odświeżania tokenu Graph API (Force: {ForceRefresh})", forceRefresh);
                
                var refreshResult = forceRefresh ? 
                    await _graphConnectionService.RefreshTokenIfNeededAsync() :
                    await _graphConnectionService.IsTokenValidAsync();
                
                var result = new
                {
                    Success = refreshResult,
                    Message = refreshResult ? "Token jest ważny lub został pomyślnie odświeżony" : "Nie udało się odświeżyć tokenu",
                    TokenValid = await _graphConnectionService.IsTokenValidAsync(),
                    RefreshedAt = DateTime.UtcNow
                };
                
                _logger.LogInformation("Odświeżanie tokenu zakończone. Sukces: {Success}", result.Success);
                
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas odświeżania tokenu Graph API");
                return StatusCode(500, new { error = "Błąd podczas odświeżania tokenu Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// TASK 4.1.2 - Pobiera status Graph API z szczegółowymi informacjami
        /// </summary>
        /// <returns>Szczegółowy status Graph API</returns>
        [HttpGet("graph/status")]
        public async Task<IActionResult> GetGraphStatus()
        {
            try
            {
                _logger.LogInformation("[DIAGNOSTIC] Rozpoczynam diagnostykę Graph API");
                _logger.LogInformation("[DIAGNOSTIC] Sprawdzanie nagłówków żądania...");
                
                // Sprawdź nagłówek Authorization
                var authHeader = Request.Headers.Authorization.FirstOrDefault();
                _logger.LogInformation("[DIAGNOSTIC] Authorization header: {AuthHeader}", 
                    string.IsNullOrEmpty(authHeader) ? "BRAK" : $"Bearer {authHeader.Substring(0, Math.Min(50, authHeader.Length))}...");
                
                // Sprawdź User-Agent
                var userAgent = Request.Headers.UserAgent.FirstOrDefault();
                _logger.LogInformation("[DIAGNOSTIC] User-Agent: {UserAgent}", userAgent ?? "BRAK");
                
                // Sprawdź inne nagłówki
                _logger.LogInformation("[DIAGNOSTIC] Content-Type: {ContentType}", Request.ContentType ?? "BRAK");
                _logger.LogInformation("[DIAGNOSTIC] Host: {Host}", Request.Host.ToString());
                
                var diagnostic = await _graphService.Connection.GetDiagnosticInfoAsync();
                
                _logger.LogInformation("[DIAGNOSTIC] Diagnostyka Graph API zakończona:");
                _logger.LogInformation("[DIAGNOSTIC] - IsConnected: {IsConnected}", diagnostic.IsConnected);
                _logger.LogInformation("[DIAGNOSTIC] - Status: {Status}", diagnostic.Status);
                _logger.LogInformation("[DIAGNOSTIC] - TenantId: {TenantId}", diagnostic.TenantId ?? "NULL");
                _logger.LogInformation("[DIAGNOSTIC] - Errors count: {ErrorsCount}", diagnostic.Errors?.Count ?? 0);
                _logger.LogInformation("[DIAGNOSTIC] - Warnings count: {WarningsCount}", diagnostic.Warnings?.Count ?? 0);
                
                if (diagnostic.Errors?.Any() == true)
                {
                    _logger.LogInformation("[DIAGNOSTIC] Błędy: {Errors}", string.Join(", ", diagnostic.Errors));
                }
                
                if (diagnostic.Warnings?.Any() == true)
                {
                    _logger.LogInformation("[DIAGNOSTIC] Ostrzeżenia: {Warnings}", string.Join(", ", diagnostic.Warnings));
                }
                
                return Ok(diagnostic);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNOSTIC] Błąd podczas diagnostyki Graph API: {Message}", ex.Message);
                _logger.LogError("[DIAGNOSTIC] Exception StackTrace: {StackTrace}", ex.StackTrace);
                return StatusCode(500, new { error = "Błąd diagnostyki Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// TASK 4.1.3 - Wykonuje kompleksowy test Graph API z konfigurowalnymi parametrami
        /// </summary>
        /// <param name="testRequest">Parametry testu Graph API</param>
        /// <returns>Wyniki testów Graph API</returns>
        [HttpPost("graph/test")]
        public async Task<ActionResult<object>> TestGraphApiAsync([FromBody] EmbeddedGraphTestRequest? testRequest = null)
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie kompleksowego testu Graph API");

                var request = testRequest ?? new EmbeddedGraphTestRequest();
                var testResults = new List<object>();
                var startTime = DateTime.UtcNow;

                // Test 1: Podstawowe połączenie
                var connectionTest = new
                {
                    TestName = "Test połączenia",
                    Description = "Sprawdzenie podstawowego połączenia z Graph API"
                };

                try
                {
                    var healthInfo = await _graphConnectionService.GetConnectionHealthAsync();
                    testResults.Add(new
                    {
                        connectionTest.TestName,
                        connectionTest.Description,
                        Success = healthInfo.IsConnected,
                        ResponseTimeMs = healthInfo.ResponseTimeMs,
                        Details = new
                        {
                            IsConnected = healthInfo.IsConnected,
                            TokenValid = healthInfo.IsTokenValid,
                            TokenExpiresAt = healthInfo.TokenExpiresAt,
                            LastError = healthInfo.LastError
                        }
                    });
                }
                catch (Exception ex)
                {
                    testResults.Add(new
                    {
                        connectionTest.TestName,
                        connectionTest.Description,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }

                // Test 2: Uwierzytelnienie i kontekst użytkownika
                var authTest = new
                {
                    TestName = "Test uwierzytelnienia",
                    Description = "Sprawdzenie uwierzytelnienia i kontekstu użytkownika"
                };

                try
                {
                    var userContext = await _graphConnectionService.GetUserContextAsync();
                    testResults.Add(new
                    {
                        authTest.TestName,
                        authTest.Description,
                        Success = userContext.IsAuthenticated,
                        Details = new
                        {
                            IsAuthenticated = userContext.IsAuthenticated,
                            UserPrincipalName = userContext.UserPrincipalName,
                            DisplayName = userContext.DisplayName,
                            TenantId = userContext.TenantId,
                            RolesCount = userContext.Roles?.Count ?? 0
                        }
                    });
                }
                catch (Exception ex)
                {
                    testResults.Add(new
                    {
                        authTest.TestName,
                        authTest.Description,
                        Success = false,
                        ErrorMessage = ex.Message
                    });
                }

                // Test 3: Uprawnienia
                if (request.TestPermissions)
                {
                    var permissionTest = new
                    {
                        TestName = "Test uprawnień",
                        Description = "Sprawdzenie uprawnień aplikacji"
                    };

                    try
                    {
                        var permissionInfo = await _graphConnectionService.GetPermissionInfoAsync();
                        testResults.Add(new
                        {
                            permissionTest.TestName,
                            permissionTest.Description,
                            Success = permissionInfo.IsValid,
                            Details = new
                            {
                                IsValid = permissionInfo.IsValid,
                                AvailablePermissions = permissionInfo.AvailablePermissions?.Count ?? 0,
                                ErrorMessage = permissionInfo.ErrorMessage
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        testResults.Add(new
                        {
                            permissionTest.TestName,
                            permissionTest.Description,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                // Test 4: Endpointy Graph API
                if (request.TestEndpoints && request.EndpointsToTest?.Any() == true)
                {
                    foreach (var endpoint in request.EndpointsToTest)
                    {
                        var endpointTest = new
                        {
                            TestName = $"Test endpointu: {endpoint}",
                            Description = $"Sprawdzenie dostępności endpointu {endpoint}"
                        };

                        try
                        {
                            var availability = await _graphConnectionService.CheckEndpointAvailabilityAsync(endpoint);
                            testResults.Add(new
                            {
                                endpointTest.TestName,
                                endpointTest.Description,
                                Success = availability.IsAvailable,
                                Details = new
                                {
                                    IsAvailable = availability.IsAvailable,
                                    ResponseTimeMs = availability.ResponseTimeMs,
                                    HttpStatusCode = availability.HttpStatusCode,
                                    ErrorMessage = availability.ErrorMessage
                                }
                            });
                        }
                        catch (Exception ex)
                        {
                            testResults.Add(new
                            {
                                endpointTest.TestName,
                                endpointTest.Description,
                                Success = false,
                                ErrorMessage = ex.Message
                            });
                        }
                    }
                }

                // Test 5: Rate Limiting
                if (request.TestRateLimit)
                {
                    var rateLimitTest = new
                    {
                        TestName = "Test limitów żądań",
                        Description = "Sprawdzenie statusu rate limiting"
                    };

                    try
                    {
                        var rateLimitStatus = await _graphConnectionService.GetRateLimitStatusAsync();
                        testResults.Add(new
                        {
                            rateLimitTest.TestName,
                            rateLimitTest.Description,
                            Success = rateLimitStatus?.IsLimitReached != true,
                            Details = new
                            {
                                RemainingRequests = rateLimitStatus?.RemainingRequests,
                                MaxRequests = rateLimitStatus?.MaxRequests,
                                UsagePercentage = rateLimitStatus?.UsagePercentage,
                                IsLimitReached = rateLimitStatus?.IsLimitReached,
                                ResetTime = rateLimitStatus?.ResetTime
                            }
                        });
                    }
                    catch (Exception ex)
                    {
                        testResults.Add(new
                        {
                            rateLimitTest.TestName,
                            rateLimitTest.Description,
                            Success = false,
                            ErrorMessage = ex.Message
                        });
                    }
                }

                var endTime = DateTime.UtcNow;
                var duration = endTime - startTime;

                var successfulTests = testResults.Count(t => (bool)t.GetType().GetProperty("Success")?.GetValue(t)!);
                var totalTests = testResults.Count;

                var testSummary = new
                {
                    TestStartTime = startTime,
                    TestEndTime = endTime,
                    TestDurationMs = duration.TotalMilliseconds,
                    TotalTests = totalTests,
                    SuccessfulTests = successfulTests,
                    FailedTests = totalTests - successfulTests,
                    SuccessRate = totalTests > 0 ? (double)successfulTests / totalTests * 100.0 : 0.0,
                    OverallResult = successfulTests == totalTests ? "PASSED" : 
                                   successfulTests > 0 ? "PARTIAL" : "FAILED",
                    TestConfiguration = request,
                    TestResults = testResults
                };

                _logger.LogInformation("Test Graph API zakończony. Wynik: {OverallResult}, Testy przeszły: {SuccessfulTests}/{TotalTests}",
                    testSummary.OverallResult, successfulTests, totalTests);

                return Ok(testSummary);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas kompleksowego testu Graph API");
                return StatusCode(500, new { error = "Błąd podczas testu Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Wykonuje kompleksowy test połączenia z Microsoft Graph API
        /// </summary>
        /// <returns>Wynik testu połączenia Graph API</returns>
        [HttpPost("connection/test")]
        [AllowAnonymous]
        public async Task<ActionResult<GraphConnectionTestResult>> TestConnectionAsync()
        {
            try
            {
                _logger.LogInformation("Rozpoczynanie testu połączenia Graph API");
                
                var testResult = await _graphConnectionService.TestConnectionAsync();
                
                _logger.LogInformation("Test połączenia Graph API zakończony. Wynik: {AllTestsPassed}, Testy przeszły: {PassedTests}/{TotalTests}",
                    testResult.AllTestsPassed, testResult.PassedTests, testResult.TotalTests);
                
                return Ok(testResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas testu połączenia Graph API");
                return StatusCode(500, new { error = "Błąd podczas testu połączenia Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// TASK 4.1.4 - Pobiera szczegółowe informacje o uprawnieniach Graph API
        /// </summary>
        /// <returns>Szczegółowe informacje o uprawnieniach Graph API</returns>
        [HttpGet("graph/permissions")]
        public async Task<ActionResult<object>> GetGraphPermissionsAsync()
        {
            try
            {
                _logger.LogInformation("Pobieranie szczegółowych informacji o uprawnieniach Graph API");

                var permissionTask = _graphConnectionService.GetPermissionInfoAsync();
                var userContextTask = _graphConnectionService.GetUserContextAsync();

                await Task.WhenAll(permissionTask, userContextTask);

                var permissionInfo = await permissionTask;
                var userContext = await userContextTask;

                // Sprawdź uprawnienia dla różnych kategorii operacji
                var userManagementPermissions = new[]
                {
                    "User.Read.All", "User.ReadWrite.All", "Directory.Read.All", "Directory.ReadWrite.All"
                };

                var teamManagementPermissions = new[]
                {
                    "Group.Read.All", "Group.ReadWrite.All", "Team.ReadBasic.All", "TeamMember.ReadWrite.All"
                };

                var mailPermissions = new[]
                {
                    "Mail.Read", "Mail.ReadWrite", "Mail.Send"
                };

                var calendarPermissions = new[]
                {
                    "Calendars.Read", "Calendars.ReadWrite"
                };

                var permissionsReport = new
                {
                    Timestamp = DateTime.UtcNow,
                    OverallStatus = permissionInfo.IsValid ? "Ważne" : "Nieprawidłowe",
                    UserContext = new
                    {
                        IsAuthenticated = userContext.IsAuthenticated,
                        UserPrincipalName = userContext.UserPrincipalName,
                        DisplayName = userContext.DisplayName,
                        TenantId = userContext.TenantId,
                        UserRoles = userContext.Roles?.Count ?? 0
                    },
                    PermissionsSummary = new
                    {
                        TotalAvailablePermissions = permissionInfo.AvailablePermissions?.Count ?? 0,
                        IsValid = permissionInfo.IsValid,
                        ErrorMessage = permissionInfo.ErrorMessage,
                        LastChecked = DateTime.UtcNow
                    },
                    PermissionCategories = new
                    {
                        UserManagement = new
                        {
                            CategoryName = "Zarządzanie użytkownikami",
                            RequiredPermissions = userManagementPermissions,
                            AvailablePermissions = userManagementPermissions.Where(p => permissionInfo.HasPermission(p)).ToArray(),
                            MissingPermissions = userManagementPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray(),
                            HasAllRequired = userManagementPermissions.All(p => permissionInfo.HasPermission(p)),
                            CompletionPercentage = userManagementPermissions.Length > 0 ? 
                                (double)userManagementPermissions.Count(p => permissionInfo.HasPermission(p)) / userManagementPermissions.Length * 100.0 : 0.0
                        },
                        TeamManagement = new
                        {
                            CategoryName = "Zarządzanie zespołami",
                            RequiredPermissions = teamManagementPermissions,
                            AvailablePermissions = teamManagementPermissions.Where(p => permissionInfo.HasPermission(p)).ToArray(),
                            MissingPermissions = teamManagementPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray(),
                            HasAllRequired = teamManagementPermissions.All(p => permissionInfo.HasPermission(p)),
                            CompletionPercentage = teamManagementPermissions.Length > 0 ? 
                                (double)teamManagementPermissions.Count(p => permissionInfo.HasPermission(p)) / teamManagementPermissions.Length * 100.0 : 0.0
                        },
                        MailAccess = new
                        {
                            CategoryName = "Dostęp do poczty",
                            RequiredPermissions = mailPermissions,
                            AvailablePermissions = mailPermissions.Where(p => permissionInfo.HasPermission(p)).ToArray(),
                            MissingPermissions = mailPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray(),
                            HasAllRequired = mailPermissions.All(p => permissionInfo.HasPermission(p)),
                            CompletionPercentage = mailPermissions.Length > 0 ? 
                                (double)mailPermissions.Count(p => permissionInfo.HasPermission(p)) / mailPermissions.Length * 100.0 : 0.0
                        },
                        CalendarAccess = new
                        {
                            CategoryName = "Dostęp do kalendarza",
                            RequiredPermissions = calendarPermissions,
                            AvailablePermissions = calendarPermissions.Where(p => permissionInfo.HasPermission(p)).ToArray(),
                            MissingPermissions = calendarPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray(),
                            HasAllRequired = calendarPermissions.All(p => permissionInfo.HasPermission(p)),
                            CompletionPercentage = calendarPermissions.Length > 0 ? 
                                (double)calendarPermissions.Count(p => permissionInfo.HasPermission(p)) / calendarPermissions.Length * 100.0 : 0.0
                        }
                    },
                    AllAvailablePermissions = permissionInfo.AvailablePermissions?.ToArray() ?? new string[0],
                    Recommendations = GeneratePermissionRecommendations(permissionInfo, userManagementPermissions, teamManagementPermissions, mailPermissions, calendarPermissions)
                };

                _logger.LogInformation("Informacje o uprawnieniach Graph API pobrane. Status: {OverallStatus}, Dostępne uprawnienia: {TotalPermissions}",
                    permissionsReport.OverallStatus, permissionsReport.PermissionsSummary.TotalAvailablePermissions);

                return Ok(permissionsReport);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania informacji o uprawnieniach Graph API");
                return StatusCode(500, new { error = "Błąd podczas pobierania uprawnień Graph API", details = ex.Message });
            }
        }

        /// <summary>
        /// Generuje rekomendacje dotyczące uprawnień Graph API
        /// </summary>
        private List<string> GeneratePermissionRecommendations(GraphPermissionInfo permissionInfo, 
            string[] userPermissions, string[] teamPermissions, string[] mailPermissions, string[] calendarPermissions)
        {
            var recommendations = new List<string>();

            if (!permissionInfo.IsValid)
            {
                recommendations.Add("Sprawdź konfigurację uprawnień aplikacji w Azure AD Portal");
            }

            var missingUserPermissions = userPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
            if (missingUserPermissions.Any())
            {
                recommendations.Add($"Dodaj uprawnienia do zarządzania użytkownikami: {string.Join(", ", missingUserPermissions)}");
            }

            var missingTeamPermissions = teamPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
            if (missingTeamPermissions.Any())
            {
                recommendations.Add($"Dodaj uprawnienia do zarządzania zespołami: {string.Join(", ", missingTeamPermissions)}");
            }

            var missingMailPermissions = mailPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
            if (missingMailPermissions.Any())
            {
                recommendations.Add($"Dodaj uprawnienia do poczty (opcjonalne): {string.Join(", ", missingMailPermissions)}");
            }

            var missingCalendarPermissions = calendarPermissions.Where(p => !permissionInfo.HasPermission(p)).ToArray();
            if (missingCalendarPermissions.Any())
            {
                recommendations.Add($"Dodaj uprawnienia do kalendarza (opcjonalne): {string.Join(", ", missingCalendarPermissions)}");
            }

            if (permissionInfo.AvailablePermissions?.Count == 0)
            {
                recommendations.Add("Brak dostępnych uprawnień - sprawdź konfigurację aplikacji w Azure AD");
            }

            if (recommendations.Count == 0)
            {
                recommendations.Add("Wszystkie wymagane uprawnienia są dostępne");
            }

            return recommendations;
        }
    }

    /// <summary>
    /// Model żądania testu Graph API dla EmbeddedApiServer
    /// </summary>
    public class EmbeddedGraphTestRequest
    {
        /// <summary>
        /// Czy testować uprawnienia
        /// </summary>
        public bool TestPermissions { get; set; } = true;

        /// <summary>
        /// Czy testować endpointy
        /// </summary>
        public bool TestEndpoints { get; set; } = true;

        /// <summary>
        /// Czy testować rate limiting
        /// </summary>
        public bool TestRateLimit { get; set; } = true;

        /// <summary>
        /// Lista endpointów do przetestowania
        /// </summary>
        public string[]? EndpointsToTest { get; set; } = new[] 
        { 
            "/v1.0/me", 
            "/v1.0/users", 
            "/v1.0/groups", 
            "/v1.0/teams" 
        };

        /// <summary>
        /// Timeout dla testów w sekundach
        /// </summary>
        public int TimeoutSeconds { get; set; } = 30;

        /// <summary>
        /// Czy wykonać testy równolegle
        /// </summary>
        public bool RunTestsInParallel { get; set; } = true;
    }
} 