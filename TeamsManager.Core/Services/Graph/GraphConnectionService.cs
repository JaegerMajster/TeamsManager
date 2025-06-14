using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Abstractions.Services.Graph;
using TeamsManager.Core.Models.Graph;
using TeamsManager.Core.Exceptions.Graph;

namespace TeamsManager.Core.Services.Graph
{
    /// <summary>
    /// Serwis zarządzania połączeniem z Microsoft Graph API.
    /// Implementuje TASK 2.1.1 - utworzenie GraphConnectionService.
    /// </summary>
    public class GraphConnectionService : IGraphConnectionService
    {
        private readonly IModernHttpService _httpService;
        private readonly IConfidentialClientApplication _confidentialClientApp;
        private readonly ILogger<GraphConnectionService> _logger;

        public GraphConnectionService(
            IModernHttpService httpService,
            IConfidentialClientApplication confidentialClientApp,
            ILogger<GraphConnectionService> logger)
        {
            _httpService = httpService ?? throw new ArgumentNullException(nameof(httpService));
            _confidentialClientApp = confidentialClientApp ?? throw new ArgumentNullException(nameof(confidentialClientApp));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// TASK 2.1.2 - Implementacja zarządzania tokenami Graph API
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
                var scopes = new[] { "https://graph.microsoft.com/.default" };

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
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas sprawdzania ważności tokenu Graph API");
                return false;
            }
        }

        /// <summary>
        /// TASK 2.1.2 - Implementacja zarządzania tokenami Graph API
        /// </summary>
        public async Task<bool> RefreshTokenIfNeededAsync()
        {
            try
            {
                _logger.LogDebug("Odświeżanie tokenu Graph API jeśli potrzebne");

                var scopes = new[] { "https://graph.microsoft.com/.default" };

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
                _logger.LogError(ex, "Błąd podczas odświeżania tokenu Graph API");
                return false;
            }
        }

        /// <summary>
        /// TASK 2.1.3 - Implementacja diagnostyki połączenia Graph
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

                // Test podstawowego połączenia
                try
                {
                    var response = await _httpService.GetAsync<object>("/v1.0/me");
                    healthInfo.IsConnected = true;
                    healthInfo.Status = GraphHealthStatus.Healthy;
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
                healthInfo.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
                healthInfo.IsConnected = false;
                healthInfo.IsTokenValid = false;
                healthInfo.LastError = ex.Message;
                healthInfo.Status = GraphHealthStatus.Critical;

                _logger.LogError(ex, "Krytyczny błąd podczas sprawdzania zdrowia połączenia Graph API");
                return healthInfo;
            }
        }

        public async Task<GraphDiagnosticInfo> GetDiagnosticInfoAsync()
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.3 - będzie zaimplementowane");
        }

        public async Task<GraphPermissionInfo> GetPermissionInfoAsync()
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.4 - będzie zaimplementowane");
        }

        public async Task<GraphConnectionTestResult> TestConnectionAsync()
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.5 - będzie zaimplementowane");
        }

        public async Task<GraphApiAvailability> CheckEndpointAvailabilityAsync(string endpoint)
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.5 - będzie zaimplementowane");
        }

        public async Task<GraphUserContext> GetUserContextAsync()
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.2 - będzie zaimplementowane");
        }

        public async Task<GraphRateLimitStatus> GetRateLimitStatusAsync()
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.5 - będzie zaimplementowane");
        }

        public async Task<GraphBatchResponse> ExecuteBatchRequestAsync(IEnumerable<GraphBatchRequest> requests)
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.5 - będzie zaimplementowane");
        }

        public GraphApiError AnalyzeGraphError(Exception exception)
        {
            // Implementacja zostanie dodana w kolejnych taskach
            throw new NotImplementedException("TASK 2.1.3 - będzie zaimplementowane");
        }
    }
} 