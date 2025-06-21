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
using TeamsManager.Core.Enums;
using TeamsManager.UI.Models.Monitoring;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis do komunikacji z API TeamsManager
    /// Obsługuje wszystkie operacje biznesowe przez EmbeddedApiServer z przepływem OBO
    /// </summary>
    public interface ITeamsManagerApiService
    {
        // ===== METODY DIAGNOSTYCZNE (ISTNIEJĄCE) =====
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

        // ===== METODY BIZNESOWE ZESPOŁÓW =====
        /// <summary>
        /// Tworzy nowy zespół przez API z przepływem OBO
        /// </summary>
        Task<Team?> CreateTeamAsync(string displayName, string description, string ownerUpn, 
            TeamVisibility visibility, string? teamTemplateId = null, string? schoolTypeId = null, 
            string? schoolYearId = null, Dictionary<string, string>? additionalTemplateValues = null);

        /// <summary>
        /// Pobiera zespół po ID przez API z przepływem OBO
        /// </summary>
        Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false);

        /// <summary>
        /// Pobiera wszystkie zespoły przez API z przepływem OBO
        /// </summary>
        Task<IEnumerable<Team>?> GetAllTeamsAsync();

        /// <summary>
        /// Aktualizuje zespół przez API z przepływem OBO
        /// </summary>
        Task<bool> UpdateTeamAsync(string teamId, string displayName, string description, string ownerUpn, 
            TeamVisibility visibility, string? schoolTypeId = null, string? schoolYearId = null);

        /// <summary>
        /// Usuwa zespół przez API z przepływem OBO
        /// </summary>
        Task<bool> DeleteTeamAsync(string teamId);

        /// <summary>
        /// Archiwizuje zespół przez API z przepływem OBO
        /// </summary>
        Task<bool> ArchiveTeamAsync(string teamId, string reason);

        /// <summary>
        /// Dodaje członka do zespołu przez API z przepływem OBO
        /// </summary>
        Task<TeamMember?> AddMemberToTeamAsync(string teamId, string userUpn, TeamMemberRole role);

        /// <summary>
        /// Usuwa członka z zespołu przez API z przepływem OBO
        /// </summary>
        Task<bool> RemoveMemberFromTeamAsync(string teamId, string membershipId);

        // ===== METODY BIZNESOWE UŻYTKOWNIKÓW =====
        /// <summary>
        /// Tworzy nowego użytkownika przez API z przepływem OBO
        /// </summary>
        Task<User?> CreateUserAsync(string firstName, string lastName, string upn, UserRole role, 
            string departmentId, string password, bool sendWelcomeEmail = false, string? phone = null, 
            string? alternateEmail = null, string? externalId = null);

        /// <summary>
        /// Pobiera użytkownika po ID przez API z przepływem OBO
        /// </summary>
        Task<User?> GetUserByIdAsync(string userId);

        /// <summary>
        /// Pobiera wszystkich aktywnych użytkowników przez API z przepływem OBO
        /// </summary>
        Task<IEnumerable<User>?> GetAllActiveUsersAsync();

        /// <summary>
        /// Aktualizuje użytkownika przez API z przepływem OBO
        /// </summary>
        Task<bool> UpdateUserAsync(string userId, string firstName, string lastName, string upn, 
            UserRole role, string departmentId, string? phone = null, string? alternateEmail = null);

        /// <summary>
        /// Usuwa użytkownika przez API z przepływem OBO
        /// </summary>
        Task<bool> DeleteUserAsync(string userId);

        // ===== METODY BIZNESOWE KANAŁÓW =====
        /// <summary>
        /// Tworzy nowy kanał w zespole przez API z przepływem OBO
        /// </summary>
        Task<Channel?> CreateChannelAsync(string teamId, string displayName, string? description = null, bool isPrivate = false);

        /// <summary>
        /// Pobiera kanały zespołu przez API z przepływem OBO
        /// </summary>
        Task<IEnumerable<Channel>?> GetTeamChannelsAsync(string teamId);

        /// <summary>
        /// Aktualizuje kanał przez API z przepływem OBO
        /// </summary>
        Task<bool> UpdateChannelAsync(string teamId, string channelId, string? newDisplayName = null, string? newDescription = null);

        /// <summary>
        /// Usuwa kanał przez API z przepływem OBO
        /// </summary>
        Task<bool> DeleteChannelAsync(string teamId, string channelId);

        // ===== METODY BIZNESOWE DZIAŁÓW =====
        /// <summary>
        /// Pobiera wszystkie działy przez API z przepływem OBO
        /// </summary>
        Task<IEnumerable<Department>?> GetAllDepartmentsAsync();

        /// <summary>
        /// Tworzy nowy dział przez API z przepływem OBO
        /// </summary>
        Task<Department?> CreateDepartmentAsync(string name, string? description = null);

        /// <summary>
        /// Aktualizuje dział przez API z przepływem OBO
        /// </summary>
        Task<bool> UpdateDepartmentAsync(string departmentId, string name, string? description = null);

        /// <summary>
        /// Usuwa dział przez API z przepływem OBO
        /// </summary>
        Task<bool> DeleteDepartmentAsync(string departmentId);
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

        public async Task<Team?> CreateTeamAsync(string displayName, string description, string ownerUpn, 
            TeamVisibility visibility, string? teamTemplateId = null, string? schoolTypeId = null, 
            string? schoolYearId = null, Dictionary<string, string>? additionalTemplateValues = null)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Tworzenie zespołu: {DisplayName}", displayName);
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/v1.0/teams");
                
                var request = new
                {
                    DisplayName = displayName,
                    Description = description,
                    OwnerUpn = ownerUpn,
                    Visibility = visibility,
                    TeamTemplateId = teamTemplateId,
                    SchoolTypeId = schoolTypeId,
                    SchoolYearId = schoolYearId,
                    AdditionalTemplateValues = additionalTemplateValues
                };
                
                var response = await _httpClient.PostAsJsonAsync(url, request);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Team>();
                    _logger.LogDebug("[API-SERVICE] Zespół utworzony pomyślnie: {TeamId}", result?.Id);
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd tworzenia zespołu: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas tworzenia zespołu: {DisplayName}", displayName);
                return null;
            }
        }

        public async Task<Team?> GetTeamByIdAsync(string teamId, bool includeMembers = false, bool includeChannels = false)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie zespołu: {TeamId}", teamId);
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}?includeMembers={includeMembers}&includeChannels={includeChannels}");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<Team>();
                    _logger.LogDebug("[API-SERVICE] Zespół pobrany pomyślnie: {TeamId}", teamId);
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania zespołu: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania zespołu: {TeamId}", teamId);
                return null;
            }
        }

        public async Task<IEnumerable<Team>?> GetAllTeamsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie wszystkich zespołów");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/v1.0/teams");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<IEnumerable<Team>>();
                    _logger.LogDebug("[API-SERVICE] Wszystkie zespoły pobrane pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania wszystkich zespołów: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania wszystkich zespołów");
                return null;
            }
        }

        public async Task<bool> UpdateTeamAsync(string teamId, string displayName, string description, string ownerUpn, 
            TeamVisibility visibility, string? schoolTypeId = null, string? schoolYearId = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}");
                var request = new { DisplayName = displayName, Description = description, OwnerUpn = ownerUpn, Visibility = visibility, SchoolTypeId = schoolTypeId, SchoolYearId = schoolYearId };
                var response = await _httpClient.PatchAsJsonAsync(url, request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd aktualizacji zespołu: {TeamId}", teamId);
                return false;
            }
        }

        public async Task<bool> DeleteTeamAsync(string teamId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}");
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd usuwania zespołu: {TeamId}", teamId);
                return false;
            }
        }

        public async Task<bool> ArchiveTeamAsync(string teamId, string reason)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/archive");
                var request = new { Reason = reason };
                var response = await _httpClient.PostAsJsonAsync(url, request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd archiwizacji zespołu: {TeamId}", teamId);
                return false;
            }
        }

        public async Task<TeamMember?> AddMemberToTeamAsync(string teamId, string userUpn, TeamMemberRole role)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/members");
                var request = new { UserUpn = userUpn, Role = role };
                var response = await _httpClient.PostAsJsonAsync(url, request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<TeamMember>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd dodawania członka do zespołu: {TeamId}", teamId);
                return null;
            }
        }

        public async Task<bool> RemoveMemberFromTeamAsync(string teamId, string membershipId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/members/{membershipId}");
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd usuwania członka z zespołu: {TeamId}", teamId);
                return false;
            }
        }

        public async Task<User?> CreateUserAsync(string firstName, string lastName, string upn, UserRole role, 
            string departmentId, string password, bool sendWelcomeEmail = false, string? phone = null, 
            string? alternateEmail = null, string? externalId = null)
        {
            try
            {
                _logger.LogInformation("=== API-SERVICE: ROZPOCZĘCIE TWORZENIA UŻYTKOWNIKA ===");
                _logger.LogInformation("Parametry: FirstName={FirstName}, LastName={LastName}, UPN={UPN}, Role={Role}, DepartmentId={DepartmentId}", 
                    firstName, lastName, upn, role, departmentId);
                _logger.LogInformation("Dodatkowe: SendWelcomeEmail={SendWelcomeEmail}, Phone={Phone}, AlternateEmail={AlternateEmail}, ExternalId={ExternalId}", 
                    sendWelcomeEmail, phone, alternateEmail, externalId);

                _logger.LogDebug("Wywołanie EnsureAuthenticatedAsync...");
                await EnsureAuthenticatedAsync();
                _logger.LogDebug("✅ Uwierzytelnienie zakończone pomyślnie");

                _logger.LogDebug("Budowanie URL API...");
                var url = await BuildApiUrlAsync("api/v1.0/users");
                _logger.LogInformation("URL API: {ApiUrl}", url);

                var request = new 
                { 
                    FirstName = firstName, 
                    LastName = lastName, 
                    UPN = upn, 
                    Role = role, 
                    DepartmentId = departmentId, 
                    Password = password,
                    SendWelcomeEmail = sendWelcomeEmail,
                    Phone = phone,
                    AlternateEmail = alternateEmail,
                    ExternalId = externalId
                };

                _logger.LogInformation("Obiekt żądania przygotowany: {RequestData}", 
                    System.Text.Json.JsonSerializer.Serialize(new { 
                        FirstName = firstName, 
                        LastName = lastName, 
                        UPN = upn, 
                        Role = role, 
                        DepartmentId = departmentId, 
                        HasPassword = !string.IsNullOrEmpty(password),
                        SendWelcomeEmail = sendWelcomeEmail,
                        Phone = phone,
                        AlternateEmail = alternateEmail,
                        ExternalId = externalId
                    }));

                _logger.LogInformation("Wysyłanie żądania POST do API...");
                var response = await _httpClient.PostAsJsonAsync(url, request);
                
                _logger.LogInformation("Odpowiedź HTTP: StatusCode={StatusCode}, IsSuccess={IsSuccess}", 
                    response.StatusCode, response.IsSuccessStatusCode);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Odczytywanie odpowiedzi JSON...");
                    var result = await response.Content.ReadFromJsonAsync<User>();
                    
                    if (result != null)
                    {
                        _logger.LogInformation("✅ SUKCES: Użytkownik utworzony pomyślnie - ID: {UserId}, UPN: {UPN}", 
                            result.Id, result.UPN);
                        return result;
                    }
                    else
                    {
                        _logger.LogError("❌ BŁĄD: API zwróciło sukces, ale deserialization zwróciła NULL");
                        return null;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ BŁĄD API: StatusCode={StatusCode}, Content={ErrorContent}", 
                        response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 WYJĄTEK w API-SERVICE podczas tworzenia użytkownika: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                return null;
            }
            finally
            {
                _logger.LogInformation("=== API-SERVICE: ZAKOŃCZENIE TWORZENIA UŻYTKOWNIKA ===");
            }
        }

        public async Task<User?> GetUserByIdAsync(string userId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/users/{userId}");
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<User>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd pobierania użytkownika: {UserId}", userId);
                return null;
            }
        }

        public async Task<IEnumerable<User>?> GetAllActiveUsersAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie wszystkich aktywnych użytkowników");
                
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/v1.0/users");
                var response = await _httpClient.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<IEnumerable<User>>();
                    _logger.LogDebug("[API-SERVICE] Wszyscy aktywni użytkownicy pobrani pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania wszystkich aktywnych użytkowników: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania wszystkich aktywnych użytkowników");
                return null;
            }
        }

        public async Task<bool> UpdateUserAsync(string userId, string firstName, string lastName, string upn, 
            UserRole role, string departmentId, string? phone = null, string? alternateEmail = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/users/{userId}");
                var request = new { FirstName = firstName, LastName = lastName, UPN = upn, Role = role, DepartmentId = departmentId, Phone = phone, AlternateEmail = alternateEmail };
                var response = await _httpClient.PatchAsJsonAsync(url, request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd aktualizacji użytkownika: {UserId}", userId);
                return false;
            }
        }

        public async Task<bool> DeleteUserAsync(string userId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/users/{userId}");
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd usuwania użytkownika: {UserId}", userId);
                return false;
            }
        }

        public async Task<Channel?> CreateChannelAsync(string teamId, string displayName, string? description = null, bool isPrivate = false)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/channels");
                var request = new { DisplayName = displayName, Description = description, IsPrivate = isPrivate };
                var response = await _httpClient.PostAsJsonAsync(url, request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Channel>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd tworzenia kanału: {TeamId}", teamId);
                return null;
            }
        }

        public async Task<IEnumerable<Channel>?> GetTeamChannelsAsync(string teamId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/channels");
                var response = await _httpClient.GetAsync(url);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<IEnumerable<Channel>>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd pobierania kanałów zespołu: {TeamId}", teamId);
                return null;
            }
        }

        public async Task<bool> UpdateChannelAsync(string teamId, string channelId, string? newDisplayName = null, string? newDescription = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/channels/{channelId}");
                var request = new { DisplayName = newDisplayName, Description = newDescription };
                var response = await _httpClient.PatchAsJsonAsync(url, request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd aktualizacji kanału: {ChannelId}", channelId);
                return false;
            }
        }

        public async Task<bool> DeleteChannelAsync(string teamId, string channelId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/teams/{teamId}/channels/{channelId}");
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd usuwania kanału: {ChannelId}", channelId);
                return false;
            }
        }

        public async Task<IEnumerable<Department>?> GetAllDepartmentsAsync()
        {
            try
            {
                _logger.LogInformation("=== API-SERVICE: ROZPOCZĘCIE POBIERANIA DZIAŁÓW ===");
                
                _logger.LogDebug("Wywołanie EnsureAuthenticatedAsync...");
                await EnsureAuthenticatedAsync();
                _logger.LogDebug("✅ Uwierzytelnienie zakończone pomyślnie");

                _logger.LogDebug("Budowanie URL API...");
                var url = await BuildApiUrlAsync("api/v1.0/departments");
                _logger.LogInformation("URL API: {ApiUrl}", url);

                _logger.LogInformation("Wysyłanie żądania GET do API...");
                var response = await _httpClient.GetAsync(url);
                
                _logger.LogInformation("Odpowiedź HTTP: StatusCode={StatusCode}, IsSuccess={IsSuccess}", 
                    response.StatusCode, response.IsSuccessStatusCode);

                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Odczytywanie odpowiedzi JSON...");
                    var result = await response.Content.ReadFromJsonAsync<IEnumerable<Department>>();
                    
                    if (result != null)
                    {
                        var departmentsList = result.ToList();
                        _logger.LogInformation("✅ SUKCES: Pobrano {Count} działów z API", departmentsList.Count);
                        
                        foreach (var dept in departmentsList)
                        {
                            _logger.LogDebug("Dział z API: ID={Id}, Name={Name}, IsActive={IsActive}", 
                                dept.Id, dept.Name, dept.IsActive);
                        }
                        
                        return result;
                    }
                    else
                    {
                        _logger.LogError("❌ BŁĄD: API zwróciło sukces, ale deserialization zwróciła NULL");
                        return null;
                    }
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogError("❌ BŁĄD API: StatusCode={StatusCode}, Content={ErrorContent}", 
                        response.StatusCode, errorContent);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 WYJĄTEK w API-SERVICE podczas pobierania działów: {Message}", ex.Message);
                _logger.LogError("Stack trace: {StackTrace}", ex.StackTrace);
                return null;
            }
            finally
            {
                _logger.LogInformation("=== API-SERVICE: ZAKOŃCZENIE POBIERANIA DZIAŁÓW ===");
            }
        }

        public async Task<Department?> CreateDepartmentAsync(string name, string? description = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync("api/v1.0/departments");
                var request = new { Name = name, Description = description };
                var response = await _httpClient.PostAsJsonAsync(url, request);
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<Department>();
                }
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd tworzenia działu: {Name}", name);
                return null;
            }
        }

        public async Task<bool> UpdateDepartmentAsync(string departmentId, string name, string? description = null)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/departments/{departmentId}");
                var request = new { Name = name, Description = description };
                var response = await _httpClient.PatchAsJsonAsync(url, request);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd aktualizacji działu: {DepartmentId}", departmentId);
                return false;
            }
        }

        public async Task<bool> DeleteDepartmentAsync(string departmentId)
        {
            try
            {
                await EnsureAuthenticatedAsync();
                var url = await BuildApiUrlAsync($"api/v1.0/departments/{departmentId}");
                var response = await _httpClient.DeleteAsync(url);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Błąd usuwania działu: {DepartmentId}", departmentId);
                return false;
            }
        }
    }
} 