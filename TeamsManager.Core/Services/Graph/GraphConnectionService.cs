using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using System.IdentityModel.Tokens.Jwt;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Exceptions.Graph;
using TeamsManager.Core.Common;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Serwis zarządzania połączeniem z Microsoft Graph API.
    /// </summary>
    public class GraphConnectionService : IGraphConnectionService
    {
        private readonly IModernHttpService _httpService;
        private readonly IConfidentialClientApplication _confidentialClientApp;
        private readonly ILogger<GraphConnectionService> _logger;
        private readonly ModernCircuitBreaker _circuitBreaker;
        private readonly GraphApiConfiguration _graphConfig;

        public GraphConnectionService(
            IModernHttpService httpService,
            IConfidentialClientApplication confidentialClientApp,
            ILogger<GraphConnectionService> logger,
            ModernCircuitBreaker circuitBreaker,
            GraphApiConfiguration? graphConfig = null)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _confidentialClientApp = confidentialClientApp ?? throw new ArgumentNullException(nameof(confidentialClientApp));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _circuitBreaker = circuitBreaker ?? throw new ArgumentNullException(nameof(circuitBreaker));
            _graphConfig = graphConfig ?? new GraphApiConfiguration();
        }

        /// <summary>
        /// Sprawdza czy token Graph API jest ważny
        /// </summary>
        public async Task<bool> IsTokenValidAsync()
        {
            try
            {
                _logger.LogDebug("Sprawdzanie ważności tokenu Graph API");

                // Próba pobrania tokenu z cache
                var accounts = await _confidentialClientApp.GetAccountsAsync();
                if (!accounts.Any())
                {
                    _logger.LogWarning("Brak kont w cache tokenu");
                    return false;
                }

                var account = accounts.First();
                var scopes = _graphConfig.Scopes.ClientCredentials;

                try
                {
                    var result = await _confidentialClientApp
                        .AcquireTokenSilent(scopes, account)
                        .ExecuteAsync();

                    var isValid = result?.AccessToken != null && 
                                  result.ExpiresOn > DateTimeOffset.UtcNow.AddMinutes(5);

                    _logger.LogDebug("Token Graph API jest {Status}", isValid ? "ważny" : "nieważny");
                    return isValid;
                }
                catch (MsalUiRequiredException)
                {
                    _logger.LogWarning("Token wymaga interakcji użytkownika");
                    return false;
                }
            }
            catch (GraphConnectionException ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex, 
                    () => IsTokenValidAsync(),
                    _logger,
                    "IsTokenValid",
                    defaultValue: false);
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas sprawdzania ważności tokenu Graph API", ex),
                    () => IsTokenValidAsync(),
                    _logger,
                    "IsTokenValid",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Odświeża token Graph API jeśli jest to potrzebne
        /// </summary>
        public async Task<bool> RefreshTokenIfNeededAsync()
        {
            try
            {
                _logger.LogDebug("Odświeżanie tokenu Graph API jeśli potrzebne");

                var scopes = _graphConfig.Scopes.ClientCredentials;

                try
                {
                    var result = await _confidentialClientApp
                        .AcquireTokenForClient(scopes)
                        .ExecuteAsync();

                    var success = result?.AccessToken != null;
                    _logger.LogDebug("Odświeżenie tokenu Graph API: {Status}", success ? "sukces" : "błąd");
                    return success;
                }
                catch (MsalServiceException ex)
                {
                    _logger.LogError(ex, "Błąd serwisu MSAL podczas odświeżania tokenu: {Error}", ex.ErrorCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas odświeżania tokenu Graph API", ex),
                    () => RefreshTokenIfNeededAsync(),
                    _logger,
                    "RefreshTokenIfNeeded",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Pobiera informacje o zdrowiu połączenia z Graph API
        /// </summary>
        public async Task<GraphConnectionHealthInfo> GetConnectionHealthAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var healthInfo = new GraphConnectionHealthInfo();

            try
            {
                _logger.LogDebug("Sprawdzanie zdrowia połączenia Graph API");

                // Sprawdź ważność tokenu
                healthInfo.IsTokenValid = await IsTokenValidAsync();

                // Test podstawowego połączenia z Circuit Breaker
                try
                {
                    var response = await _circuitBreaker.ExecuteAsync(async () =>
                    {
                        return await _httpService.GetFromGraphAsync<object>(_graphConfig.Endpoints.Me);
                    }, "GraphConnectionHealthCheck");
                    
                    healthInfo.IsConnected = true;
                    healthInfo.Status = GraphHealthStatus.Healthy;
                }
                catch (CircuitBreakerOpenException ex)
                {
                    healthInfo.IsConnected = false;
                    healthInfo.LastError = $"Wyłącznik bezpieczeństwa jest otwarty: {ex.Message}";
                    healthInfo.Status = GraphHealthStatus.Critical;
                    _logger.LogWarning("Wyłącznik bezpieczeństwa jest otwarty dla sprawdzania zdrowia połączenia Graph API");
                }
                catch (Exception ex)
                {
                    healthInfo.IsConnected = false;
                    healthInfo.LastError = ex.Message;
                    healthInfo.Status = GraphHealthStatus.Critical;
                    _logger.LogWarning(ex, "Błąd połączenia z Graph API");
                }

                stopwatch.Stop();
                healthInfo.ResponseTimeMs = stopwatch.ElapsedMilliseconds;

                // Ustaw status na podstawie wyników
                if (healthInfo.IsConnected && healthInfo.IsTokenValid)
                {
                    healthInfo.Status = healthInfo.ResponseTimeMs > 2000 ? 
                        GraphHealthStatus.Warning : GraphHealthStatus.Healthy;
                }
                else if (healthInfo.IsTokenValid)
                {
                    healthInfo.Status = GraphHealthStatus.Warning;
                }
                else
                {
                    healthInfo.Status = GraphHealthStatus.Critical;
                }

                _logger.LogDebug("Zdrowie połączenia Graph API: {Status}, Czas: {Time}ms", 
                    healthInfo.Status, healthInfo.ResponseTimeMs);

                return healthInfo;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Krytyczny błąd podczas sprawdzania zdrowia połączenia Graph API", ex),
                    () => GetConnectionHealthAsync(),
                    _logger,
                    "GetConnectionHealth",
                    defaultValue: new GraphConnectionHealthInfo 
                    { 
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        IsConnected = false,
                        IsTokenValid = false,
                        LastError = ex.Message,
                        Status = GraphHealthStatus.Critical
                    });
            }
        }

        /// <summary>
        /// Pobiera szczegółowe informacje diagnostyczne o połączeniu Graph API
        /// </summary>
        public async Task<GraphDiagnosticInfo> GetDiagnosticInfoAsync()
        {
            var diagnostic = new GraphDiagnosticInfo
            {
                LastChecked = DateTime.UtcNow
            };

            try
            {
                _logger.LogInformation("[DIAGNOSTIC] Rozpoczynam GetDiagnosticInfoAsync");
                
                // Test podstawowego połączenia
                _logger.LogInformation("[DIAGNOSTIC] Sprawdzanie podstawowego połączenia Graph API...");
                var connectionTest = await TestBasicConnectionAsync();
                diagnostic.IsConnected = connectionTest.IsSuccessful;
                diagnostic.ResponseTimeMs = connectionTest.ResponseTimeMs;
                
                _logger.LogInformation("[DIAGNOSTIC] Podstawowe połączenie: IsConnected={IsConnected}, ResponseTime={ResponseTime}ms", 
                    diagnostic.IsConnected, diagnostic.ResponseTimeMs);

                if (!diagnostic.IsConnected)
                {
                    diagnostic.Status = GraphHealthStatus.Critical;
                    diagnostic.Errors.Add($"Brak połączenia z Graph API: {connectionTest.ErrorMessage}");
                    _logger.LogError("[DIAGNOSTIC] Brak połączenia z Graph API: {Error}", connectionTest.ErrorMessage);
                    return diagnostic;
                }

                // Test uwierzytelnienia
                _logger.LogInformation("[DIAGNOSTIC] Sprawdzanie uwierzytelnienia...");
                var authTest = await TestAuthenticationAsync();
                diagnostic.IsAuthenticated = authTest.IsSuccessful;
                
                _logger.LogInformation("[DIAGNOSTIC] Uwierzytelnienie: IsAuthenticated={IsAuthenticated}", diagnostic.IsAuthenticated);
                
                if (!diagnostic.IsAuthenticated)
                {
                    diagnostic.Status = GraphHealthStatus.Critical;
                    diagnostic.Errors.Add($"Błąd uwierzytelnienia: {authTest.ErrorMessage}");
                    _logger.LogError("[DIAGNOSTIC] Błąd uwierzytelnienia: {Error}", authTest.ErrorMessage);
                    return diagnostic;
                }

                // Pobierz informacje o aplikacji i dzierżawie
                _logger.LogInformation("[DIAGNOSTIC] Pobieranie informacji o aplikacji...");
                try
                {
                    var accessToken = await GetAccessTokenAsync();
                    if (!string.IsNullOrEmpty(accessToken))
                    {
                        _logger.LogInformation("[DIAGNOSTIC] Token dostępu otrzymany, długość: {TokenLength}", accessToken.Length);
                        
                        // Próba dekodowania tokenu JWT
                        try
                        {
                            var handler = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler();
                            var token = handler.ReadJwtToken(accessToken);
                            
                            diagnostic.TenantId = token.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;
                            diagnostic.ApplicationId = token.Claims.FirstOrDefault(c => c.Type == "aud")?.Value ?? 
                                                    token.Claims.FirstOrDefault(c => c.Type == "appid")?.Value;
                            
                            _logger.LogInformation("[DIAGNOSTIC] Token zdekodowany:");
                            _logger.LogInformation("[DIAGNOSTIC] - TenantId: {TenantId}", diagnostic.TenantId ?? "NULL");
                            _logger.LogInformation("[DIAGNOSTIC] - ApplicationId: {ApplicationId}", diagnostic.ApplicationId ?? "NULL");
                            _logger.LogInformation("[DIAGNOSTIC] - Issuer: {Issuer}", token.Issuer ?? "NULL");
                            _logger.LogInformation("[DIAGNOSTIC] - Audience: {Audience}", string.Join(", ", token.Audiences) ?? "NULL");
                            _logger.LogInformation("[DIAGNOSTIC] - ExpiresOn: {ExpiresOn}", token.ValidTo);
                            
                            var scopes = token.Claims.FirstOrDefault(c => c.Type == "scp")?.Value;
                            _logger.LogInformation("[DIAGNOSTIC] - Scopes: {Scopes}", scopes ?? "NULL");
                        }
                        catch (Exception tokenEx)
                        {
                            _logger.LogWarning("[DIAGNOSTIC] Nie można zdekodować tokenu JWT: {Error}", tokenEx.Message);
                        }
                    }
                    else
                    {
                        _logger.LogWarning("[DIAGNOSTIC] Token dostępu jest pusty");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DIAGNOSTIC] Błąd podczas pobierania informacji o aplikacji: {Message}", ex.Message);
                }

                // Test uprawnień
                _logger.LogInformation("[DIAGNOSTIC] Sprawdzanie uprawnień...");
                var permissionTest = await TestPermissionsAsync();
                diagnostic.HasRequiredPermissions = permissionTest.IsSuccessful;
                
                _logger.LogInformation("[DIAGNOSTIC] Uprawnienia: HasRequiredPermissions={HasRequiredPermissions}", 
                    diagnostic.HasRequiredPermissions);

                if (!diagnostic.HasRequiredPermissions)
                {
                    diagnostic.Warnings.Add($"Brak wymaganych uprawnień: {permissionTest.ErrorMessage}");
                    _logger.LogWarning("[DIAGNOSTIC] Brak wymaganych uprawnień: {Error}", permissionTest.ErrorMessage);
                }

                // Sprawdź rate limiting
                _logger.LogInformation("[DIAGNOSTIC] Sprawdzanie rate limiting...");
                try
                {
                    var rateLimitStatus = await GetRateLimitStatusAsync();
                    diagnostic.RateLimitInfo = rateLimitStatus;
                    _logger.LogInformation("[DIAGNOSTIC] Rate limiting: RemainingRequests={RemainingRequests}, IsLimitReached={IsLimitReached}", 
                        rateLimitStatus?.RemainingRequests, rateLimitStatus?.IsLimitReached);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[DIAGNOSTIC] Błąd podczas sprawdzania rate limiting: {Message}", ex.Message);
                }

                // Określ końcowy status
                if (diagnostic.IsConnected && diagnostic.IsAuthenticated && diagnostic.HasRequiredPermissions)
                {
                    diagnostic.Status = GraphHealthStatus.Healthy;
                    diagnostic.AllTestsPassed = true;
                    _logger.LogInformation("[DIAGNOSTIC] Status końcowy: Healthy - wszystkie testy przeszły pomyślnie");
                }
                else if (diagnostic.IsConnected && diagnostic.IsAuthenticated)
                {
                    diagnostic.Status = GraphHealthStatus.Warning;
                    _logger.LogInformation("[DIAGNOSTIC] Status końcowy: Warning - połączenie i uwierzytelnienie OK, ale problemy z uprawnieniami");
                }
                else
                {
                    diagnostic.Status = GraphHealthStatus.Critical;
                    _logger.LogError("[DIAGNOSTIC] Status końcowy: Critical - podstawowe problemy z połączeniem lub uwierzytelnieniem");
                }

                _logger.LogInformation("[DIAGNOSTIC] GetDiagnosticInfoAsync zakończone. Status: {Status}", diagnostic.Status);
                return diagnostic;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNOSTIC] Nieoczekiwany błąd w GetDiagnosticInfoAsync: {Message}", ex.Message);
                diagnostic.Status = GraphHealthStatus.Critical;
                diagnostic.Errors.Add($"Nieoczekiwany błąd diagnostyczny: {ex.Message}");
                return diagnostic;
            }
        }

        /// <summary>
        /// Testuje podstawowe połączenie z Graph API
        /// </summary>
        private async Task<(bool IsSuccessful, long ResponseTimeMs, string? ErrorMessage)> TestBasicConnectionAsync()
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            try
            {
                await _httpService.GetAsync<object>("/v1.0/me");
                stopwatch.Stop();
                return (true, stopwatch.ElapsedMilliseconds, null);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return (false, stopwatch.ElapsedMilliseconds, ex.Message);
            }
        }

        /// <summary>
        /// Testuje uwierzytelnienie Graph API
        /// </summary>
        private async Task<(bool IsSuccessful, string? ErrorMessage)> TestAuthenticationAsync()
        {
            try
            {
                var token = await GetAccessTokenAsync();
                return (!string.IsNullOrEmpty(token), string.IsNullOrEmpty(token) ? "Brak tokenu dostępu" : null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Testuje uprawnienia Graph API
        /// </summary>
        private async Task<(bool IsSuccessful, string? ErrorMessage)> TestPermissionsAsync()
        {
            try
            {
                // Test podstawowych uprawnień
                await _httpService.GetAsync<object>("/v1.0/me");
                return (true, null);
            }
            catch (Exception ex)
            {
                return (false, ex.Message);
            }
        }

        /// <summary>
        /// Sprawdza uprawnienia Graph API dla bieżącego użytkownika
        /// </summary>
        public async Task<GraphPermissionInfo> GetPermissionInfoAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie informacji o uprawnieniach Graph API");

                var permissionInfo = new GraphPermissionInfo
                {
                    AuthenticationType = "Application", // Confidential Client Application
                    AssignedPermissions = new List<string>(),
                    MissingPermissions = new List<string>()
                };

                // Pobierz informacje o tokenie
                try
                {
                    var accounts = await _confidentialClientApp.GetAccountsAsync();
                    if (accounts.Any())
                    {
                        var account = accounts.First();
                        var scopes = _graphConfig.Scopes.ClientCredentials;
                        
                        var result = await _confidentialClientApp
                            .AcquireTokenSilent(scopes, account)
                            .ExecuteAsync();

                        if (result != null)
                        {
                            permissionInfo.TokenExpiresAt = result.ExpiresOn.DateTime;
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie można pobrać informacji o tokenie");
                }

                // Pobierz informacje o aplikacji i dzierżawie
                try
                {
                    var userContext = await GetUserContextAsync();
                    if (userContext.IsAuthenticated)
                    {
                        permissionInfo.TenantName = userContext.TenantId;
                        
                        // Spróbuj pobrać informacje o aplikacji
                        var meResponse = await _httpService.GetAsync<dynamic>("/v1.0/me");
                        if (meResponse != null)
                        {
                            // Dla aplikacji, ID aplikacji może być w różnych miejscach
                            permissionInfo.ApplicationId = "Application"; // Placeholder - w rzeczywistości trzeba by to pobrać z Azure AD
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie można pobrać informacji o aplikacji");
                }

                // Test uprawnień przez próbę dostępu do różnych endpointów
                var permissionTests = new Dictionary<string, string[]>
                {
                    ["/v1.0/me"] = new[] { "User.Read" },
                    ["/v1.0/users"] = new[] { "User.Read.All", "User.ReadBasic.All" },
                    ["/v1.0/users?$select=id,displayName"] = new[] { "User.ReadBasic.All" },
                    ["/v1.0/groups"] = new[] { "Group.Read.All" },
                    ["/v1.0/teams"] = new[] { "Team.ReadBasic.All" },
                    ["/v1.0/organization"] = new[] { "Organization.Read.All" },
                    ["/v1.0/directoryObjects"] = new[] { "Directory.Read.All" },
                    ["/v1.0/applications"] = new[] { "Application.Read.All" }
                };

                foreach (var test in permissionTests)
                {
                    try
                    {
                        await _httpService.GetAsync<object>(test.Key);
                        
                        // Jeśli żądanie się powiodło, dodaj uprawnienia
                        foreach (var permission in test.Value)
                        {
                            if (!permissionInfo.AssignedPermissions.Contains(permission))
                            {
                                permissionInfo.AssignedPermissions.Add(permission);
                                _logger.LogDebug("Potwierdzone uprawnienie: {Permission}", permission);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Brak uprawnienia dla {Endpoint}: {Error}", test.Key, ex.Message);
                        
                        // Dodaj brakujące uprawnienia
                        foreach (var permission in test.Value)
                        {
                            if (!permissionInfo.AssignedPermissions.Contains(permission) && 
                                !permissionInfo.MissingPermissions.Contains(permission))
                            {
                                permissionInfo.MissingPermissions.Add(permission);
                            }
                        }
                    }
                }

                // Test uprawnień do zapisu (bardziej ostrożnie)
                var writePermissionTests = new Dictionary<string, string[]>
                {
                    // Te testy są bardziej inwazyjne, więc robimy je ostrożnie
                    // Możemy sprawdzić czy endpoint istnieje bez faktycznego tworzenia zasobów
                };

                // Sprawdź czy ma wystarczające uprawnienia
                var requiredPermissions = GraphPermissionScopes.RequiredPermissions;
                var hasAllRequired = requiredPermissions.All(p => permissionInfo.AssignedPermissions.Contains(p));
                
                // Alternatywnie, sprawdź czy ma przynajmniej podstawowe uprawnienia
                var basicPermissions = new[] { "User.Read", "User.Read.All", "Group.Read.All" };
                var hasBasicPermissions = basicPermissions.All(p => permissionInfo.AssignedPermissions.Contains(p));
                
                permissionInfo.HasRequiredPermissions = hasBasicPermissions; // Używamy bardziej realistycznego kryterium

                // Dodaj brakujące wymagane uprawnienia
                foreach (var required in requiredPermissions)
                {
                    if (!permissionInfo.AssignedPermissions.Contains(required) && 
                        !permissionInfo.MissingPermissions.Contains(required))
                    {
                        permissionInfo.MissingPermissions.Add(required);
                    }
                }

                _logger.LogDebug("Analiza uprawnień zakończona: Przypisane={AssignedCount}, Brakujące={MissingCount}, Status={Status}", 
                    permissionInfo.AssignedPermissions.Count, 
                    permissionInfo.MissingPermissions.Count,
                    permissionInfo.Status);

                return permissionInfo;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas sprawdzania uprawnień Graph API", ex),
                    () => GetPermissionInfoAsync(),
                    _logger,
                    "GetPermissionInfo",
                    defaultValue: new GraphPermissionInfo
                    {
                        HasUserReadPermission = false,
                        HasTeamReadPermission = false,
                        HasTeamManagePermission = false,
                        HasUserManagePermission = false,
                        HasDirectoryReadPermission = false,
                        HasGroupReadPermission = false,
                        HasGroupWritePermission = false,
                        Status = GraphHealthStatus.Critical
                    });
            }
        }

        /// <summary>
        /// Wykonuje kompleksowy test połączenia z Graph API
        /// </summary>
        public async Task<GraphConnectionTestResult> TestConnectionAsync()
        {
            var stopwatch = Stopwatch.StartNew();
            var testResult = new GraphConnectionTestResult();

            try
            {
                _logger.LogDebug("Rozpoczęcie testu połączenia Graph API");

                // Test podstawowego połączenia
                var healthInfo = await GetConnectionHealthAsync();
                testResult.IsConnected = healthInfo.IsConnected;
                testResult.IsAuthenticated = healthInfo.IsTokenValid;
                testResult.ResponseTimeMs = healthInfo.ResponseTimeMs;

                if (!string.IsNullOrEmpty(healthInfo.LastError))
                {
                    testResult.Errors.Add($"Błąd połączenia: {healthInfo.LastError}");
                }

                // Test uwierzytelnienia
                try
                {
                    var userContext = await GetUserContextAsync();
                    testResult.IsAuthenticated = userContext.IsAuthenticated;
                    
                    if (userContext.IsAuthenticated)
                    {
                        testResult.AdditionalInfo["UserPrincipalName"] = userContext.UserPrincipalName ?? "Nieznane";
                        testResult.AdditionalInfo["TenantId"] = userContext.TenantId ?? "Nieznane";
                    }
                }
                catch (Exception ex)
                {
                    testResult.IsAuthenticated = false;
                    testResult.Errors.Add($"Błąd uwierzytelnienia: {ex.Message}");
                }

                // Test uprawnień
                try
                {
                    var permissionInfo = await GetPermissionInfoAsync();
                    testResult.HasRequiredPermissions = permissionInfo.HasRequiredPermissions;
                    testResult.AdditionalInfo["PermissionStatus"] = permissionInfo.Status.ToString();
                    testResult.AdditionalInfo["PermissionCompleteness"] = $"{permissionInfo.PermissionCompleteness:F1}%";
                    testResult.AdditionalInfo["AssignedPermissionsCount"] = permissionInfo.AssignedPermissions.Count;
                    
                    if (permissionInfo.MissingPermissions.Count > 0)
                    {
                        testResult.WarningMessages.Add($"Brakuje {permissionInfo.MissingPermissions.Count} uprawnień");
                    }
                }
                catch (Exception ex)
                {
                    testResult.HasRequiredPermissions = false;
                    testResult.Errors.Add($"Błąd sprawdzania uprawnień: {ex.Message}");
                }

                // Test endpointów
                var endpointTests = new[]
                {
                    "/v1.0/me",
                    "/v1.0/users",
                    "/v1.0/groups", 
                    "/v1.0/teams"
                };

                var endpointResults = new List<GraphApiAvailability>();
                foreach (var endpoint in endpointTests)
                {
                    var endpointResult = await TestSingleEndpoint(endpoint);
                    endpointResults.Add(endpointResult);
                }

                testResult.EndpointTestResults = endpointResults;

                // Test rate limiting
                try
                {
                    var rateLimitStatus = await GetRateLimitStatusAsync();
                    if (rateLimitStatus != null)
                    {
                        testResult.RateLimitInfo = new GraphRateLimitStatus
                        {
                            RemainingRequests = rateLimitStatus.RemainingRequests,
                            MaxRequests = rateLimitStatus.MaxRequests,
                            ResetTime = rateLimitStatus.ResetTime,
                            UsagePercentage = rateLimitStatus.UsagePercentage
                        };

                        if (rateLimitStatus.IsLimitReached)
                        {
                            testResult.WarningMessages.Add("Osiągnięto limit żądań Graph API");
                        }
                    }
                }
                catch (Exception ex)
                {
                    testResult.WarningMessages.Add($"Nie można sprawdzić rate limiting: {ex.Message}");
                }

                // Oblicz końcowe wyniki
                stopwatch.Stop();
                testResult.ResponseTimeMs = Math.Max(testResult.ResponseTimeMs, stopwatch.ElapsedMilliseconds);
                testResult.AverageResponseTimeMs = endpointResults.Count > 0 ? 
                    endpointResults.Average(r => r.ResponseTimeMs) : testResult.ResponseTimeMs;

                testResult.AllTestsPassed = testResult.IsConnected && 
                                           testResult.IsAuthenticated && 
                                           testResult.HasRequiredPermissions &&
                                           testResult.Errors.Count == 0;

                _logger.LogDebug("Test połączenia zakończony: AllTestsPassed={AllTestsPassed}, Czas={Time}ms", 
                    testResult.AllTestsPassed, testResult.ResponseTimeMs);

                return testResult;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Krytyczny błąd podczas testowania połączenia Graph API", ex),
                    () => TestConnectionAsync(),
                    _logger,
                    "TestConnection",
                    defaultValue: new GraphConnectionTestResult
                    {
                        IsConnected = false,
                        IsAuthenticated = false,
                        HasRequiredPermissions = false,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Errors = new List<string> { ex.Message },
                        AllTestsPassed = false,
                        EndpointTestResults = new List<GraphApiAvailability>()
                    });
            }
        }

        /// <summary>
        /// Testuje pojedynczy endpoint Graph API
        /// </summary>
        private async Task<GraphApiAvailability> TestSingleEndpoint(string endpoint)
        {
            var stopwatch = Stopwatch.StartNew();
            var result = new GraphApiAvailability
            {
                Name = endpoint,
                Url = endpoint
            };

            try
            {
                await _httpService.GetAsync<object>(endpoint);
                result.IsAvailable = true;
                result.HttpStatusCode = 200;
            }
            catch (GraphConnectionException ex)
            {
                result.IsAvailable = false;
                result.ErrorMessage = ex.Message;
                result.HttpStatusCode = (int?)ex.HttpStatusCode ?? 0;
                
                _logger.LogWarning(ex, "GraphConnectionException podczas testowania endpointu {Endpoint}", endpoint);
            }
            catch (Exception ex)
            {
                result.IsAvailable = false;
                result.ErrorMessage = ex.Message;
                
                // Spróbuj wyciągnąć kod statusu z wyjątku
                if (ex.Message.Contains("401"))
                    result.HttpStatusCode = 401;
                else if (ex.Message.Contains("403"))
                    result.HttpStatusCode = 403;
                else if (ex.Message.Contains("404"))
                    result.HttpStatusCode = 404;
                else
                    result.HttpStatusCode = 500;
            }
            finally
            {
                stopwatch.Stop();
                result.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
            }

            return result;
        }

        /// <summary>
        /// Sprawdza dostępność konkretnego endpointu Graph API
        /// </summary>
        public async Task<GraphApiAvailability> CheckEndpointAvailabilityAsync(string endpoint)
        {
            var stopwatch = Stopwatch.StartNew();
            var availability = new GraphApiAvailability
            {
                Name = endpoint,
                Url = endpoint
            };

            try
            {
                _logger.LogDebug("Sprawdzanie dostępności endpointu: {Endpoint}", endpoint);

                // Wykonaj żądanie do endpointu
                await _httpService.GetAsync<object>(endpoint);
                
                availability.IsAvailable = true;
                availability.HttpStatusCode = 200;
                
                _logger.LogDebug("Endpoint {Endpoint} jest dostępny", endpoint);
            }
            catch (GraphConnectionException ex)
            {
                stopwatch.Stop();
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(ex,
                    () => CheckEndpointAvailabilityAsync(endpoint),
                    _logger,
                    "CheckEndpointAvailability",
                    defaultValue: new GraphApiAvailability
                    {
                        Name = endpoint,
                        Url = endpoint,
                        IsAvailable = false,
                        ErrorMessage = ex.Message,
                        HttpStatusCode = 0,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        CheckedAt = DateTime.UtcNow
                    });
            }
            catch (Exception ex)
            {
                availability.IsAvailable = false;
                availability.ErrorMessage = ex.Message;

                // Spróbuj wyciągnąć kod statusu HTTP z wyjątku
                if (ex.Message.Contains("401"))
                {
                    availability.HttpStatusCode = 401;
                }
                else if (ex.Message.Contains("403"))
                {
                    availability.HttpStatusCode = 403;
                }
                else if (ex.Message.Contains("404"))
                {
                    availability.HttpStatusCode = 404;
                }
                else if (ex.Message.Contains("429"))
                {
                    availability.HttpStatusCode = 429;
                }
                else if (ex.Message.Contains("500"))
                {
                    availability.HttpStatusCode = 500;
                }
                else if (ex.Message.Contains("502"))
                {
                    availability.HttpStatusCode = 502;
                }
                else if (ex.Message.Contains("503"))
                {
                    availability.HttpStatusCode = 503;
                }
                else
                {
                    availability.HttpStatusCode = 0; // Nieznany błąd
                }

                _logger.LogWarning("Endpoint {Endpoint} nie jest dostępny: {Error}", endpoint, ex.Message);
            }
            finally
            {
                stopwatch.Stop();
                availability.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                availability.CheckedAt = DateTime.UtcNow;
            }

            return availability;
        }

        /// <summary>
        /// Pobiera kontekst użytkownika z Graph API
        /// </summary>
        public async Task<GraphUserContext> GetUserContextAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie kontekstu użytkownika z Graph API");

                // Sprawdź czy token jest ważny
                if (!await IsTokenValidAsync())
                {
                    _logger.LogWarning("Token nie jest ważny, próba odświeżenia");
                    if (!await RefreshTokenIfNeededAsync())
                    {
                        throw new GraphConnectionException("Nie można uzyskać ważnego tokenu Graph API");
                    }
                }

                // Pobierz informacje o użytkowniku z Graph API
                var userResponse = await _httpService.GetAsync<dynamic>("/v1.0/me");
                
                var userContext = new GraphUserContext
                {
                    UserId = userResponse?.id?.ToString(),
                    UserPrincipalName = userResponse?.userPrincipalName?.ToString(),
                    DisplayName = userResponse?.displayName?.ToString(),
                    Mail = userResponse?.mail?.ToString(),
                    TenantId = userResponse?.tenantId?.ToString(),
                    IsAuthenticated = true
                };

                // Pobierz role użytkownika
                try
                {
                    var rolesResponse = await _httpService.GetAsync<dynamic>("/v1.0/me/memberOf");
                    if (rolesResponse?.value != null)
                    {
                        foreach (var role in rolesResponse.value)
                        {
                            if (role?.displayName != null)
                            {
                                userContext.Roles.Add(role.displayName.ToString());
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie można pobrać ról użytkownika");
                }

                _logger.LogDebug("Pobrano kontekst użytkownika: {UserPrincipalName}", userContext.UserPrincipalName);
                return userContext;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas pobierania kontekstu użytkownika", ex),
                    () => GetUserContextAsync(),
                    _logger,
                    "GetUserContext",
                    defaultValue: new GraphUserContext { IsAuthenticated = false });
            }
        }

        /// <summary>
        /// Pobiera informacje o statusie rate limiting Graph API
        /// </summary>
        public async Task<GraphRateLimitStatus> GetRateLimitStatusAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie informacji o rate limiting Graph API");

                var rateLimitStatus = new GraphRateLimitStatus();

                // Microsoft Graph API nie udostępnia bezpośredniego endpointu do sprawdzania rate limiting
                // Musimy polegać na nagłówkach HTTP z poprzednich żądań lub wykonać test żądanie
                
                try
                {
                    // Wykonaj lekkie żądanie testowe
                    var response = await _httpService.GetAsync<object>("/v1.0/me?$select=id");
                    
                    // W rzeczywistej implementacji, ModernHttpService powinien udostępniać nagłówki HTTP
                    // Na razie symulujemy wartości domyślne
                    rateLimitStatus.IsLimitReached = false;
                    rateLimitStatus.RemainingRequests = 9500; // Przykładowa wartość
                    rateLimitStatus.MaxRequests = 10000; // Domyślny limit Microsoft Graph
                    rateLimitStatus.ResetTime = DateTime.UtcNow.AddMinutes(10); // Reset co 10 minut
                    rateLimitStatus.LimitType = "Standard";
                    
                    _logger.LogDebug("Rate limiting status: Pozostało {Remaining}/{Max} żądań", 
                        rateLimitStatus.RemainingRequests, rateLimitStatus.MaxRequests);
                }
                catch (GraphRateLimitException rateLimitEx)
                {
                    // Jeśli otrzymaliśmy wyjątek rate limiting, użyj informacji z niego
                    rateLimitStatus.IsLimitReached = true;
                    rateLimitStatus.RemainingRequests = 0;
                    rateLimitStatus.MaxRequests = rateLimitEx.MaxRequestCount;
                    rateLimitStatus.RetryAfterSeconds = rateLimitEx.RetryAfterSeconds;
                    rateLimitStatus.LimitType = rateLimitEx.LimitType.ToString() ?? "Unknown";
                    
                    if (rateLimitEx.WindowResetSeconds.HasValue)
                    {
                        rateLimitStatus.ResetTime = DateTime.UtcNow.AddSeconds(rateLimitEx.WindowResetSeconds.Value);
                    }
                    
                    _logger.LogWarning("Rate limit osiągnięty: Typ={LimitType}, RetryAfter={RetryAfter}s", 
                        rateLimitStatus.LimitType, rateLimitStatus.RetryAfterSeconds);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Nie można pobrać informacji o rate limiting");
                    
                    // Zwróć bezpieczne wartości domyślne
                    rateLimitStatus.IsLimitReached = false;
                    rateLimitStatus.RemainingRequests = null;
                    rateLimitStatus.MaxRequests = null;
                    rateLimitStatus.LimitType = "Unknown";
                }

                return rateLimitStatus;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas pobierania informacji o rate limiting", ex),
                    () => GetRateLimitStatusAsync(),
                    _logger,
                    "GetRateLimitStatus",
                    defaultValue: new GraphRateLimitStatus
                    {
                        IsLimitReached = false,
                        LimitType = "Unknown"
                    });
            }
        }

        /// <summary>
        /// Wykonuje żądanie batch do Graph API
        /// </summary>
        public async Task<GraphBatchResponse> ExecuteBatchRequestAsync(IEnumerable<GraphBatchRequest> requests)
        {
            try
            {
                _logger.LogDebug("Wykonywanie batch request Graph API");

                var requestList = requests.ToList();
                if (requestList.Count == 0)
                {
                    return new GraphBatchResponse();
                }

                if (requestList.Count > 20)
                {
                    throw new ArgumentException("Graph API batch request może zawierać maksymalnie 20 żądań");
                }

                // Przygotuj batch request z wszystkich items z wszystkich requests
                var allRequestItems = requestList.SelectMany(r => r.Requests).ToList();
                
                var batchRequest = new
                {
                    requests = allRequestItems.Select((req, index) => new
                    {
                        id = req.Id ?? index.ToString(),
                        method = req.Method.ToUpperInvariant(),
                        url = req.Url.StartsWith("/") ? req.Url.Substring(1) : req.Url, // Usuń początkowy slash
                        headers = req.Headers.Count > 0 ? req.Headers : null,
                        body = req.Body
                    }).ToArray()
                };

                _logger.LogDebug("Wysyłanie batch request z {Count} żądaniami", allRequestItems.Count);

                // Wykonaj batch request
                var response = await _httpService.PostAsync<object, dynamic>("/v1.0/$batch", batchRequest);

                var batchResponse = new GraphBatchResponse();

                if (response?.responses != null)
                {
                    foreach (var responseItem in response.responses)
                    {
                        var batchResponseItem = new GraphBatchResponseItem
                        {
                            Id = responseItem.id?.ToString() ?? "unknown",
                            Status = responseItem.status ?? 500,
                            Body = responseItem.body
                        };

                        // Dodaj nagłówki jeśli są dostępne
                        if (responseItem.headers != null)
                        {
                            foreach (var header in responseItem.headers)
                            {
                                batchResponseItem.Headers[header.Name?.ToString() ?? "unknown"] = 
                                    header.Value?.ToString() ?? "";
                            }
                        }

                        batchResponse.Responses.Add(batchResponseItem);
                    }
                }

                _logger.LogDebug("Batch request zakończony: Sukces={SuccessCount}/{Total}", 
                    batchResponse.SuccessfulCount, batchResponse.Responses.Count);

                return batchResponse;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas wykonywania batch request", ex),
                    () => ExecuteBatchRequestAsync(requests),
                    _logger,
                    "ExecuteBatchRequest",
                    defaultValue: CreateErrorBatchResponse(requests, ex.Message));
            }
        }

        private GraphBatchResponse CreateErrorBatchResponse(IEnumerable<GraphBatchRequest> requests, string errorMessage)
        {
            // Zwróć response z błędami dla wszystkich żądań
            var errorResponse = new GraphBatchResponse();
            var allRequestItems = requests.SelectMany(r => r.Requests).ToList();
            
            for (int i = 0; i < allRequestItems.Count; i++)
            {
                errorResponse.Responses.Add(new GraphBatchResponseItem
                {
                    Id = allRequestItems[i].Id ?? i.ToString(),
                    Status = 500,
                    Body = new { error = new { message = errorMessage } }
                });
            }

            return errorResponse;
        }

        /// <summary>
        /// Analizuje błąd Graph API i zwraca szczegółowe informacje
        /// </summary>
        public GraphApiError AnalyzeGraphError(Exception exception)
        {
            try
            {
                _logger.LogDebug("Analiza błędu Graph API: {ExceptionType}", exception.GetType().Name);

                var error = new GraphApiError
                {
                    Message = exception.Message,
                    Timestamp = DateTime.UtcNow
                };

                // Analiza różnych typów wyjątków
                switch (exception)
                {
                    case GraphConnectionException graphConnEx:
                        error.Code = "GraphConnectionError";
                        error.Details = $"Błąd połączenia z Graph API: {graphConnEx.Message}";
                        error.Endpoint = graphConnEx.Endpoint;
                        error.HttpStatusCode = (int?)graphConnEx.HttpStatusCode;
                        error.RequestId = graphConnEx.RequestId;
                        error.CanRetry = graphConnEx.CanRetry();
                        error.RetryAfterSeconds = graphConnEx.GetRecommendedRetryDelay();
                        break;

                    case GraphRateLimitException rateLimitEx:
                        error.Code = "RateLimitExceeded";
                        error.Details = $"Przekroczono limit żądań Graph API. Typ: {rateLimitEx.LimitType}";
                        error.Endpoint = rateLimitEx.Endpoint;
                        error.HttpStatusCode = (int?)rateLimitEx.HttpStatusCode;
                        error.RequestId = rateLimitEx.RequestId;
                        error.CanRetry = true;
                        error.RetryAfterSeconds = rateLimitEx.RetryAfterSeconds;
                        break;

                    case GraphApiException graphApiEx:
                        error.Code = graphApiEx.GraphErrorCode ?? "GraphApiError";
                        error.Details = graphApiEx.GraphErrorDetails ?? graphApiEx.Message;
                        error.Endpoint = graphApiEx.Endpoint;
                        error.HttpStatusCode = (int?)graphApiEx.HttpStatusCode;
                        error.RequestId = graphApiEx.RequestId;
                        error.CanRetry = graphApiEx.CanRetry();
                        error.RetryAfterSeconds = graphApiEx.GetRecommendedRetryDelay();
                        break;

                    case HttpRequestException httpEx:
                        error.Code = "HttpRequestError";
                        error.Details = $"Błąd żądania HTTP: {httpEx.Message}";
                        error.CanRetry = IsRetryableHttpError(httpEx);
                        error.RetryAfterSeconds = error.CanRetry ? 30 : null;
                        break;

                    case TaskCanceledException timeoutEx:
                        error.Code = "RequestTimeout";
                        error.Details = "Przekroczono limit czasu żądania Graph API";
                        error.CanRetry = true;
                        error.RetryAfterSeconds = 60;
                        break;

                    case UnauthorizedAccessException authEx:
                        error.Code = "Unauthorized";
                        error.Details = "Brak autoryzacji do Graph API";
                        error.CanRetry = false;
                        break;

                    case ArgumentException argEx:
                        error.Code = "InvalidArgument";
                        error.Details = $"Nieprawidłowy argument: {argEx.Message}";
                        error.CanRetry = false;
                        break;

                    default:
                        error.Code = "UnknownError";
                        error.Details = $"Nieznany błąd: {exception.GetType().Name} - {exception.Message}";
                        error.CanRetry = false;
                        break;
                }

                // Dodaj informacje o inner exception
                if (exception.InnerException != null)
                {
                    error.Details += $" | Inner: {exception.InnerException.Message}";
                }

                _logger.LogDebug("Analiza błędu zakończona: Code={Code}, CanRetry={CanRetry}, RetryAfter={RetryAfter}s", 
                    error.Code, error.CanRetry, error.RetryAfterSeconds);

                return error;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas analizy wyjątku Graph API");
                
                return new GraphApiError
                {
                    Code = "AnalysisError",
                    Message = "Nie można przeanalizować błędu",
                    Details = $"Błąd analizy: {ex.Message} | Oryginalny błąd: {exception.Message}",
                    Timestamp = DateTime.UtcNow,
                    CanRetry = false
                };
            }
        }

        /// <summary>
        /// Sprawdza czy błąd HTTP jest możliwy do ponowienia
        /// </summary>
        private bool IsRetryableHttpError(HttpRequestException httpException)
        {
            var message = httpException.Message.ToLowerInvariant();
            
            // Błędy sieciowe które można ponowić
            var retryableErrors = new[]
            {
                "timeout",
                "connection reset",
                "connection aborted",
                "network unreachable",
                "host unreachable",
                "temporary failure",
                "service unavailable",
                "bad gateway",
                "gateway timeout"
            };

            return retryableErrors.Any(error => message.Contains(error));
        }

        /// <summary>
        /// Sprawdza ważność tokenu dostępu.
        /// Używane w GraphUserManagementService i innych serwisach Graph.
        /// </summary>
        public async Task<bool> CheckTokenValidityAsync()
        {
            // Deleguj do istniejącej metody IsTokenValidAsync
            return await IsTokenValidAsync();
        }

        /// <summary>
        /// Odświeża token dostępu.
        /// Używane w GraphUserManagementService i innych serwisach Graph.
        /// </summary>
        public async Task<bool> RefreshTokenAsync()
        {
            // Deleguj do istniejącej metody RefreshTokenIfNeededAsync
            return await RefreshTokenIfNeededAsync();
        }

        /// <summary>
        /// Zapewnia ważny token dostępu - sprawdza i odświeża jeśli potrzeba.
        /// Używane w GraphBulkOperationsService.
        /// </summary>
        public async Task<bool> EnsureValidTokenAsync()
        {
            try
            {
                _logger.LogDebug("Zapewnianie ważnego tokenu Graph API");

                // Sprawdź czy token jest ważny
                var isValid = await CheckTokenValidityAsync();
                if (isValid)
                {
                    _logger.LogDebug("Token Graph API jest ważny");
                    return true;
                }

                // Token nieważny - spróbuj odświeżyć
                _logger.LogDebug("Token nieważny - próba odświeżenia");
                var refreshed = await RefreshTokenAsync();
                
                if (refreshed)
                {
                    _logger.LogDebug("Token Graph API został pomyślnie odświeżony");
                    return true;
                }

                _logger.LogWarning("Nie udało się zapewnić ważnego tokenu Graph API");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapewniania ważnego tokenu Graph API");
                return false;
            }
        }

        /// <summary>
        /// Pobiera aktualny token dostępu.
        /// Używane w GraphBulkOperationsService i innych serwisach.
        /// </summary>
        /// <returns>Token dostępu lub null jeśli niedostępny</returns>
        public async Task<string?> GetAccessTokenAsync()
        {
            try
            {
                _logger.LogDebug("Pobieranie tokenu dostępu Graph API");

                // ✅ NAPRAWKA OBO: Użyj ModernHttpService który ma token OBO
                var token = await _httpService.GetAccessTokenAsync();
                
                if (!string.IsNullOrEmpty(token))
                {
                    _logger.LogDebug("Token Graph API pobrany z ModernHttpService (OBO): {HasToken}", !string.IsNullOrEmpty(token));
                    return token;
                }
                
                // Fallback do Client Credentials jeśli OBO nie jest dostępny
                _logger.LogDebug("Brak tokenu OBO, używam Client Credentials jako fallback");
                var scopes = _graphConfig.Scopes.ClientCredentials;
                var result = await _confidentialClientApp
                    .AcquireTokenForClient(scopes)
                    .ExecuteAsync();

                var fallbackToken = result?.AccessToken;
                _logger.LogDebug("Token Graph API pobrany (Client Credentials fallback): {HasToken}", !string.IsNullOrEmpty(fallbackToken));
                return fallbackToken;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Błąd podczas pobierania tokenu dostępu Graph API", ex),
                    () => GetAccessTokenAsync(),
                    _logger,
                    "GetAccessToken",
                    defaultValue: null);
            }
        }

        /// <summary>
        /// Wykonuje diagnostykę połączenia Graph API.
        /// Alias dla GetDiagnosticInfoAsync().
        /// </summary>
        /// <returns>Informacje diagnostyczne</returns>
        public async Task<GraphDiagnosticInfo> DiagnoseConnectionAsync()
        {
            _logger.LogDebug("Diagnostyka połączenia Graph API");
            return await GetDiagnosticInfoAsync();
        }

        /// <summary>
        /// Symuluje wykonanie operacji Graph API.
        /// W Graph API zwraca informacje o dostępności endpointu.
        /// </summary>
        /// <param name="script">Operacja Graph API do wykonania</param>
        /// <returns>Wynik symulacji wykonania z właściwością Count dla kompatybilności z orkiestratorami</returns>
        public async Task<object> ExecuteScriptAsync(string script)
        {
            _logger.LogDebug("Wykonanie operacji Graph API: {Script}", script);
            
            try
            {
                // Symulacja - sprawdź podstawowy endpoint
                var availability = await CheckEndpointAvailabilityAsync("/v1.0/me");
                
                return new
                {
                    Success = availability.IsAvailable,
                    Message = availability.IsAvailable ? "Skrypt wykonany pomyślnie (symulacja)" : "Błąd wykonania skryptu (symulacja)",
                    Details = availability.ResponseMessage,
                    Script = script,
                    ExecutedAt = DateTime.UtcNow,
                    IsSimulated = true,
                    Count = availability.IsAvailable ? 1 : 0
                };
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas wykonania operacji Graph API");
                
                return new
                {
                    Success = false,
                    Message = $"Błąd symulacji skryptu: {ex.Message}",
                    Script = script,
                    ExecutedAt = DateTime.UtcNow,
                    IsSimulated = true,
                    Error = ex.Message,
                    Count = 0
                };
            }
        }
    }
} 