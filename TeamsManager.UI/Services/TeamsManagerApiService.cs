using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using TeamsManager.Core.Abstractions.Services.PowerShell;
using TeamsManager.UI.Services.Abstractions;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis do komunikacji z API TeamsManager
    /// Obsługuje endpointy diagnostyczne i monitorowania
    /// </summary>
    public interface ITeamsManagerApiService
    {
        Task<PowerShellDiagnosticInfo?> GetConnectionDiagnosticsAsync();
        Task<PowerShellDiagnosticInfo?> GetExtendedConnectionDiagnosticsAsync(string[]? testCommands = null, bool includePermissions = true);
        Task<PowerShellPermissionInfo?> ValidatePermissionsAsync(string[] requiredPermissions);
        Task<ConnectionHealthInfo?> GetConnectionHealthAsync();
        Task<PowerShellDiagnosticInfo?> TestOperationAsync(string operationType, Dictionary<string, object>? parameters = null);
        Task<object?> GetFullDiagnosticReportAsync();
        
        // Nowe metody zarządzania modułami
        Task<PowerShellModuleStatus?> GetModuleStatusAsync();
        Task<PowerShellModuleInstallationResult?> InstallModulesAsync(bool forceReinstall = false);
        Task<PowerShellConnectionTestResult?> TestConnectionAsync();
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

        public async Task<PowerShellDiagnosticInfo?> GetConnectionDiagnosticsAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie diagnostyki połączenia");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/connection");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellDiagnosticInfo>();
                    _logger.LogDebug("[API-SERVICE] Diagnostyka połączenia pobrana pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania diagnostyki: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania diagnostyki połączenia");
                return null;
            }
        }

        public async Task<PowerShellDiagnosticInfo?> GetExtendedConnectionDiagnosticsAsync(string[]? testCommands = null, bool includePermissions = true)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie rozszerzonej diagnostyki połączenia");
                
                await EnsureAuthenticatedAsync();
                
                var requestBody = testCommands ?? new[] { "Get-MgUser -Top 1", "Get-MgGroup -Top 1" };
                var response = await _httpClient.PostAsJsonAsync($"api/diagnostics/connection/extended?includePermissions={includePermissions}", requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellDiagnosticInfo>();
                    _logger.LogDebug("[API-SERVICE] Rozszerzona diagnostyka pobrana pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania rozszerzonej diagnostyki: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania rozszerzonej diagnostyki");
                return null;
            }
        }

        public async Task<PowerShellPermissionInfo?> ValidatePermissionsAsync(string[] requiredPermissions)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Sprawdzanie uprawnień: {Permissions}", string.Join(", ", requiredPermissions));
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.PostAsJsonAsync("api/diagnostics/permissions", requiredPermissions);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellPermissionInfo>();
                    _logger.LogDebug("[API-SERVICE] Uprawnienia sprawdzone pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd sprawdzania uprawnień: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas sprawdzania uprawnień");
                return null;
            }
        }

        public async Task<ConnectionHealthInfo?> GetConnectionHealthAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie stanu połączenia");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/health");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<ConnectionHealthInfo>();
                    _logger.LogDebug("[API-SERVICE] Stan połączenia pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania stanu połączenia: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania stanu połączenia");
                return null;
            }
        }

        public async Task<PowerShellDiagnosticInfo?> TestOperationAsync(string operationType, Dictionary<string, object>? parameters = null)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Testowanie operacji: {OperationType}", operationType);
                
                await EnsureAuthenticatedAsync();
                
                var requestBody = new
                {
                    OperationType = operationType,
                    Parameters = parameters ?? new Dictionary<string, object>()
                };
                
                var response = await _httpClient.PostAsJsonAsync("api/diagnostics/test-operation", requestBody);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellDiagnosticInfo>();
                    _logger.LogDebug("[API-SERVICE] Test operacji zakończony pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd testowania operacji: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas testowania operacji");
                return null;
            }
        }

        public async Task<object?> GetFullDiagnosticReportAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie pełnego raportu diagnostycznego");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/full-report");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<object>();
                    _logger.LogDebug("[API-SERVICE] Pełny raport diagnostyczny pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania pełnego raportu: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania pełnego raportu");
                return null;
            }
        }

        public async Task<PowerShellModuleStatus?> GetModuleStatusAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Pobieranie statusu modułów PowerShell");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.GetAsync("api/diagnostics/modules/status");
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellModuleStatus>();
                    _logger.LogDebug("[API-SERVICE] Status modułów pobrany pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd pobierania statusu modułów: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas pobierania statusu modułów");
                return null;
            }
        }

        public async Task<PowerShellModuleInstallationResult?> InstallModulesAsync(bool forceReinstall = false)
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Instalowanie modułów PowerShell (Force: {ForceReinstall})", forceReinstall);
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.PostAsync($"api/diagnostics/modules/install?forceReinstall={forceReinstall}", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellModuleInstallationResult>();
                    _logger.LogDebug("[API-SERVICE] Instalacja modułów zakończona pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd instalacji modułów: {StatusCode}", response.StatusCode);
                    // Spróbuj odczytać błąd z odpowiedzi
                    var errorResult = await response.Content.ReadFromJsonAsync<PowerShellModuleInstallationResult>();
                    return errorResult;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas instalacji modułów");
                return null;
            }
        }

        public async Task<PowerShellConnectionTestResult?> TestConnectionAsync()
        {
            try
            {
                _logger.LogDebug("[API-SERVICE] Testowanie połączenia Microsoft Graph");
                
                await EnsureAuthenticatedAsync();
                var response = await _httpClient.PostAsync("api/diagnostics/connection/test", null);
                
                if (response.IsSuccessStatusCode)
                {
                    var result = await response.Content.ReadFromJsonAsync<PowerShellConnectionTestResult>();
                    _logger.LogDebug("[API-SERVICE] Test połączenia zakończony pomyślnie");
                    return result;
                }
                else
                {
                    _logger.LogWarning("[API-SERVICE] Błąd testu połączenia: {StatusCode}", response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[API-SERVICE] Wyjątek podczas testu połączenia");
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
    }
} 