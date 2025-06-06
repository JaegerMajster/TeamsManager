using System;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Nowoczesny HTTP service wykorzystujący Microsoft.Extensions.Http.Resilience
    /// Zastępuje starą implementację resilience patterns
    /// </summary>
    public class ModernHttpService : IModernHttpService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<ModernHttpService> _logger;

        public ModernHttpService(IHttpClientFactory httpClientFactory, ILogger<ModernHttpService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
        }

        /// <summary>
        /// Wykonuje żądanie GET do Microsoft Graph API z nowoczesnym resilience
        /// </summary>
        public async Task<T?> GetFromGraphAsync<T>(string endpoint, string? accessToken = null) where T : class
        {
            // Walidacja argumentów
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("Endpoint cannot be null or empty", nameof(endpoint));
            }

            using var client = _httpClientFactory.CreateClient("MicrosoftGraph");
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                _logger.LogDebug("Making GET request to Graph API endpoint: {Endpoint}", endpoint);
                
                var response = await client.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Successfully received response from Graph API endpoint: {Endpoint}", endpoint);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Graph API request failed. Endpoint: {Endpoint}, StatusCode: {StatusCode}, Reason: {Reason}",
                        endpoint, response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when calling Graph API endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Timeout when calling Graph API endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when calling Graph API endpoint: {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// Wykonuje żądanie POST do Microsoft Graph API z nowoczesnym resilience
        /// </summary>
        public async Task<TResponse?> PostToGraphAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest data, 
            string? accessToken = null) 
            where TRequest : class 
            where TResponse : class
        {
            using var client = _httpClientFactory.CreateClient("MicrosoftGraph");
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var jsonContent = JsonSerializer.Serialize(data);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Making POST request to Graph API endpoint: {Endpoint}", endpoint);
                
                var response = await client.PostAsync(endpoint, httpContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Successfully posted to Graph API endpoint: {Endpoint}", endpoint);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Graph API POST failed. Endpoint: {Endpoint}, StatusCode: {StatusCode}, Reason: {Reason}",
                        endpoint, response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "HTTP request exception when posting to Graph API endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Timeout when posting to Graph API endpoint: {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Unexpected error when posting to Graph API endpoint: {Endpoint}", endpoint);
                throw;
            }
        }

        /// <summary>
        /// Wykonuje żądanie do zewnętrznego API z resilience
        /// </summary>
        public async Task<T?> GetFromExternalApiAsync<T>(string url) where T : class
        {
            // Walidacja argumentów
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL cannot be null or empty", nameof(url));
            }

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                throw new ArgumentException("URL must be a valid absolute URI", nameof(url));
            }

            using var client = _httpClientFactory.CreateClient("ExternalApis");
            
            try
            {
                _logger.LogDebug("Making GET request to external API: {Url}", url);
                
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Successfully received response from external API: {Url}", url);
                    return result;
                }
                else
                {
                    _logger.LogWarning("External API request failed. Url: {Url}, StatusCode: {StatusCode}",
                        url, response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error when calling external API: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Sprawdza dostępność Graph API z resilience
        /// </summary>
        public async Task<bool> CheckGraphApiHealthAsync(string? accessToken = null)
        {
            try
            {
                // Wywołanie prostego endpointu Graph API do sprawdzenia dostępności
                var result = await GetFromGraphAsync<object>("v1.0/me", accessToken);
                return result != null;
            }
            catch
            {
                return false;
            }
        }
    }
} 