using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models.Graph;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis do pobierania profili użytkowników z Microsoft Graph API
    /// </summary>
    public class GraphUserProfileService : IGraphUserProfileService, IDisposable
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<GraphUserProfileService> _logger;
        private readonly GraphApiConfiguration _graphConfig;
        private bool _disposed = false;

        public GraphUserProfileService(
            HttpClient httpClient, 
            ILogger<GraphUserProfileService> logger,
            GraphApiConfiguration? graphConfig = null)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _graphConfig = graphConfig ?? new GraphApiConfiguration();
        }

        public async Task<UserProfile?> GetUserProfileAsync(string accessToken)
        {
            var httpClient = _httpClient;
            
            try
            {
                _logger.LogDebug("[GraphProfile] Rozpoczynanie pobierania profilu użytkownika...");
                
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", accessToken);

                var response = await httpClient.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Me}");
                if (response.IsSuccessStatusCode)
                {
                    var userProfile = await response.Content.ReadFromJsonAsync<UserProfile>();
                    _logger.LogDebug("Pobrano profil użytkownika pomyślnie");
                    return userProfile;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GraphProfile] Exception during GetUserProfileAsync");
            }

            return null;
        }

        public async Task<BitmapImage?> GetUserPhotoAsync(string accessToken)
        {
            var httpClient = _httpClient;
            
            try
            {
                _logger.LogDebug("[GraphPhoto] Rozpoczynanie pobierania zdjęcia użytkownika...");

                var requestUrl = "/v1.0/me/photo/$value";
                _logger.LogDebug("[GraphPhoto] Request URL: {RequestUrl}", requestUrl);

                var response = await httpClient.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Me}/photo/$value");
                
                _logger.LogDebug("[GraphPhoto] Response Status: {StatusCode}", response.StatusCode);

                if (response.IsSuccessStatusCode)
                {
                    var photoBytes = await response.Content.ReadAsByteArrayAsync();
                    _logger.LogDebug("[GraphPhoto] Photo size: {PhotoSize} bytes", photoBytes.Length);
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(photoBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Dla thread safety
                    
                    _logger.LogDebug("[GraphPhoto] Successfully created bitmap image");
                    return bitmap;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    _logger.LogDebug("[GraphPhoto] Error response: {StatusCode} - {ErrorContent}", response.StatusCode, errorContent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[GraphPhoto] Exception during GetUserPhotoAsync - brak zdjęcia to normalny przypadek");
            }

            return null;
        }

        // Metoda testowa do sprawdzenia czy token ma odpowiednie uprawnienia
        public async Task<GraphTestResult> TestGraphAccessAsync(string accessToken)
        {
            var httpClient = _httpClient;
            var result = new GraphTestResult();
            var startTime = DateTime.UtcNow;
            
            try
            {
                _logger.LogDebug("[GraphProfile] Sprawdzanie tokenów dla Graph API");
                
                // Dekoduj token żeby sprawdzić scopes
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var token = handler.ReadJwtToken(accessToken);
                    var scopes = token.Claims.FirstOrDefault(c => c.Type == "scp")?.Value;
                    var audience = token.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
                    
                    _logger.LogDebug("[GraphProfile] Token Audience: {Audience}, Scopes: {Scopes}", audience, scopes);
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[GraphProfile] Nie można zdekodować tokenu");
                }

                // Test endpoint /me
                try
                {
                    startTime = DateTime.UtcNow;
                    var meResponse = await httpClient.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Me}");
                    result.IsSuccessful = meResponse.IsSuccessStatusCode;
                    result.StatusCode = (int)meResponse.StatusCode;
                    result.ResponseTime = DateTime.UtcNow - startTime;
                    
                    _logger.LogDebug("[GraphProfile] /me Response Status: {StatusCode}", meResponse.StatusCode);
                    
                    if (!meResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await meResponse.Content.ReadAsStringAsync();
                        _logger.LogDebug("[GraphProfile] /me Error Content: {ErrorContent}", errorContent);
                        result.ErrorMessage = $"Me endpoint error: {errorContent}";
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[GraphProfile] Błąd podczas wywołania /me");
                    result.IsSuccessful = false;
                    result.StatusCode = 500;
                    result.ResponseTime = DateTime.UtcNow - startTime;
                    result.ErrorMessage = $"Me endpoint exception: {ex.Message}";
                }

                // Test endpoint /me/photo/$value
                try
                {
                    startTime = DateTime.UtcNow;
                    var photoResponse = await httpClient.GetAsync($"{_graphConfig.BaseUrl}{_graphConfig.Endpoints.Me}/photo/$value");
                    result.IsSuccessful = photoResponse.IsSuccessStatusCode;
                    result.StatusCode = (int)photoResponse.StatusCode;
                    result.ResponseTime = DateTime.UtcNow - startTime;
                    
                    _logger.LogDebug("[GraphProfile] /me/photo/$value Response Status: {StatusCode}", photoResponse.StatusCode);
                    
                    if (!photoResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await photoResponse.Content.ReadAsStringAsync();
                        _logger.LogDebug("[GraphProfile] /me/photo Error Content: {ErrorContent}", errorContent);
                        if (string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            result.ErrorMessage = $"Photo endpoint error: {errorContent}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug(ex, "[GraphProfile] Błąd podczas wywołania /me/photo");
                    result.IsSuccessful = false;
                    result.StatusCode = 500;
                    result.ResponseTime = DateTime.UtcNow - startTime;
                    result.ErrorMessage = $"Photo endpoint exception: {ex.Message}";
                }

                _logger.LogDebug("[GraphProfile] Test zakończony. Profile: {IsSuccessful}, Photo: {IsSuccessful}", result.IsSuccessful, result.IsSuccessful);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GraphProfile] Ogólny błąd podczas testowania Graph API");
                result.IsSuccessful = false;
                result.StatusCode = 500;
                result.ResponseTime = DateTime.UtcNow - startTime;
                result.ErrorMessage = $"General error: {ex.Message}";
                return result;
            }
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                // HttpClient jest zarządzany przez IHttpClientFactory - nie usuwamy go ręcznie
                _disposed = true;
            }
        }
    }

    public class UserProfile
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? UserPrincipalName { get; set; }
        public string? Mail { get; set; }
        public string? JobTitle { get; set; }
        public string? OfficeLocation { get; set; }
        public string? Department { get; set; }
        public string? CompanyName { get; set; }
        public string[]? BusinessPhones { get; set; }
        public string? MobilePhone { get; set; }
        public string? City { get; set; }
        public string? Country { get; set; }
        public string? PostalCode { get; set; }
        public string? StreetAddress { get; set; }
        public string? State { get; set; }
        public string? EmployeeType { get; set; }
        public string? EmployeeId { get; set; }
        public BitmapImage? ProfilePicture { get; set; }
    }

    public class GraphTestResult
    {
        public bool IsSuccessful { get; set; } = false;
        public int StatusCode { get; set; } = 0;
        public TimeSpan ResponseTime { get; set; } = TimeSpan.Zero;
        public string ErrorMessage { get; set; } = "";
    }
} 