using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Identity.Client;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.Core.Services
{
    /// <summary>
    /// Nowoczesny HTTP service wykorzystujący Microsoft.Extensions.Http.Resilience
    /// Zastępuje starą implementację resilience patterns
    /// </summary>
    public class ModernHttpService : IModernHttpService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ModernHttpService> _logger;
        private readonly IConfidentialClientApplication _confidentialClientApp;
        private readonly JsonSerializerOptions _jsonOptions;
        private readonly GraphApiConfiguration _graphConfig;

        public ModernHttpService(
            HttpClient httpClient,
            ILogger<ModernHttpService> logger,
            IConfidentialClientApplication confidentialClientApp,
            GraphApiConfiguration? graphConfig = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _confidentialClientApp = confidentialClientApp ?? throw new ArgumentNullException(nameof(confidentialClientApp));
            _graphConfig = graphConfig ?? new GraphApiConfiguration();
            
            _jsonOptions = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        // ===== PODSTAWOWE METODY HTTP ZWRACAJĄCE HttpResponseMessage =====

        /// <summary>
        /// Wykonuje żądanie GET zwracające HttpResponseMessage
        /// </summary>
        public async Task<HttpResponseMessage> GetAsync(string url, Dictionary<string, string>? headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL nie może być null lub pusty", nameof(url));
            }

            var client = GetHttpClient(url);
            AddHeaders(client, headers);

            try
            {
                // ✅ NAPRAWKA OBO: Sprawdź czy URL jest względny i dodaj Graph base URL
                var finalUrl = GetAbsoluteUrl(url);
                
                _logger.LogDebug("Wykonywanie żądania GET do: {Url} (finalne URL: {FinalUrl})", url, finalUrl);
                var response = await client.GetAsync(finalUrl);
                _logger.LogDebug("Żądanie GET zakończone. Status: {StatusCode}", response.StatusCode);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas żądania GET do: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Wykonuje żądanie POST zwracające HttpResponseMessage
        /// </summary>
        public async Task<HttpResponseMessage> PostAsync(string url, string content, Dictionary<string, string>? headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL nie może być null lub pusty", nameof(url));
            }

            var client = GetHttpClient(url);
            AddHeaders(client, headers);

            try
            {
                // ✅ NAPRAWKA OBO: Sprawdź czy URL jest względny i dodaj Graph base URL
                var finalUrl = GetAbsoluteUrl(url);
                
                _logger.LogDebug("Wykonywanie żądania POST do: {Url} (finalne URL: {FinalUrl})", url, finalUrl);
                var httpContent = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/json");
                var response = await client.PostAsync(finalUrl, httpContent);
                _logger.LogDebug("Żądanie POST zakończone. Status: {StatusCode}", response.StatusCode);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas żądania POST do: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Wykonuje żądanie PATCH zwracające HttpResponseMessage
        /// </summary>
        public async Task<HttpResponseMessage> PatchAsync(string url, string content, Dictionary<string, string>? headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL nie może być null lub pusty", nameof(url));
            }

            var client = GetHttpClient(url);
            AddHeaders(client, headers);

            try
            {
                // ✅ NAPRAWKA OBO: Sprawdź czy URL jest względny i dodaj Graph base URL
                var finalUrl = GetAbsoluteUrl(url);
                
                _logger.LogDebug("Wykonywanie żądania PATCH do: {Url} (finalne URL: {FinalUrl})", url, finalUrl);
                var httpContent = new StringContent(content ?? string.Empty, Encoding.UTF8, "application/json");
                var response = await client.PatchAsync(finalUrl, httpContent);
                _logger.LogDebug("Żądanie PATCH zakończone. Status: {StatusCode}", response.StatusCode);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas żądania PATCH do: {Url}", url);
                throw;
            }
        }

        /// <summary>
        /// Wykonuje żądanie DELETE zwracające HttpResponseMessage
        /// </summary>
        public async Task<HttpResponseMessage> DeleteAsync(string url, Dictionary<string, string>? headers = null)
        {
            if (string.IsNullOrEmpty(url))
            {
                throw new ArgumentException("URL nie może być null lub pusty", nameof(url));
            }

            var client = GetHttpClient(url);
            AddHeaders(client, headers);

            try
            {
                // ✅ NAPRAWKA OBO: Sprawdź czy URL jest względny i dodaj Graph base URL
                var finalUrl = GetAbsoluteUrl(url);
                
                _logger.LogDebug("Wykonywanie żądania DELETE do: {Url} (finalne URL: {FinalUrl})", url, finalUrl);
                var response = await client.DeleteAsync(finalUrl);
                _logger.LogDebug("Żądanie DELETE zakończone. Status: {StatusCode}", response.StatusCode);
                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas żądania DELETE do: {Url}", url);
                throw;
            }
        }

        // ===== METODY POMOCNICZE =====

        private HttpClient GetHttpClient(string url)
        {
            // Określ typ klienta na podstawie URL
            if (url.Contains("graph.microsoft.com") || url.StartsWith("/v1.0") || url.StartsWith("v1.0"))
            {
                return _httpClient;
            }
            else
            {
                return _httpClient;
            }
        }

        private void AddHeaders(HttpClient client, Dictionary<string, string>? headers)
        {
            if (headers != null)
            {
                foreach (var header in headers)
                {
                    client.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
                }
            }
        }

        // ===== ISTNIEJĄCE METODY GENERYCZNE =====

        /// <summary>
        /// Wykonuje żądanie GET do Microsoft Graph API z nowoczesnym resilience
        /// </summary>
        public async Task<T?> GetFromGraphAsync<T>(string endpoint, string? accessToken = null) where T : class
        {
            // Walidacja argumentów
            if (string.IsNullOrEmpty(endpoint))
            {
                throw new ArgumentException("Endpoint nie może być null lub pusty", nameof(endpoint));
            }

            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                _logger.LogDebug("Wykonywanie żądania GET do endpointu Graph API: {Endpoint}", endpoint);
                
                var response = await client.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Pomyślnie otrzymano odpowiedź z endpointu Graph API: {Endpoint}", endpoint);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Żądanie Graph API nie powiodło się. Endpoint: {Endpoint}, StatusCode: {StatusCode}, Reason: {Reason}",
                        endpoint, response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas wywołania endpointu Graph API: {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas wywołania endpointu Graph API: {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas wywołania endpointu Graph API: {Endpoint}", endpoint);
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
            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var jsonContent = JsonSerializer.Serialize(data);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Wykonywanie żądania POST do endpointu Graph API: {Endpoint}", endpoint);
                
                var response = await client.PostAsync(endpoint, httpContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Pomyślnie wysłano żądanie POST do endpointu Graph API: {Endpoint}", endpoint);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Żądanie POST Graph API nie powiodło się. Endpoint: {Endpoint}, StatusCode: {StatusCode}, Reason: {Reason}",
                        endpoint, response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas wysyłania POST do endpointu Graph API: {Endpoint}", endpoint);
                throw;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas wysyłania POST do endpointu Graph API: {Endpoint}", endpoint);
                throw;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas wysyłania POST do endpointu Graph API: {Endpoint}", endpoint);
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
                throw new ArgumentException("URL nie może być null lub pusty", nameof(url));
            }

            if (!Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                throw new ArgumentException("URL musi być prawidłowym bezwzględnym URI", nameof(url));
            }

            var client = _httpClient;
            
            try
            {
                _logger.LogDebug("Wykonywanie żądania GET do zewnętrznego API: {Url}", url);
                
                var response = await client.GetAsync(url);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Pomyślnie otrzymano odpowiedź z zewnętrznego API: {Url}", url);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Żądanie do zewnętrznego API nie powiodło się. Url: {Url}, StatusCode: {StatusCode}",
                        url, response.StatusCode);
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wywołania zewnętrznego API: {Url}", url);
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

        // ===== MAIL API METHODS IMPLEMENTATION =====

        /// <summary>
        /// Wysyła email przez Microsoft Graph Mail API
        /// Endpoint: POST /v1.0/me/sendMail
        /// </summary>
        public async Task<bool> SendMailAsync<TRequest>(TRequest emailData, string? accessToken = null) 
            where TRequest : class
        {
            if (emailData == null)
            {
                throw new ArgumentNullException(nameof(emailData));
            }

            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var jsonContent = JsonSerializer.Serialize(emailData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Wysyłanie emaila przez endpoint Graph API Mail");
                
                var response = await client.PostAsync("v1.0/me/sendMail", httpContent);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Email wysłany pomyślnie przez Graph API");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Nie udało się wysłać emaila przez Graph API. StatusCode: {StatusCode}, Reason: {Reason}",
                        response.StatusCode, response.ReasonPhrase);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas wysyłania emaila przez Graph API");
                return false;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas wysyłania emaila przez Graph API");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas wysyłania emaila przez Graph API");
                return false;
            }
        }

        /// <summary>
        /// Wysyła email w imieniu określonego użytkownika przez Microsoft Graph Mail API
        /// Endpoint: POST /v1.0/users/{user-id}/sendMail
        /// </summary>
        public async Task<bool> SendMailOnBehalfOfUserAsync<TRequest>(
            string userId, 
            TRequest emailData, 
            string? accessToken = null) 
            where TRequest : class
        {
            if (string.IsNullOrEmpty(userId))
            {
                throw new ArgumentException("ID użytkownika nie może być null lub pusty", nameof(userId));
            }
            
            if (emailData == null)
            {
                throw new ArgumentNullException(nameof(emailData));
            }

            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var jsonContent = JsonSerializer.Serialize(emailData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                var endpoint = $"v1.0/users/{userId}/sendMail";
                
                _logger.LogDebug("Wysyłanie emaila w imieniu użytkownika {UserId} przez Graph API", userId);
                
                var response = await client.PostAsync(endpoint, httpContent);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogDebug("Email wysłany pomyślnie w imieniu użytkownika {UserId} przez Graph API", userId);
                    return true;
                }
                else
                {
                    _logger.LogWarning("Nie udało się wysłać emaila w imieniu użytkownika {UserId} przez Graph API. StatusCode: {StatusCode}, Reason: {Reason}",
                        userId, response.StatusCode, response.ReasonPhrase);
                    return false;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas wysyłania emaila w imieniu użytkownika {UserId} przez Graph API", userId);
                return false;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas wysyłania emaila w imieniu użytkownika {UserId} przez Graph API", userId);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas wysyłania emaila w imieniu użytkownika {UserId} przez Graph API", userId);
                return false;
            }
        }

        /// <summary>
        /// Tworzy draft email przez Microsoft Graph Mail API
        /// Endpoint: POST /v1.0/me/messages
        /// </summary>
        public async Task<TResponse?> CreateDraftEmailAsync<TRequest, TResponse>(
            TRequest emailData, 
            string? accessToken = null) 
            where TRequest : class 
            where TResponse : class
        {
            if (emailData == null)
            {
                throw new ArgumentNullException(nameof(emailData));
            }

            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var jsonContent = JsonSerializer.Serialize(emailData);
                var httpContent = new StringContent(jsonContent, Encoding.UTF8, "application/json");
                
                _logger.LogDebug("Tworzenie szkicu emaila przez Graph API");
                
                var response = await client.PostAsync("v1.0/me/messages", httpContent);
                
                if (response.IsSuccessStatusCode)
                {
                    var responseContent = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TResponse>(responseContent, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Szkic emaila utworzony pomyślnie przez Graph API");
                    return result;
                }
                else
                {
                    _logger.LogWarning("Nie udało się utworzyć szkicu emaila przez Graph API. StatusCode: {StatusCode}, Reason: {Reason}",
                        response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas tworzenia szkicu emaila przez Graph API");
                return null;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas tworzenia szkicu emaila przez Graph API");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas tworzenia szkicu emaila przez Graph API");
                return null;
            }
        }

        /// <summary>
        /// Pobiera wiadomości email z skrzynki użytkownika
        /// Endpoint: GET /v1.0/me/messages
        /// </summary>
        public async Task<TResponse?> GetMailMessagesAsync<TResponse>(
            string? accessToken = null,
            string? filter = null,
            string? select = null,
            int? top = null) 
            where TResponse : class
        {
            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var queryParams = new List<string>();
                
                if (!string.IsNullOrEmpty(filter))
                    queryParams.Add($"$filter={Uri.EscapeDataString(filter)}");
                
                if (!string.IsNullOrEmpty(select))
                    queryParams.Add($"$select={Uri.EscapeDataString(select)}");
                
                if (top.HasValue)
                    queryParams.Add($"$top={top.Value}");

                var endpoint = "v1.0/me/messages";
                if (queryParams.Any())
                    endpoint += "?" + string.Join("&", queryParams);
                
                _logger.LogDebug("Pobieranie wiadomości email przez Graph API: {Endpoint}", endpoint);
                
                var response = await client.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TResponse>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Wiadomości email pobrane pomyślnie przez Graph API");
                    return result;
                }
                else
                {
                    _logger.LogWarning("Nie udało się pobrać wiadomości email przez Graph API. StatusCode: {StatusCode}, Reason: {Reason}",
                        response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas pobierania wiadomości email przez Graph API");
                return null;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas pobierania wiadomości email przez Graph API");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas pobierania wiadomości email przez Graph API");
                return null;
            }
        }

        /// <summary>
        /// Pobiera konkretną wiadomość email
        /// Endpoint: GET /v1.0/me/messages/{message-id}
        /// </summary>
        public async Task<TResponse?> GetMailMessageAsync<TResponse>(
            string messageId,
            string? accessToken = null) 
            where TResponse : class
        {
            if (string.IsNullOrEmpty(messageId))
            {
                throw new ArgumentException("ID wiadomości nie może być null lub pusty", nameof(messageId));
            }

            var client = _httpClient;
            
            if (!string.IsNullOrEmpty(accessToken))
            {
                client.DefaultRequestHeaders.Authorization = 
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);
            }

            try
            {
                var endpoint = $"v1.0/me/messages/{messageId}";
                
                _logger.LogDebug("Pobieranie wiadomości email {MessageId} przez Graph API", messageId);
                
                var response = await client.GetAsync(endpoint);
                
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var result = JsonSerializer.Deserialize<TResponse>(content, new JsonSerializerOptions 
                    { 
                        PropertyNameCaseInsensitive = true 
                    });
                    
                    _logger.LogDebug("Wiadomość email {MessageId} pobrana pomyślnie przez Graph API", messageId);
                    return result;
                }
                else
                {
                    _logger.LogWarning("Nie udało się pobrać wiadomości email {MessageId} przez Graph API. StatusCode: {StatusCode}, Reason: {Reason}",
                        messageId, response.StatusCode, response.ReasonPhrase);
                    return null;
                }
            }
            catch (HttpRequestException ex)
            {
                _logger.LogError(ex, "Wyjątek żądania HTTP podczas pobierania wiadomości email {MessageId} przez Graph API", messageId);
                return null;
            }
            catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
            {
                _logger.LogError(ex, "Przekroczenie czasu podczas pobierania wiadomości email {MessageId} przez Graph API", messageId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Nieoczekiwany błąd podczas pobierania wiadomości email {MessageId} przez Graph API", messageId);
                return null;
            }
        }

        // ===== NOT IMPLEMENTED METHODS =====
        // Poniższe metody nie są jeszcze zaimplementowane - będą dodane w przyszłych etapach

        public Task<TResponse?> PatchToGraphAsync<TRequest, TResponse>(string endpoint, TRequest data, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams, IGraphService.Users lub IGraphService.BulkOperations
            throw new NotSupportedException("Użyj dedykowanych serwisów Graph API przez IGraphService");
        }

        public Task<bool> DeleteFromGraphAsync(string endpoint, string? accessToken = null)
        {
            // Użyj IGraphService.Teams, IGraphService.Users lub IGraphService.BulkOperations
            throw new NotSupportedException("Użyj dedykowanych serwisów Graph API przez IGraphService");
        }

        public Task<TResponse?> CreateTeamAsync<TRequest, TResponse>(TRequest teamData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams.CreateTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.CreateTeamAsync()");
        }

        public Task<TResponse?> UpdateTeamAsync<TRequest, TResponse>(string teamId, TRequest updateData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams.UpdateTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.UpdateTeamAsync()");
        }

        public Task<TResponse?> GetTeamAsync<TResponse>(string teamId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.GetTeamAsync()");
        }

        public Task<TResponse?> GetAllTeamsAsync<TResponse>(string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetAllTeamsAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.GetAllTeamsAsync()");
        }

        public Task<bool> ArchiveTeamAsync(string teamId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.ArchiveTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.ArchiveTeamAsync()");
        }

        public Task<bool> UnarchiveTeamAsync(string teamId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.UnarchiveTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.UnarchiveTeamAsync()");
        }

        public Task<bool> DeleteTeamAsync(string teamId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.DeleteTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.DeleteTeamAsync()");
        }

        public Task<TResponse?> GetTeamMembersAsync<TResponse>(string teamId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Users.GetTeamMembersAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.GetTeamMembersAsync()");
        }

        public Task<TResponse?> AddTeamMemberAsync<TRequest, TResponse>(string teamId, TRequest memberData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Users.AddUserToTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.AddUserToTeamAsync()");
        }

        public Task<bool> RemoveTeamMemberAsync(string teamId, string membershipId, string? accessToken = null)
        {
            // Użyj IGraphService.Users.RemoveUserFromTeamAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.RemoveUserFromTeamAsync()");
        }

        public Task<TResponse?> GetTeamChannelsAsync<TResponse>(string teamId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetTeamChannelsAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.GetTeamChannelsAsync()");
        }

        public Task<TResponse?> CreateTeamChannelAsync<TRequest, TResponse>(string teamId, TRequest channelData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams.CreateChannelAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.CreateChannelAsync()");
        }

        public Task<TResponse?> UpdateTeamChannelAsync<TRequest, TResponse>(string teamId, string channelId, TRequest updateData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams.UpdateChannelAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.UpdateChannelAsync()");
        }

        public Task<bool> DeleteTeamChannelAsync(string teamId, string channelId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.DeleteChannelAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.DeleteChannelAsync()");
        }

        public Task<TResponse?> GetTeamChannelAsync<TResponse>(string teamId, string channelId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetChannelAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.GetChannelAsync()");
        }

        public Task<TResponse?> CreateUserAsync<TRequest, TResponse>(TRequest userData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Users.CreateM365UserAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.CreateM365UserAsync()");
        }

        public Task<TResponse?> UpdateUserAsync<TRequest, TResponse>(string userId, TRequest updateData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Users.UpdateM365UserPropertiesAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.UpdateM365UserPropertiesAsync()");
        }

        public Task<TResponse?> GetUserAsync<TResponse>(string userId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Users.GetM365UserByIdAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.GetM365UserByIdAsync()");
        }

        public Task<TResponse?> GetAllUsersAsync<TResponse>(string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Users.GetAllUsersAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.GetAllUsersAsync()");
        }

        public Task<bool> DeleteUserAsync(string userId, string? accessToken = null)
        {
            // Użyj IGraphService.Users.DeleteM365UserAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.DeleteM365UserAsync()");
        }

        public Task<TResponse?> AssignUserLicenseAsync<TRequest, TResponse>(string userId, TRequest licenseData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Users.AssignLicenseToUserAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.AssignLicenseToUserAsync()");
        }

        public Task<TResponse?> GetUserLicensesAsync<TResponse>(string userId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Users.GetUserLicensesAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.GetUserLicensesAsync()");
        }

        public Task<bool> RevokeUserSignInSessionsAsync(string userId, string? accessToken = null)
        {
            // Użyj IGraphService.Users.RevokeUserSignInSessionsAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.RevokeUserSignInSessionsAsync()");
        }

        public Task<TResponse?> GetUsersByDepartmentAsync<TResponse>(string department, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Users.GetUsersByDepartmentAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.GetUsersByDepartmentAsync()");
        }

        public Task<TResponse?> GetInactiveUsersAsync<TResponse>(int daysInactive, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Users.GetInactiveUsersAsync()
            throw new NotSupportedException("Użyj IGraphService.Users.GetInactiveUsersAsync()");
        }

        public Task<TResponse?> GetUserTeamsAsync<TResponse>(string userId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetUserTeamsAsync()
            throw new NotSupportedException("Użyj IGraphService.Teams.GetUserTeamsAsync()");
        }

        public Task<TResponse?> CreateGroupAsync<TRequest, TResponse>(TRequest groupData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams.CreateGroupAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> UpdateGroupAsync<TRequest, TResponse>(string groupId, TRequest updateData, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.Teams.UpdateGroupAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetGroupAsync<TResponse>(string groupId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetGroupAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetAllGroupsAsync<TResponse>(string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetAllGroupsAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<bool> DeleteGroupAsync(string groupId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.DeleteGroupAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetGroupMembersAsync<TResponse>(string groupId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetGroupMembersAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<bool> AddGroupMemberAsync(string groupId, string userId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.AddGroupMemberAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<bool> RemoveGroupMemberAsync(string groupId, string userId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.RemoveGroupMemberAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetGroupOwnersAsync<TResponse>(string groupId, string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetGroupOwnersAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<bool> AddGroupOwnerAsync(string groupId, string userId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.AddGroupOwnerAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<bool> RemoveGroupOwnerAsync(string groupId, string userId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.RemoveGroupOwnerAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetMicrosoft365GroupsAsync<TResponse>(string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetMicrosoft365GroupsAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetSecurityGroupsAsync<TResponse>(string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetSecurityGroupsAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<TResponse?> GetDistributionGroupsAsync<TResponse>(string? accessToken = null) where TResponse : class
        {
            // Użyj IGraphService.Teams.GetDistributionGroupsAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<bool> GroupHasTeamAsync(string groupId, string? accessToken = null)
        {
            // Użyj IGraphService.Teams.GroupHasTeamAsync() (jeśli dostępne)
            throw new NotSupportedException("Funkcjonalność grup będzie dostępna w przyszłych wersjach");
        }

        public Task<IEnumerable<TResponse?>> ExecuteParallelGetRequestsAsync<TResponse>(IEnumerable<string> endpoints, string? accessToken = null, int batchSize = 20) where TResponse : class
        {
            // Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()
            throw new NotSupportedException("Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()");
        }

        public Task<IEnumerable<TResponse?>> ExecuteParallelPostRequestsAsync<TRequest, TResponse>(IEnumerable<(string endpoint, TRequest data)> operations, string? accessToken = null, int batchSize = 20) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()
            throw new NotSupportedException("Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()");
        }

        public Task<IEnumerable<TResponse?>> ExecuteParallelPatchRequestsAsync<TRequest, TResponse>(IEnumerable<(string endpoint, TRequest data)> operations, string? accessToken = null, int batchSize = 20) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()
            throw new NotSupportedException("Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()");
        }

        public Task<IEnumerable<bool>> ExecuteParallelDeleteRequestsAsync(IEnumerable<string> endpoints, string? accessToken = null, int batchSize = 20)
        {
            // Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()
            throw new NotSupportedException("Użyj IGraphService.BulkOperations.ExecuteBatchOperationsAsync()");
        }

        public Task<(int TotalOperations, int SuccessfulOperations, int FailedOperations, IEnumerable<TResponse?> Results, IEnumerable<string> Errors, DateTime CompletedAt)> ExecuteBulkUserOperationsAsync<TRequest, TResponse>(IEnumerable<(string operationType, string endpoint, TRequest? data)> operations, IProgress<(int completed, int total, string currentOperation)>? progress = null, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.BulkOperations.BulkAddUsersToTeamAsync(), BulkRemoveUsersFromTeamAsync(), itp.
            throw new NotSupportedException("Użyj dedykowanych metod bulk w IGraphService.BulkOperations");
        }

        public Task<(int TotalOperations, int SuccessfulOperations, int FailedOperations, IEnumerable<TResponse?> Results, IEnumerable<string> Errors, DateTime CompletedAt)> ExecuteBulkTeamOperationsAsync<TRequest, TResponse>(IEnumerable<(string operationType, string endpoint, TRequest? data)> operations, IProgress<(int completed, int total, string currentOperation)>? progress = null, string? accessToken = null) where TRequest : class where TResponse : class
        {
            // Użyj IGraphService.BulkOperations.BulkArchiveTeamsAsync(), itp.
            throw new NotSupportedException("Użyj dedykowanych metod bulk w IGraphService.BulkOperations");
        }

        // Metoda pomocnicza dla przyszłych rozszerzeń
        public async Task<TResponse?> PostToGraphWithRetryAsync<TRequest, TResponse>(
            string endpoint, 
            TRequest data, 
            string? accessToken = null) 
            where TRequest : class 
            where TResponse : class
        {
            _logger.LogDebug("PostToGraphWithRetryAsync - endpoint: {Endpoint}", endpoint);
            
            // Ta metoda będzie rozszerzona w przyszłych wersjach o retry logic
            return await PostToGraphAsync<TRequest, TResponse>(endpoint, data, accessToken);
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
                _logger.LogDebug("Pobieranie tokenu dostępu z ModernHttpService...");
                
                var result = await _confidentialClientApp
                    .AcquireTokenForClient(_graphConfig.Scopes.ClientCredentials)
                    .ExecuteAsync();

                if (result?.AccessToken != null)
                {
                    _logger.LogDebug("Token dostępu pobrany pomyślnie");
                    return result.AccessToken;
                }

                _logger.LogWarning("Nie udało się pobrać tokenu dostępu");
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania tokenu dostępu");
                return null;
            }
        }

        /// <summary>
        /// Wykonuje żądanie GET zwracające obiekt typu T
        /// </summary>
        public async Task<T?> GetAsync<T>(string url, Dictionary<string, string>? headers = null)
        {
            try
            {
                var response = await GetAsync(url, headers);
                if (!response.IsSuccessStatusCode)
                {
                    return default(T);
                }

                var jsonContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<T>(jsonContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania żądania GET<T> dla URL: {Url}", url);
                return default(T);
            }
        }

        /// <summary>
        /// Wykonuje żądanie POST z obiektem typu TRequest zwracające obiekt typu TResponse
        /// </summary>
        public async Task<TResponse?> PostAsync<TRequest, TResponse>(string url, TRequest content, Dictionary<string, string>? headers = null)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(content, _jsonOptions);
                var response = await PostAsync(url, jsonContent, headers);
                
                if (!response.IsSuccessStatusCode)
                {
                    return default(TResponse);
                }

                var responseContent = await response.Content.ReadAsStringAsync();
                return JsonSerializer.Deserialize<TResponse>(responseContent, _jsonOptions);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania żądania POST<TRequest, TResponse> dla URL: {Url}", url);
                return default(TResponse);
            }
        }

        /// <summary>
        /// Wykonuje żądanie PUT z obiektem typu T
        /// </summary>
        public async Task PutAsync<T>(string url, T content, Dictionary<string, string>? headers = null)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(content, _jsonOptions);
                
                var request = new HttpRequestMessage(HttpMethod.Put, url);
                request.Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                if (headers != null)
                {
                    foreach (var header in headers)
                    {
                        request.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }
                }

                var response = await _httpClient.SendAsync(request);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Żądanie PUT<T> zakończone niepowodzeniem dla URL: {Url}, Status: {StatusCode}", 
                        url, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania żądania PUT<T> dla URL: {Url}", url);
            }
        }

        /// <summary>
        /// Wykonuje żądanie PATCH z obiektem typu T
        /// </summary>
        public async Task PatchAsync<T>(string url, T content, Dictionary<string, string>? headers = null)
        {
            try
            {
                var jsonContent = JsonSerializer.Serialize(content, _jsonOptions);
                var response = await PatchAsync(url, jsonContent, headers);
                
                if (!response.IsSuccessStatusCode)
                {
                    _logger.LogWarning("Żądanie PATCH<T> zakończone niepowodzeniem dla URL: {Url}, Status: {StatusCode}", 
                        url, response.StatusCode);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania żądania PATCH<T> dla URL: {Url}", url);
            }
        }

        private string GetAbsoluteUrl(string url)
        {
            // Jeśli URL jest już absolutny, zwróć go bez zmian
            if (Uri.IsWellFormedUriString(url, UriKind.Absolute))
            {
                return url;
            }
            
            // Jeśli URL jest względny i dotyczy Graph API, dodaj base URL
            if (url.StartsWith("v1.0/") || url.StartsWith("/v1.0/") || url.StartsWith("me") || url.StartsWith("/me"))
            {
                var baseUrl = "https://graph.microsoft.com/";
                var cleanUrl = url.TrimStart('/');
                if (!cleanUrl.StartsWith("v1.0/"))
                {
                    cleanUrl = "v1.0/" + cleanUrl;
                }
                return baseUrl + cleanUrl;
            }
            
            // W przeciwnym razie zwróć URL bez zmian (może być lokalny endpoint)
            return url;
        }
    }
} 
