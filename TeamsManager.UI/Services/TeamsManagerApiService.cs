using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models;
using TeamsManager.Core.Models.Graph;
using TeamsManager.UI.Models.Monitoring;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis do komunikacji z API TeamsManager
    /// Obsługuje endpointy diagnostyczne i monitorowania Graph API
    /// Implementuje operacje Graph API
    /// </summary>
    public interface ITeamsManagerApiService
    {

        Task<GraphDiagnosticInfo?> GetGraphConnectionDiagnosticsAsync();
        Task<GraphDiagnosticInfo?> GetExtendedGraphConnectionDiagnosticsAsync(string[]? testEndpoints = null, bool includePermissions = true);
        Task<GraphPermissionInfo?> ValidateGraphPermissionsAsync(string[] requiredPermissions);
        Task<GraphConnectionHealthInfo?> GetGraphConnectionHealthAsync();
        Task<GraphDiagnosticInfo?> TestGraphOperationAsync(string operationType, Dictionary<string, object>? parameters = null);
        Task<object?> GetFullGraphDiagnosticReportAsync();
        

        Task<GraphConnectionHealthInfo?> GetGraphStatusAsync();
        Task<GraphConnectionTestResult?> TestGraphConnectionAsync();
        Task<GraphPermissionInfo?> GetGraphPermissionsAsync();
        
        Task<GraphRateLimitStatus?> GetGraphRateLimitStatusAsync();
        Task<GraphHealthStatus?> GetGraphHealthStatusAsync();
        Task<GraphBatchOperationResult?> ExecuteGraphBatchOperationAsync(GraphBatchRequest batchRequest);
        Task<GraphMetricsInfo?> GetGraphMetricsAsync();
        Task<GraphCacheInfo?> GetGraphCacheStatusAsync();
        Task<bool> ClearGraphCacheAsync();
        Task<GraphTokenInfo?> GetGraphTokenInfoAsync();
        Task<bool> RefreshGraphTokenAsync();
        Task<GraphApiAvailability[]?> GetAvailableGraphEndpointsAsync();
        Task<GraphRateLimitStatus?> GetGraphQuotaInfoAsync();
    }

    public class TeamsManagerApiService : ITeamsManagerApiService
    {
        private readonly HttpClient _httpClient;
        private readonly IMsalAuthService _authService;
        private readonly ILogger<TeamsManagerApiService> _logger;
        private readonly EmbeddedApiServer _embeddedApiServer;

        public TeamsManagerApiService(
            HttpClient httpClient,
            IMsalAuthService authService,
            ILogger<TeamsManagerApiService> logger,
            EmbeddedApiServer embeddedApiServer)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _embeddedApiServer = embeddedApiServer ?? throw new ArgumentNullException(nameof(embeddedApiServer));
        }

        public async Task<GraphDiagnosticInfo?> GetGraphConnectionDiagnosticsAsync()
        {
            try
            {
                _logger.LogInformation("[DIAGNOSTIC] Rozpoczynam pobieranie diagnostyki połączenia Graph API");
                _logger.LogInformation("[DIAGNOSTIC] HttpClient BaseAddress: {BaseAddress}", _httpClient.BaseAddress);
                
                await EnsureAuthenticatedAsync();
                
                var url = await BuildApiUrlAsync("api/diagnostics/graph/status");
                _logger.LogInformation("[DIAGNOSTIC] Wysyłam żądanie GET do: {Url}", url);
                var response = await _httpClient.GetAsync(url);
                
                _logger.LogInformation("[DIAGNOSTIC] Otrzymano odpowiedź: StatusCode={StatusCode}, ReasonPhrase={ReasonPhrase}", 
                    response.StatusCode, response.ReasonPhrase);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    _logger.LogInformation("[DIAGNOSTIC] Zawartość odpowiedzi (pierwsze 500 znaków): {Content}", 
                        responseContent.Length > 500 ? responseContent.Substring(0, 500) + "..." : responseContent);
                    
                    var result = await response.Content.ReadFromJsonAsync<GraphDiagnosticInfo>();
                    _logger.LogInformation("[DIAGNOSTIC] Diagnostyka Graph API pobrana pomyślnie. IsConnected={IsConnected}, Status={Status}", 
                        result?.IsConnected, result?.Status);
                    return result;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("[DIAGNOSTIC] Błąd pobierania diagnostyki Graph API: {StatusCode}, Content: {ErrorContent}", 
                        response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNOSTIC] Wyjątek podczas pobierania diagnostyki połączenia Graph API. Message: {Message}, StackTrace: {StackTrace}", 
                    ex.Message, ex.StackTrace);
                return null;
            }
        }

        public async Task<GraphDiagnosticInfo?> GetExtendedGraphConnectionDiagnosticsAsync(string[]? testEndpoints = null, bool includePermissions = true)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie rozszerzonej diagnostyki połączenia Graph API");
                
                await EnsureAuthenticatedAsync();
                
                var requestBody = new
                {
                    TestPermissions = includePermissions,
                    TestEndpoints = includePermissions,
                    TestRateLimit = true,
                    EndpointsToTest = testEndpoints ?? new[] { "/v1.0/me", "/v1.0/users?$top=1", "/v1.0/groups?$top=1" },
                    TimeoutSeconds = 30,
                    RunTestsInParallel = true
                };
                
                var url = await BuildApiUrlAsync("api/diagnostics/graph/test");
                var response = await _httpClient.PostAsJsonAsync(url, requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphDiagnosticInfo>();
                    _logger.LogDebug("[API-SERVICE] Rozszerzona diagnostyka Graph API pobrana pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania rozszerzonej diagnostyki Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania rozszerzonej diagnostyki Graph API");
                return null;
            }
        }

        public async Task<GraphPermissionInfo?> ValidateGraphPermissionsAsync(string[] requiredPermissions)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Sprawdzanie uprawnień Graph API: {Permissions}", string.Join(", ", requiredPermissions));
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/permissions");
                var response = await _httpClient.PostAsJsonAsync(url, requiredPermissions);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphPermissionInfo>();
                    _logger.LogDebug("[API-SERVICE] Uprawnienia Graph API sprawdzone pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd sprawdzania uprawnień Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas sprawdzania uprawnień Graph API");
                return null;
            }
        }

        public async Task<GraphConnectionHealthInfo?> GetGraphConnectionHealthAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie stanu połączenia Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/status");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphConnectionHealthInfo>();
                    _logger.LogDebug("[API-SERVICE] Stan połączenia Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania stanu połączenia Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania stanu połączenia Graph API");
                return null;
            }
        }

        public async Task<GraphDiagnosticInfo?> TestGraphOperationAsync(string operationType, Dictionary<string, object>? parameters = null)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Testowanie operacji Graph API: {OperationType}", operationType);
                
                await EnsureAuthenticatedAsync();
                
                var requestBody = new
                {
                    TestPermissions = true,
                    TestEndpoints = true,
                    TestRateLimit = true,
                    EndpointsToTest = new[] { $"/v1.0/{operationType}" },
                    TimeoutSeconds = 30,
                    RunTestsInParallel = false
                };
                
                var url = await BuildApiUrlAsync("api/diagnostics/graph/test");
                var response = await _httpClient.PostAsJsonAsync(url, requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphDiagnosticInfo>();
                    _logger.LogDebug("[API-SERVICE] Test operacji Graph API zakończony pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd testowania operacji Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas testowania operacji Graph API");
                return null;
            }
        }

        public async Task<object?> GetFullGraphDiagnosticReportAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie pełnego raportu diagnostycznego Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/status");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    _logger.LogDebug("[API-SERVICE] Pełny raport diagnostyczny Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania pełnego raportu Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania pełnego raportu Graph API");
                return null;
            }
        }

        public async Task<GraphConnectionHealthInfo?> GetGraphStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu połączenia Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/status");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphConnectionHealthInfo>();
                    _logger.LogDebug("[API-SERVICE] Status połączenia Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania statusu połączenia Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania statusu połączenia Graph API");
                return null;
            }
        }

        public async Task<GraphConnectionTestResult?> TestGraphConnectionAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Testowanie połączenia Microsoft Graph API");
                
                await EnsureAuthenticatedAsync();
                
                var requestBody = new
                {
                    TestPermissions = true,
                    TestEndpoints = true,
                    TestRateLimit = true,
                    EndpointsToTest = new[] { "/v1.0/me", "/v1.0/users?$top=1" },
                    TimeoutSeconds = 30,
                    RunTestsInParallel = true
                };
                
                var url = await BuildApiUrlAsync("api/diagnostics/graph/test");
                var response = await _httpClient.PostAsJsonAsync(url, requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphConnectionTestResult>();
                    _logger.LogDebug("[API-SERVICE] Test połączenia Graph API zakończony pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd testowania połączenia Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas testowania połączenia Graph API");
                return null;
            }
        }

        public async Task<GraphPermissionInfo?> GetGraphPermissionsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie uprawnień Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/permissions");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphPermissionInfo>();
                    _logger.LogDebug("[API-SERVICE] Uprawnienia Graph API pobrane pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania uprawnień Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania uprawnień Graph API");
                return null;
            }
        }

        private async Task<string> BuildApiUrlAsync(string endpoint)
        {
            try
            {
                // Upewnij się, że EmbeddedApiServer jest uruchomiony
                _logger.LogDebug("[DIAGNOSTIC] Sprawdzanie stanu EmbeddedApiServer: IsRunning={IsRunning}", _embeddedApiServer.IsRunning);
                
                if (!_embeddedApiServer.IsRunning)
                {
                    _logger.LogInformation("[DIAGNOSTIC] EmbeddedApiServer nie działa - uruchamianie...");
                    var startResult = await _embeddedApiServer.StartAsync();
                    _logger.LogInformation("[DIAGNOSTIC] EmbeddedApiServer uruchomiony: {Success} na: {BaseUrl}", startResult, _embeddedApiServer.BaseUrl);
                }
                else
                {
                    _logger.LogDebug("[DIAGNOSTIC] EmbeddedApiServer już działa na: {BaseUrl}", _embeddedApiServer.BaseUrl);
                }
                
                var baseUrl = _embeddedApiServer.BaseUrl;
                _logger.LogDebug("[DIAGNOSTIC] Używam EmbeddedApiServer URL: {BaseUrl}", baseUrl);
                
                var fullUrl = $"{baseUrl.TrimEnd('/')}/{endpoint.TrimStart('/')}";
                _logger.LogDebug("[DIAGNOSTIC] Zbudowano URL: {FullUrl}", fullUrl);
                return fullUrl;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[DIAGNOSTIC] Błąd podczas budowania URL API z EmbeddedApiServer");
                // Fallback do stałego URL w przypadku problemów
                var fallbackUrl = $"https://localhost:7037/{endpoint.TrimStart('/')}";
                _logger.LogWarning("[DIAGNOSTIC] Używam fallback URL: {FallbackUrl}", fallbackUrl);
                return fallbackUrl;
            }
        }

        private async Task EnsureAuthenticatedAsync()
        {
            _logger.LogDebug("[UI-DIAGNOSTIC] ==================== ROZPOCZĘCIE EnsureAuthenticatedAsync ====================");
            
            try
            {
                _logger.LogDebug("[UI-DIAGNOSTIC] Sprawdzanie czy HttpClient ma już Authorization header...");
                
                // Sprawdź czy już mamy header
                var existingAuth = _httpClient.DefaultRequestHeaders.Authorization;
                if (existingAuth != null)
                {
                    _logger.LogDebug("[UI-DIAGNOSTIC] HttpClient ma już Authorization header: {Scheme} {Parameter}", 
                        existingAuth.Scheme, 
                        existingAuth.Parameter?.Length > 20 ? existingAuth.Parameter.Substring(0, 20) + "..." : existingAuth.Parameter);
                    return;
                }
                
                _logger.LogDebug("[UI-DIAGNOSTIC] Brak Authorization header w HttpClient. Pobieranie tokenu...");
                
                _logger.LogDebug("[UI-DIAGNOSTIC] Wywołanie _authService.AcquireApiTokenSilentAsync()...");
                var authResult = await _authService.AcquireApiTokenSilentAsync();
                
                if (authResult == null || string.IsNullOrEmpty(authResult.AccessToken))
                {
                    _logger.LogWarning("[UI-DIAGNOSTIC] ⚠️ AcquireApiTokenSilentAsync() zwróciło NULL lub pusty token!");
                    _logger.LogWarning("[UI-DIAGNOSTIC] AuthResult is null: {IsNull}", authResult == null);
                    if (authResult != null)
                    {
                        _logger.LogWarning("[UI-DIAGNOSTIC] AccessToken is null or empty: {IsEmpty}", string.IsNullOrEmpty(authResult.AccessToken));
                    }
                    
                    _logger.LogError("[UI-DIAGNOSTIC] ❌ Nie można pobrać tokenu API!");
                    return;
                }
                
                var token = authResult.AccessToken;
                _logger.LogInformation("[UI-DIAGNOSTIC] ✅ Token API otrzymany pomyślnie! Długość: {TokenLength}", token.Length);
                _logger.LogInformation("[UI-DIAGNOSTIC] Token Account: {Account}", authResult.Account?.Username ?? "N/A");
                _logger.LogInformation("[UI-DIAGNOSTIC] Token Scopes: {Scopes}", string.Join(", ", authResult.Scopes ?? new string[0]));
                _logger.LogInformation("[UI-DIAGNOSTIC] Token ExpiresOn: {ExpiresOn}", authResult.ExpiresOn);
                _logger.LogDebug("[UI-DIAGNOSTIC] Token (pierwsze 30 znaków): {TokenStart}...", 
                    token.Length > 30 ? token.Substring(0, 30) : token);
                
                _logger.LogDebug("[UI-DIAGNOSTIC] Ustawianie Authorization header w HttpClient...");
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                
                _logger.LogInformation("[UI-DIAGNOSTIC] ✅ Authorization header ustawiony w HttpClient!");
                
                // Weryfikacja czy header został ustawiony
                var verifyAuth = _httpClient.DefaultRequestHeaders.Authorization;
                if (verifyAuth != null && verifyAuth.Scheme == "Bearer" && !string.IsNullOrEmpty(verifyAuth.Parameter))
                {
                    _logger.LogInformation("[UI-DIAGNOSTIC] ✅ WERYFIKACJA: Authorization header poprawnie ustawiony");
                }
                else
                {
                    _logger.LogError("[UI-DIAGNOSTIC] ❌ WERYFIKACJA NIEUDANA: Authorization header nie został ustawiony!");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[UI-DIAGNOSTIC] ❌ BŁĄD w EnsureAuthenticatedAsync: {Message}", ex.Message);
                _logger.LogError("[UI-DIAGNOSTIC] StackTrace: {StackTrace}", ex.StackTrace);
                throw;
            }
            finally
            {
                _logger.LogDebug("[UI-DIAGNOSTIC] ==================== KONIEC EnsureAuthenticatedAsync ====================");
            }
        }

        public async Task<GraphRateLimitStatus?> GetGraphRateLimitStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu rate limit Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/rate-limit");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphRateLimitStatus>();
                    _logger.LogDebug("[API-SERVICE] Status rate limit Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania statusu rate limit Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania statusu rate limit Graph API");
                return null;
            }
        }

        public async Task<GraphHealthStatus?> GetGraphHealthStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu zdrowia Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/health");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphHealthStatus>();
                    _logger.LogDebug("[API-SERVICE] Status zdrowia Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania statusu zdrowia Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania statusu zdrowia Graph API");
                return null;
            }
        }

        public async Task<GraphBatchOperationResult?> ExecuteGraphBatchOperationAsync(GraphBatchRequest batchRequest)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Wykonywanie operacji batch Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/graph/batch");
                var response = await _httpClient.PostAsJsonAsync(url, batchRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphBatchOperationResult>();
                    _logger.LogDebug("[API-SERVICE] Operacja batch Graph API wykonana pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd wykonywania operacji batch Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas wykonywania operacji batch Graph API");
                return null;
            }
        }

        public async Task<GraphMetricsInfo?> GetGraphMetricsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie metryk Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/metrics");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphMetricsInfo>();
                    _logger.LogDebug("[API-SERVICE] Metryki Graph API pobrane pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania metryk Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania metryk Graph API");
                return null;
            }
        }

        public async Task<GraphCacheInfo?> GetGraphCacheStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu cache Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/cache");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphCacheInfo>();
                    _logger.LogDebug("[API-SERVICE] Status cache Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania statusu cache Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania statusu cache Graph API");
                return null;
            }
        }

        public async Task<bool> ClearGraphCacheAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Czyszczenie cache Graph API");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.DeleteAsync("api/diagnostics/graph/cache");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("[API-SERVICE] Cache Graph API wyczyszczony pomyślnie");
                    return true;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd czyszczenia cache Graph API: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas czyszczenia cache Graph API");
                return false;
            }
        }

        public async Task<GraphTokenInfo?> GetGraphTokenInfoAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie informacji o tokenie Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/token");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphTokenInfo>();
                    _logger.LogDebug("[API-SERVICE] Informacje o tokenie Graph API pobrane pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania informacji o tokenie Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania informacji o tokenie Graph API");
                return null;
            }
        }

        public async Task<bool> RefreshGraphTokenAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Odświeżanie tokenu Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/token/refresh");
                var response = await _httpClient.PostAsync(url, null);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("[API-SERVICE] Token Graph API odświeżony pomyślnie");
                    return true;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd odświeżania tokenu Graph API: {StatusCode}", response.StatusCode);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas odświeżania tokenu Graph API");
                return false;
            }
        }

        public async Task<GraphApiAvailability[]?> GetAvailableGraphEndpointsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie dostępnych endpointów Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/endpoints");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphApiAvailability[]>();
                    _logger.LogDebug("[API-SERVICE] Dostępne endpointy Graph API pobrane pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania dostępnych endpointów Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania dostępnych endpointów Graph API");
                return null;
            }
        }

        public async Task<GraphRateLimitStatus?> GetGraphQuotaInfoAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie informacji o quota Graph API");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/diagnostics/graph/quota");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphRateLimitStatus>();
                    _logger.LogDebug("[API-SERVICE] Informacje o quota Graph API pobrane pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania informacji o quota Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania informacji o quota Graph API");
                return null;
            }
        }
    }
} 