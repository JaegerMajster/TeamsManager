using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using TeamsManager.Core.Models;
using TeamsManager.Core.Abstractions.Services;
using Microsoft.Extensions.Logging;

namespace TeamsManager.UI.Services.Http;

public class TeamsManagerApiClient
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<TeamsManagerApiClient> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public TeamsManagerApiClient(HttpClient httpClient, ILogger<TeamsManagerApiClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = true
        };
    }

    public async Task<T?> GetAsync<T>(string endpoint, string? accessToken = null) where T : class
    {
        try
        {
            SetAuthorizationHeader(accessToken);
            
            _logger.LogDebug("Wywołanie GET API: {Endpoint}", endpoint);
            var response = await _httpClient.GetAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(json, _jsonOptions);
            }
            else
            {
                _logger.LogWarning("API GET nieudane: {Endpoint}, Status: {StatusCode}, Reason: {Reason}", 
                    endpoint, response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wywołania GET API: {Endpoint}", endpoint);
            return null;
        }
    }

    public async Task<TResponse?> PostAsync<TRequest, TResponse>(string endpoint, TRequest data, string? accessToken = null) 
        where TRequest : class 
        where TResponse : class
    {
        try
        {
            SetAuthorizationHeader(accessToken);
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Wywołanie POST API: {Endpoint}", endpoint);
            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                var responseJson = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TResponse>(responseJson, _jsonOptions);
            }
            else
            {
                _logger.LogWarning("API POST nieudane: {Endpoint}, Status: {StatusCode}, Reason: {Reason}", 
                    endpoint, response.StatusCode, response.ReasonPhrase);
                return null;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wywołania POST API: {Endpoint}", endpoint);
            return null;
        }
    }

    public async Task<bool> PostAsync<TRequest>(string endpoint, TRequest data, string? accessToken = null) 
        where TRequest : class
    {
        try
        {
            SetAuthorizationHeader(accessToken);
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Wywołanie POST API: {Endpoint}", endpoint);
            var response = await _httpClient.PostAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("API POST udane: {Endpoint}", endpoint);
                return true;
            }
            else
            {
                _logger.LogWarning("API POST nieudane: {Endpoint}, Status: {StatusCode}, Reason: {Reason}", 
                    endpoint, response.StatusCode, response.ReasonPhrase);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wywołania POST API: {Endpoint}", endpoint);
            return false;
        }
    }

    public async Task<bool> PutAsync<TRequest>(string endpoint, TRequest data, string? accessToken = null) 
        where TRequest : class
    {
        try
        {
            SetAuthorizationHeader(accessToken);
            
            var json = JsonSerializer.Serialize(data, _jsonOptions);
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            
            _logger.LogDebug("Wywołanie PUT API: {Endpoint}", endpoint);
            var response = await _httpClient.PutAsync(endpoint, content);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("API PUT udane: {Endpoint}", endpoint);
                return true;
            }
            else
            {
                _logger.LogWarning("API PUT nieudane: {Endpoint}, Status: {StatusCode}, Reason: {Reason}", 
                    endpoint, response.StatusCode, response.ReasonPhrase);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wywołania PUT API: {Endpoint}", endpoint);
            return false;
        }
    }

    public async Task<bool> DeleteAsync(string endpoint, string? accessToken = null)
    {
        try
        {
            SetAuthorizationHeader(accessToken);
            
            _logger.LogDebug("Wywołanie DELETE API: {Endpoint}", endpoint);
            var response = await _httpClient.DeleteAsync(endpoint);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("API DELETE udane: {Endpoint}", endpoint);
                return true;
            }
            else
            {
                _logger.LogWarning("API DELETE nieudane: {Endpoint}, Status: {StatusCode}, Reason: {Reason}", 
                    endpoint, response.StatusCode, response.ReasonPhrase);
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Błąd podczas wywołania DELETE API: {Endpoint}", endpoint);
            return false;
        }
    }

    private void SetAuthorizationHeader(string? accessToken)
    {
        if (!string.IsNullOrEmpty(accessToken))
        {
            _httpClient.DefaultRequestHeaders.Authorization = 
                new AuthenticationHeaderValue("Bearer", accessToken);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization = null;
        }
    }
} 