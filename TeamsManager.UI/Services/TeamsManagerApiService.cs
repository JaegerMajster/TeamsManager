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

        public TeamsManagerApiService(
            HttpClient httpClient,
            IMsalAuthService authService,
            ILogger<TeamsManagerApiService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<GraphDiagnosticInfo?> GetGraphConnectionDiagnosticsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie diagnostyki połączenia Graph API");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/graph/status");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphDiagnosticInfo>();
                    _logger.LogDebug("[API-SERVICE] Diagnostyka połączenia Graph API pobrana pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania diagnostyki Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania diagnostyki połączenia Graph API");
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
                
                var response = await _httpClient.PostAsJsonAsync("api/diagnostics/graph/test", requestBody);
                
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
                var response = await _httpClient.PostAsJsonAsync("api/diagnostics/graph/permissions", requiredPermissions);
                
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/status");
                
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
                
                var response = await _httpClient.PostAsJsonAsync("api/diagnostics/graph/test", requestBody);
                
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/status");
                
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/status");
                
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
                
                var response = await _httpClient.PostAsJsonAsync("api/diagnostics/graph/test", requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphConnectionTestResult>();
                    _logger.LogDebug("[API-SERVICE] Test połączenia Graph API zakończony pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd testu połączenia Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas testu połączenia Graph API");
                return null;
            }
        }

        public async Task<GraphPermissionInfo?> GetGraphPermissionsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie uprawnień Microsoft Graph API");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/graph/permissions");
                
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

        private async Task EnsureAuthenticatedAsync()
        {
            try
            {
                var token = await _authService.GetAccessTokenAsync();
                if (!string.IsNullOrEmpty(token))
                {
                    _httpClient.DefaultRequestHeaders.Authorization = 
                        new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Nie udało się uzyskać tokenu dostępu");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd podczas uwierzytelniania");
            }
        }

        public async Task<GraphRateLimitStatus?> GetGraphRateLimitStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu rate limiting Graph API");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/graph/rate-limit");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphRateLimitStatus>();
                    _logger.LogDebug("[API-SERVICE] Status rate limiting Graph API pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania statusu rate limiting Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania statusu rate limiting Graph API");
                return null;
            }
        }

        public async Task<GraphHealthStatus?> GetGraphHealthStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu zdrowia Graph API");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/graph/health");
                
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
                _logger.LogDebug("[API-SERVICE] Wykonywanie batch operacji Graph API z {RequestCount} requestami", batchRequest.Requests?.Count ?? 0);
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.PostAsJsonAsync("api/graph/batch", batchRequest);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphBatchOperationResult>();
                    _logger.LogDebug("[API-SERVICE] Batch operacja Graph API wykonana pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd wykonywania batch operacji Graph API: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas wykonywania batch operacji Graph API");
                return null;
            }
        }

        public async Task<GraphMetricsInfo?> GetGraphMetricsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie metryk Graph API");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/graph/metrics");
                
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/cache");
                
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/token");
                
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
                var response = await _httpClient.PostAsync("api/diagnostics/graph/token/refresh", null);
                
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/endpoints");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<GraphApiAvailability[]>();
                    _logger.LogDebug("[API-SERVICE] Dostępne endpointy Graph API pobrane pomyślnie ({Count} endpointów)", result?.Length ?? 0);
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
                var response = await _httpClient.GetAsync("api/diagnostics/graph/quota");
                
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