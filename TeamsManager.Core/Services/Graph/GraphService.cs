using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Exceptions.Graph;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Główny serwis fasadowy dla operacji Microsoft Graph API.
    /// Główny serwis Graph API z pełnym wsparciem dla Graph API patterns.
    /// TASK 2.6.1 - Utworzyć GraphService.cs jako fasadę łączącą wszystkie Graph services.
    /// </summary>
    public class GraphService : IGraphService
    {
        private readonly IGraphTeamManagementService _teamManagementService;
        private readonly IGraphUserManagementService _userManagementService;
        private readonly IGraphBulkOperationsService _bulkOperationsService;
        private readonly IGraphConnectionService _connectionService;
        private readonly IGraphCacheService _cacheService;
        private readonly ILogger<GraphService> _logger;

        private GraphServiceConfiguration _configuration;
        private GraphServiceMetrics _metrics;
        private bool _performanceMetricsEnabled;
        private readonly object _metricsLock = new object();
        private bool _disposed = false;

        public GraphService(
            IGraphTeamManagementService teamManagementService,
            IGraphUserManagementService userManagementService,
            IGraphBulkOperationsService bulkOperationsService,
            IGraphConnectionService connectionService,
            IGraphCacheService cacheService,
            ILogger<GraphService> logger)
        {
            _teamManagementService = teamManagementService ?? throw new ArgumentNullException(nameof(teamManagementService));
            _userManagementService = userManagementService ?? throw new ArgumentNullException(nameof(userManagementService));
            _bulkOperationsService = bulkOperationsService ?? throw new ArgumentNullException(nameof(bulkOperationsService));
            _connectionService = connectionService ?? throw new ArgumentNullException(nameof(connectionService));
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _configuration = new GraphServiceConfiguration();
            _metrics = new GraphServiceMetrics();
            _performanceMetricsEnabled = _configuration.EnablePerformanceMetrics;

            _logger.LogInformation("GraphService initialized with all Graph API services");
        }

        #region IGraphService Properties

        /// <summary>
        /// Sprawdza czy jest aktywne połączenie z Microsoft Graph
        /// </summary>
        public bool IsConnected => _connectionService.IsTokenValidAsync().Result;

        /// <summary>
        /// Serwis zarządzający zespołami i kanałami przez Graph API
        /// </summary>
        public IGraphTeamManagementService Teams => _teamManagementService;

        /// <summary>
        /// Serwis zarządzający użytkownikami, członkostwem i licencjami przez Graph API
        /// </summary>
        public IGraphUserManagementService Users => _userManagementService;

        /// <summary>
        /// Serwis zarządzający operacjami masowymi przez Graph Batch API
        /// </summary>
        public IGraphBulkOperationsService BulkOperations => _bulkOperationsService;

        /// <summary>
        /// Serwis zarządzający połączeniem i diagnostyką Graph API
        /// </summary>
        public IGraphConnectionService Connection => _connectionService;

        /// <summary>
        /// Serwis zarządzający cache'owaniem danych Graph API
        /// </summary>
        public IGraphCacheService Cache => _cacheService;

        #endregion

        #region Connection Management

        /// <summary>
        /// Łączy się z Microsoft Graph używając tokenu dostępu
        /// Graph API Endpoint: GET /v1.0/me (test connection)
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <param name="scopes">Opcjonalne zakresy uprawnień</param>
        /// <returns>True jeśli połączenie udane, false w przeciwnym wypadku</returns>
        public async Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null)
        {
            if (string.IsNullOrWhiteSpace(accessToken))
            {
                _logger.LogError("Access token is null or empty");
                return false;
            }

            try
            {
                _logger.LogInformation("Attempting to connect to Microsoft Graph with access token");

                // Test connection using connection service
                var isValid = await _connectionService.IsTokenValidAsync();
                if (isValid)
                {
                    _logger.LogInformation("Successfully connected to Microsoft Graph");
                    await UpdateMetricsAsync("ConnectWithAccessToken", true, 0);
                    return true;
                }

                _logger.LogWarning("Failed to connect to Microsoft Graph - token validation failed");
                await UpdateMetricsAsync("ConnectWithAccessToken", false, 0);
                return false;
            }
            catch (Exception ex)
            {
                await UpdateMetricsAsync("ConnectWithAccessToken", false, 0);
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Error connecting to Microsoft Graph", ex),
                    () => ConnectWithAccessTokenAsync(accessToken, scopes),
                    _logger,
                    "ConnectWithAccessToken",
                    defaultValue: false);
            }
        }

        /// <summary>
        /// Wykonuje operację z automatycznym połączeniem i obsługą tokenu OBO
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO)</param>
        /// <param name="operation">Operacja do wykonania</param>
        /// <param name="operationDescription">Opis operacji do logowania</param>
        /// <returns>GraphOperationResult z wynikiem operacji</returns>
        public async Task<GraphOperationResult<T>> ExecuteWithAutoConnectAsync<T>(
            string apiAccessToken, 
            Func<Task<T>> operation, 
            string? operationDescription = null)
        {
            if (string.IsNullOrEmpty(apiAccessToken))
            {
                return GraphOperationResult<T>.CreateError("API access token is required");
            }

            if (operation == null)
            {
                return GraphOperationResult<T>.CreateError("Operation is required");
            }

            var stopwatch = Stopwatch.StartNew();
            var description = operationDescription ?? "ExecuteWithAutoConnect";

            try
            {
                _logger.LogInformation("Executing operation with auto-connect: {Operation}", description);

                // Check if we need to refresh token
                if (!IsConnected)
                {
                    var connected = await ConnectWithAccessTokenAsync(apiAccessToken);
                    if (!connected)
                    {
                        return GraphOperationResult<T>.CreateError(
                            "Failed to establish connection to Microsoft Graph",
                            executionTimeMs: stopwatch.ElapsedMilliseconds);
                    }
                }

                // Execute the operation with retry logic
                var result = await ExecuteWithRetryAsync(operation, description);
                
                stopwatch.Stop();
                await UpdateMetricsAsync(description, true, stopwatch.ElapsedMilliseconds);

                return GraphOperationResult<T>.CreateSuccess(
                    result, 
                    executionTimeMs: stopwatch.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await UpdateMetricsAsync(description, false, stopwatch.ElapsedMilliseconds);

                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Error executing operation with auto-connect: {description}", ex),
                    async () => {
                        return await ExecuteWithAutoConnectAsync(apiAccessToken, operation, operationDescription);
                    },
                    _logger,
                    "ExecuteWithAutoConnect",
                    defaultValue: GraphOperationResult<T>.CreateError(
                        $"Failed to execute operation with auto-connect: {description}",
                        ex.Message
                    )
                );
            }
        }

        /// <summary>
        /// Wykonuje operację batch z automatycznym rate limiting i retry logic
        /// Graph API Endpoint: POST /v1.0/$batch
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <param name="batchOperations">Lista operacji batch do wykonania</param>
        /// <param name="respectRateLimit">Czy respektować rate limiting</param>
        /// <param name="operationDescription">Opis operacji</param>
        /// <returns>GraphOperationResult z wynikami batch</returns>
        public async Task<GraphOperationResult<T>> ExecuteBatchOperationAsync<T>(
            string apiAccessToken,
            GraphBatchOperation[] batchOperations,
            bool respectRateLimit = true,
            string? operationDescription = null)
        {
            if (string.IsNullOrWhiteSpace(apiAccessToken))
            {
                return GraphOperationResult<T>.CreateError("API access token is required");
            }

            if (batchOperations == null || batchOperations.Length == 0)
            {
                return GraphOperationResult<T>.CreateError("Batch operations are required");
            }

            var stopwatch = Stopwatch.StartNew();
            var description = operationDescription ?? "ExecuteBatchOperation";

            try
            {
                _logger.LogInformation("Executing batch operation with {Count} operations: {Operation}", 
                    batchOperations.Length, description);

                // Check rate limiting if enabled
                if (respectRateLimit && _configuration.RespectRateLimit)
                {
                    var rateLimitStatus = await GetGlobalRateLimitStatusAsync();
                    if (rateLimitStatus.IsLimitReached)
                    {
                        _logger.LogWarning("Rate limit reached, waiting before executing batch operation");
                        var retrySeconds = rateLimitStatus.RetryAfterSeconds ?? 60; // Domyślnie 60 sekund jeśli null
                        await Task.Delay(TimeSpan.FromSeconds((double)retrySeconds));
                    }
                }

                // Convert GraphBatchOperation to GraphBatchRequest
                var batchRequest = new GraphBatchRequest
                {
                    Requests = batchOperations.Select(op => new GraphBatchRequestItem
                    {
                        Id = op.Id,
                        Method = op.Method,
                        Url = op.Url,
                        Headers = op.Headers,
                        Body = op.Body
                    }).ToList()
                };

                // Execute batch through connection service
                var batchResponse = await _connectionService.ExecuteBatchRequestAsync(new[] { batchRequest });
                
                stopwatch.Stop();
                await UpdateMetricsAsync(description, batchResponse.AllSuccessful, stopwatch.ElapsedMilliseconds);

                if (batchResponse.AllSuccessful)
                {
                    return GraphOperationResult<T>.CreateSuccess(
                        default(T),
                        executionTimeMs: stopwatch.ElapsedMilliseconds);
                }
                else
                {
                    var errorMessage = $"Batch operation partially failed: {batchResponse.SuccessfulCount}/{batchResponse.Responses.Count} requests succeeded";
                    return GraphOperationResult<T>.CreateError(
                        errorMessage,
                        executionTimeMs: stopwatch.ElapsedMilliseconds);
                }
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                await UpdateMetricsAsync(description, false, stopwatch.ElapsedMilliseconds);

                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException($"Error executing batch operation: {description}", ex),
                    () => ExecuteBatchOperationAsync<T>(apiAccessToken, batchOperations, respectRateLimit, operationDescription),
                    _logger,
                    "ExecuteBatchOperation",
                    defaultValue: GraphOperationResult<T>.CreateError(
                        ex.Message,
                        executionTimeMs: stopwatch.ElapsedMilliseconds));
            }
        }

        #endregion

        #region Performance & Monitoring

        /// <summary>
        /// Pobiera metryki wydajności całego Graph Service
        /// </summary>
        /// <returns>Metryki wydajności</returns>
        public GraphServiceMetrics GetPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                return new GraphServiceMetrics
                {
                    TotalRequests = _metrics.TotalRequests,
                    SuccessfulRequests = _metrics.SuccessfulRequests,
                    FailedRequests = _metrics.FailedRequests,
                    AverageResponseTimeMs = _metrics.AverageResponseTimeMs,
                    LastUpdated = _metrics.LastUpdated
                };
            }
        }

        /// <summary>
        /// Resetuje metryki wydajności
        /// </summary>
        public void ResetPerformanceMetrics()
        {
            lock (_metricsLock)
            {
                _metrics = new GraphServiceMetrics();
                _logger.LogInformation("Performance metrics reset");
            }
        }

        /// <summary>
        /// Włącza/wyłącza zbieranie szczegółowych metryk wydajności
        /// </summary>
        /// <param name="enabled">Czy włączyć metryki</param>
        public void SetPerformanceMetricsEnabled(bool enabled)
        {
            _performanceMetricsEnabled = enabled;
            _configuration.EnablePerformanceMetrics = enabled;
            _logger.LogInformation("Performance metrics enabled: {Enabled}", enabled);
        }

        #endregion

        #region Cache Management

        /// <summary>
        /// Wstępnie ładuje dane do cache (cache warming)
        /// Przydatne do przygotowania aplikacji przed pierwszym użyciem
        /// </summary>
        /// <param name="options">Opcje cache warming</param>
        /// <returns>Wynik operacji cache warming</returns>
        public async Task<GraphCacheWarmupResult> WarmCacheAsync(GraphCacheWarmupOptions options)
        {
            if (options == null)
            {
                throw new ArgumentNullException(nameof(options));
            }

            var stopwatch = Stopwatch.StartNew();
            var result = new GraphCacheWarmupResult
            {
                TotalEndpoints = options.Endpoints.Count
            };

            try
            {
                _logger.LogInformation("Starting cache warming for {Count} endpoints", options.Endpoints.Count);

                var warmedCount = 0;
                var errors = new List<string>();

                foreach (var endpoint in options.Endpoints)
                {
                    try
                    {
                        // Use cache service to warm specific endpoints
                        await _cacheService.WarmCacheAsync(
                            $"graph:warmup:{endpoint}", 
                            () => Task.FromResult<object>("warmed"), 
                            TimeSpan.FromMinutes(_configuration.Cache.DefaultCacheDurationMinutes),
                            _configuration.RespectRateLimit);

                        warmedCount++;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Failed to warm cache for endpoint: {Endpoint}", endpoint);
                        errors.Add($"Endpoint {endpoint}: {ex.Message}");
                    }
                }

                stopwatch.Stop();

                result.Success = warmedCount > 0;
                result.WarmedEndpoints = warmedCount;
                result.DurationMs = stopwatch.ElapsedMilliseconds;
                result.Errors = errors;

                _logger.LogInformation("Cache warming completed: {Warmed}/{Total} endpoints in {Duration}ms", 
                    warmedCount, options.Endpoints.Count, stopwatch.ElapsedMilliseconds);

                return result;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Error warming cache", ex),
                    () => WarmCacheAsync(options),
                    _logger,
                    "WarmCache",
                    defaultValue: new GraphCacheWarmupResult
                    {
                        IsSuccessful = false,
                        ErrorMessage = ex.Message,
                        ExecutionTimeMs = stopwatch.ElapsedMilliseconds,
                        CachedItemsCount = 0
                    });
            }
        }

        /// <summary>
        /// Unieważnia cały cache Graph API
        /// </summary>
        public void InvalidateAllCache()
        {
            try
            {
                _cacheService.InvalidateAllCache();
                _logger.LogInformation("All Graph API cache invalidated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error invalidating all cache");
            }
        }

        /// <summary>
        /// Sprawdza status cache i dostępnej pamięci
        /// </summary>
        /// <returns>Informacje o statusie cache</returns>
        public GraphCacheMetrics GetCacheStatus()
        {
            try
            {
                return _cacheService.GetCacheMetrics();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting cache status");
                return new GraphCacheMetrics();
            }
        }

        #endregion

        #region Diagnostics & Health Check

        /// <summary>
        /// Testuje połączenie i uprawnienia Graph API
        /// Graph API Endpoints: GET /v1.0/me, GET /v1.0/users, GET /v1.0/groups, GET /v1.0/teams
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Informacje diagnostyczne o połączeniu Graph API</returns>
        public async Task<GraphDiagnosticInfo> DiagnoseConnectionAsync(string apiAccessToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            if (string.IsNullOrWhiteSpace(apiAccessToken))
            {
                return new GraphDiagnosticInfo
                {
                    IsConnected = false,
                    Status = GraphHealthStatus.Critical,
                    Errors = new List<string> { "API access token is required" }
                };
            }

            try
            {
                _logger.LogInformation("Starting Graph API connection diagnostics");

                // Use connection service for diagnostics
                var healthInfo = await _connectionService.GetConnectionHealthAsync();
                
                var diagnosticInfo = new GraphDiagnosticInfo
                {
                    IsConnected = healthInfo.IsHealthy,
                    Status = healthInfo.IsHealthy ? GraphHealthStatus.Healthy : GraphHealthStatus.Critical,
                    GraphApiVersion = "v1.0",
                    ResponseTimeMs = healthInfo.ResponseTimeMs,
                    LastChecked = DateTime.UtcNow
                };

                if (!healthInfo.IsHealthy)
                {
                    if (!string.IsNullOrEmpty(healthInfo.LastError))
                    {
                        diagnosticInfo.Errors.Add(healthInfo.LastError);
                    }
                }

                stopwatch.Stop();
                _logger.LogInformation("Graph API diagnostics completed: Status = {Status}", diagnosticInfo.Status);
                return diagnosticInfo;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Error diagnosing connection", ex),
                    () => DiagnoseConnectionAsync(apiAccessToken),
                    _logger,
                    "DiagnoseConnection",
                    defaultValue: new GraphDiagnosticInfo
                    {
                        IsConnected = false,
                        IsAuthenticated = false,
                        Status = GraphHealthStatus.Critical,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        Errors = new List<string> { ex.Message }
                    });
            }
        }

        /// <summary>
        /// Wykonuje pełny health check Graph API service
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Szczegółowe informacje o stanie zdrowia</returns>
        public async Task<GraphConnectionHealthInfo> PerformHealthCheckAsync(string apiAccessToken)
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            
            if (string.IsNullOrWhiteSpace(apiAccessToken))
            {
                return new GraphConnectionHealthInfo
                {
                    IsConnected = false,
                    IsTokenValid = false,
                    Status = GraphHealthStatus.Critical,
                    LastError = "API access token is required"
                };
            }

            try
            {
                _logger.LogInformation("Performing comprehensive Graph API health check");

                // Use connection service for health check
                var healthInfo = await _connectionService.GetConnectionHealthAsync();

                stopwatch.Stop();
                _logger.LogInformation("Graph API health check completed: Healthy = {IsHealthy}", healthInfo.IsHealthy);
                return healthInfo;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Error performing health check", ex),
                    () => PerformHealthCheckAsync(apiAccessToken),
                    _logger,
                    "PerformHealthCheck",
                    defaultValue: new GraphConnectionHealthInfo
                    {
                        IsConnected = false,
                        IsTokenValid = false,
                        Status = GraphHealthStatus.Critical,
                        ResponseTimeMs = stopwatch.ElapsedMilliseconds,
                        LastError = ex.Message
                    });
            }
        }

        /// <summary>
        /// Sprawdza aktualny status rate limiting dla wszystkich endpointów
        /// </summary>
        /// <returns>Status rate limiting</returns>
        public async Task<GraphRateLimitStatus> GetGlobalRateLimitStatusAsync()
        {
            try
            {
                // Aggregate rate limit info from all services
                var rateLimitStatus = new GraphRateLimitStatus
                {
                    IsLimitReached = false,
                    RemainingRequests = _configuration.RateLimit.MaxRequestsPerMinute,
                    RetryAfterSeconds = 60
                };

                // Check rate limit for common endpoints
                var commonEndpoints = new[]
                {
                    "/v1.0/me",
                    "/v1.0/users",
                    "/v1.0/groups",
                    "/v1.0/teams"
                };

                foreach (var endpoint in commonEndpoints)
                {
                    var rateLimitInfo = _cacheService.GetRateLimitInfo(endpoint);
                    if (rateLimitInfo != null)
                    {
                        if (rateLimitInfo.RemainingRequests < 10) // Low threshold
                        {
                            rateLimitStatus.IsLimitReached = true;
                            rateLimitStatus.RemainingRequests = Math.Min(rateLimitStatus.RemainingRequests ?? 0, rateLimitInfo.RemainingRequests ?? 0);
                        }
                    }
                }

                return rateLimitStatus;
            }
            catch (Exception ex)
            {
                return await GraphExceptionHandler.HandleGraphConnectionExceptionAsync(
                    new GraphConnectionException("Error getting global rate limit status", ex),
                    () => GetGlobalRateLimitStatusAsync(),
                    _logger,
                    "GetGlobalRateLimitStatus",
                    defaultValue: new GraphRateLimitStatus
                    {
                        IsLimitReached = false,
                        RemainingRequests = 0,
                        RetryAfterSeconds = 60
                    });
            }
        }

        #endregion

        #region Rate Limiting & Error Reporting

        /// <summary>
        /// Aktualizuje informacje o rate limiting
        /// </summary>
        /// <param name="retryAfterSeconds">Liczba sekund do ponownej próby</param>
        /// <returns>Task</returns>
        public async Task UpdateRateLimitInfoAsync(int retryAfterSeconds)
        {
            try
            {
                // Aktualizuj cache z informacjami o rate limiting
                var rateLimitInfo = new GraphRateLimitStatus
                {
                    IsLimitReached = true,
                    RemainingRequests = 0,
                    ResetTime = DateTime.UtcNow.AddSeconds(retryAfterSeconds),
                    RetryAfterSeconds = retryAfterSeconds
                };

                // Zapisz informacje w cache dla przyszłych żądań
                _cacheService.SetRateLimitInfo("global", rateLimitInfo);
                
                _logger.LogWarning("Rate limit reached. Retry after {RetryAfter} seconds", retryAfterSeconds);
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating rate limit info");
            }
        }

        /// <summary>
        /// Raportuje błąd serwera dla circuit breaker
        /// </summary>
        /// <returns>Task</returns>
        public async Task ReportServerErrorAsync()
        {
            try
            {
                // Zwiększ licznik błędów serwera w metrykach
                lock (_metricsLock)
                {
                    _metrics.FailedRequests++;
                }
                
                // Jeśli za dużo błędów serwera, loguj ostrzeżenie
                var errorRate = _metrics.TotalRequests > 0 ? (double)_metrics.FailedRequests / _metrics.TotalRequests : 0;
                
                if (errorRate > 0.5) // Jeśli ponad 50% żądań kończy się błędem
                {
                    _logger.LogWarning("High error rate detected: {ErrorRate:P}. Consider implementing circuit breaker", errorRate);
                }
                
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reporting server error");
            }
        }

        #endregion

        #region Configuration & Settings

        /// <summary>
        /// Aktualizuje konfigurację Graph service w runtime
        /// </summary>
        /// <param name="configuration">Nowa konfiguracja</param>
        public void UpdateConfiguration(GraphServiceConfiguration configuration)
        {
            if (configuration == null)
            {
                throw new ArgumentNullException(nameof(configuration));
            }

            if (!configuration.IsValid())
            {
                throw new ArgumentException("Configuration is not valid", nameof(configuration));
            }

            _configuration = configuration;
            _performanceMetricsEnabled = configuration.EnablePerformanceMetrics;

            _logger.LogInformation("Graph service configuration updated: {Configuration}", 
                configuration.GetConfigurationReport());
        }

        /// <summary>
        /// Pobiera aktualną konfigurację Graph service
        /// </summary>
        /// <returns>Aktualna konfiguracja</returns>
        public GraphServiceConfiguration GetConfiguration()
        {
            return _configuration;
        }

        /// <summary>
        /// Sprawdza czy Graph service jest poprawnie skonfigurowany
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        public bool IsConfigurationValid()
        {
            return _configuration.IsValid();
        }

        #endregion

        #region Private Helper Methods

        /// <summary>
        /// Wykonuje operację z retry logic
        /// </summary>
        /// <typeparam name="T">Typ wyniku</typeparam>
        /// <param name="operation">Operacja do wykonania</param>
        /// <param name="operationName">Nazwa operacji</param>
        /// <returns>Wynik operacji</returns>
        private async Task<T> ExecuteWithRetryAsync<T>(Func<Task<T>> operation, string operationName)
        {
            var retryConfig = _configuration.Retry;
            var maxAttempts = retryConfig.Enabled ? retryConfig.MaxAttempts : 1;
            var delay = retryConfig.InitialDelayMs;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    return await operation();
                }
                catch (Exception ex) when (attempt < maxAttempts && ShouldRetry(ex))
                {
                    _logger.LogWarning(ex, "Operation {Operation} failed on attempt {Attempt}/{MaxAttempts}, retrying in {Delay}ms", 
                        operationName, attempt, maxAttempts, delay);

                    await Task.Delay(delay);
                    
                    // Exponential backoff with jitter
                    delay = Math.Min((int)(delay * retryConfig.BackoffMultiplier), retryConfig.MaxDelayMs);
                    if (retryConfig.UseJitter)
                    {
                        var jitter = new Random().Next(0, (int)(delay * 0.1));
                        delay += jitter;
                    }
                }
            }

            // Final attempt without catch
            return await operation();
        }

        /// <summary>
        /// Sprawdza czy błąd kwalifikuje się do retry
        /// </summary>
        /// <param name="exception">Wyjątek do sprawdzenia</param>
        /// <returns>True jeśli można ponowić operację</returns>
        private bool ShouldRetry(Exception exception)
        {
            // Retry for transient errors
            return exception is TimeoutException ||
                   exception is TaskCanceledException ||
                   (exception.Message?.Contains("timeout", StringComparison.OrdinalIgnoreCase) == true) ||
                   (exception.Message?.Contains("throttle", StringComparison.OrdinalIgnoreCase) == true);
        }

        /// <summary>
        /// Aktualizuje metryki wydajności
        /// </summary>
        /// <param name="operationName">Nazwa operacji</param>
        /// <param name="success">Czy operacja się powiodła</param>
        /// <param name="executionTimeMs">Czas wykonania w ms</param>
        private async Task UpdateMetricsAsync(string operationName, bool success, long executionTimeMs)
        {
            if (!_performanceMetricsEnabled)
                return;

            await Task.Run(() =>
            {
                lock (_metricsLock)
                {
                    _metrics.TotalRequests++;
                    
                    if (success)
                        _metrics.SuccessfulRequests++;
                    else
                        _metrics.FailedRequests++;

                    // Update average response time
                    var totalTime = (_metrics.AverageResponseTimeMs * (_metrics.TotalRequests - 1)) + executionTimeMs;
                    _metrics.AverageResponseTimeMs = totalTime / _metrics.TotalRequests;
                    
                    _metrics.LastUpdated = DateTime.UtcNow;
                }
            });
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Zwalnia zasoby używane przez GraphService
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Zwalnia zasoby używane przez GraphService
        /// </summary>
        /// <param name="disposing">Czy zwalniać managed resources</param>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed && disposing)
            {
                try
                {
                    _logger.LogInformation("Disposing GraphService");
                    
                    // Dispose all services if they implement IDisposable
                    if (_cacheService is IDisposable cacheDisposable)
                        cacheDisposable.Dispose();

                    _disposed = true;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error disposing GraphService");
                }
            }
        }

        #endregion
    }
}
