using System;
using System.Threading.Tasks;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Abstractions.Services.Graph
{
    /// <summary>
    /// Główny serwis fasadowy dla operacji Microsoft Graph API
    /// Główny interfejs Graph API z pełnym wsparciem dla Graph API patterns
    /// </summary>
    public interface IGraphService : IDisposable
    {
        /// <summary>
        /// Sprawdza czy jest aktywne połączenie z Microsoft Graph
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Łączy się z Microsoft Graph używając tokenu dostępu
        /// Graph API Endpoint: GET /v1.0/me (test connection)
        /// </summary>
        /// <param name="accessToken">Token dostępu do Microsoft Graph</param>
        /// <param name="scopes">Opcjonalne zakresy uprawnień</param>
        /// <returns>True jeśli połączenie udane, false w przeciwnym wypadku</returns>
        Task<bool> ConnectWithAccessTokenAsync(string accessToken, string[]? scopes = null);

        /// <summary>
        /// Wykonuje operację z automatycznym połączeniem i obsługą tokenu OBO
        /// </summary>
        /// <typeparam name="T">Typ wyniku operacji</typeparam>
        /// <param name="apiAccessToken">Token dostępu API (dla przepływu OBO)</param>
        /// <param name="operation">Operacja do wykonania</param>
        /// <param name="operationDescription">Opis operacji do logowania</param>
        /// <returns>GraphOperationResult z wynikiem operacji</returns>
        Task<GraphOperationResult<T>> ExecuteWithAutoConnectAsync<T>(string apiAccessToken, Func<Task<T>> operation, string? operationDescription = null);

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
        Task<GraphOperationResult<T>> ExecuteBatchOperationAsync<T>(
            string apiAccessToken,
            GraphBatchOperation[] batchOperations,
            bool respectRateLimit = true,
            string? operationDescription = null);

        #region Service Properties - All Graph Services

        /// <summary>
        /// Serwis zarządzający zespołami i kanałami przez Graph API
        /// </summary>
        IGraphTeamManagementService Teams { get; }

        /// <summary>
        /// Serwis zarządzający użytkownikami, członkostwem i licencjami przez Graph API
        /// </summary>
        IGraphUserManagementService Users { get; }

        /// <summary>
        /// Serwis zarządzający operacjami masowymi przez Graph Batch API
        /// </summary>
        IGraphBulkOperationsService BulkOperations { get; }

        /// <summary>
        /// Serwis zarządzający połączeniem i diagnostyką Graph API
        /// </summary>
        IGraphConnectionService Connection { get; }

        /// <summary>
        /// Serwis zarządzający cache'owaniem danych Graph API
        /// </summary>
        IGraphCacheService Cache { get; }

        #endregion

        #region Performance & Monitoring

        /// <summary>
        /// Pobiera metryki wydajności całego Graph Service
        /// </summary>
        /// <returns>Metryki wydajności</returns>
        GraphServiceMetrics GetPerformanceMetrics();

        /// <summary>
        /// Resetuje metryki wydajności
        /// </summary>
        void ResetPerformanceMetrics();

        /// <summary>
        /// Włącza/wyłącza zbieranie szczegółowych metryk wydajności
        /// </summary>
        /// <param name="enabled">Czy włączyć metryki</param>
        void SetPerformanceMetricsEnabled(bool enabled);

        #endregion

        #region Cache Management

        /// <summary>
        /// Wstępnie ładuje dane do cache (cache warming)
        /// Przydatne do przygotowania aplikacji przed pierwszym użyciem
        /// </summary>
        /// <param name="options">Opcje cache warming</param>
        /// <returns>Wynik operacji cache warming</returns>
        Task<GraphCacheWarmupResult> WarmCacheAsync(GraphCacheWarmupOptions options);

        /// <summary>
        /// Unieważnia cały cache Graph API
        /// </summary>
        void InvalidateAllCache();

        /// <summary>
        /// Sprawdza status cache i dostępnej pamięci
        /// </summary>
        /// <returns>Informacje o statusie cache</returns>
        GraphCacheMetrics GetCacheStatus();

        #endregion

        #region Diagnostics & Health Check

        /// <summary>
        /// Testuje połączenie i uprawnienia Graph API
        /// Graph API Endpoints: GET /v1.0/me, GET /v1.0/users, GET /v1.0/groups, GET /v1.0/teams
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Informacje diagnostyczne o połączeniu Graph API</returns>
        Task<GraphDiagnosticInfo> DiagnoseConnectionAsync(string apiAccessToken);

        /// <summary>
        /// Wykonuje pełny health check Graph API service
        /// </summary>
        /// <param name="apiAccessToken">Token dostępu API</param>
        /// <returns>Szczegółowe informacje o stanie zdrowia</returns>
        Task<GraphConnectionHealthInfo> PerformHealthCheckAsync(string apiAccessToken);

        /// <summary>
        /// Sprawdza aktualny status rate limiting dla wszystkich endpointów
        /// </summary>
        /// <returns>Status rate limiting</returns>
        Task<GraphRateLimitStatus> GetGlobalRateLimitStatusAsync();

        #endregion

        #region Rate Limiting & Error Reporting

        /// <summary>
        /// Aktualizuje informacje o rate limiting
        /// </summary>
        /// <param name="retryAfterSeconds">Liczba sekund do ponownej próby</param>
        /// <returns>Task</returns>
        Task UpdateRateLimitInfoAsync(int retryAfterSeconds);

        /// <summary>
        /// Raportuje błąd serwera dla circuit breaker
        /// </summary>
        /// <returns>Task</returns>
        Task ReportServerErrorAsync();

        #endregion

        #region Configuration & Settings

        /// <summary>
        /// Aktualizuje konfigurację Graph service w runtime
        /// </summary>
        /// <param name="configuration">Nowa konfiguracja</param>
        void UpdateConfiguration(GraphServiceConfiguration configuration);

        /// <summary>
        /// Pobiera aktualną konfigurację Graph service
        /// </summary>
        /// <returns>Aktualna konfiguracja</returns>
        GraphServiceConfiguration GetConfiguration();

        /// <summary>
        /// Sprawdza czy Graph service jest poprawnie skonfigurowany
        /// </summary>
        /// <returns>True jeśli konfiguracja jest prawidłowa</returns>
        bool IsConfigurationValid();

        #endregion
    }
} 