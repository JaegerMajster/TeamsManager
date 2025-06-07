using Microsoft.Identity.Client;
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows.Media.Imaging;
using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using TeamsManager.UI.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace TeamsManager.UI.Services
{
    public class GraphUserProfileService : IGraphUserProfileService, IDisposable
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<GraphUserProfileService> _logger;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private bool _disposed = false;

        public GraphUserProfileService(IHttpClientFactory httpClientFactory, ILogger<GraphUserProfileService> logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<UserProfile?> GetUserProfileAsync(string accessToken)
        {
            var httpClient = _httpClientFactory.CreateClient("MicrosoftGraph");
            
            try
            {
                _logger.LogDebug("[GraphProfile] Rozpoczynanie pobierania profilu użytkownika...");
                
                var requestUrl = "/v1.0/me";
                _logger.LogDebug("[GraphProfile] Request URL: {RequestUrl}", requestUrl);

                var response = await httpClient.GetAsync(requestUrl);
                
                _logger.LogDebug("[GraphProfile] Response Status: {StatusCode}", response.StatusCode);

                var responseContent = await response.Content.ReadAsStringAsync();
                _logger.LogDebug("[GraphProfile] Response Content: {ResponseContent}", responseContent);

                if (response.IsSuccessStatusCode)
                {
                    var userProfile = JsonSerializer.Deserialize<UserProfile>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    _logger.LogDebug("[GraphProfile] Successfully parsed profile: {DisplayName}", userProfile?.DisplayName);
                    return userProfile;
                }
                else
                {
                    _logger.LogWarning("[GraphProfile] Error response: {StatusCode} - {ResponseContent}", response.StatusCode, responseContent);
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
            var httpClient = _httpClientFactory.CreateClient("MicrosoftGraph");
            
            try
            {
                _logger.LogDebug("[GraphPhoto] Rozpoczynanie pobierania zdjęcia użytkownika...");

                var requestUrl = "/v1.0/me/photo/$value";
                _logger.LogDebug("[GraphPhoto] Request URL: {RequestUrl}", requestUrl);

                var response = await httpClient.GetAsync(requestUrl);
                
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
            var httpClient = _httpClientFactory.CreateClient("MicrosoftGraph");
            var result = new GraphTestResult();
            
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
                    var meResponse = await httpClient.GetAsync("/v1.0/me");
                    result.MeEndpointStatus = meResponse.StatusCode.ToString();
                    result.CanAccessProfile = meResponse.IsSuccessStatusCode;
                    
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
                    result.MeEndpointStatus = "Exception";
                    result.ErrorMessage = $"Me endpoint exception: {ex.Message}";
                    result.CanAccessProfile = false;
                }

                // Test endpoint /me/photo/$value
                try
                {
                    var photoResponse = await httpClient.GetAsync("/v1.0/me/photo/$value");
                    result.PhotoEndpointStatus = photoResponse.StatusCode.ToString();
                    result.CanAccessPhoto = photoResponse.IsSuccessStatusCode;
                    
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
                    result.PhotoEndpointStatus = "Exception";
                    if (string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        result.ErrorMessage = $"Photo endpoint exception: {ex.Message}";
                    }
                    result.CanAccessPhoto = false;
                }

                _logger.LogDebug("[GraphProfile] Test zakończony. Profile: {CanAccessProfile}, Photo: {CanAccessPhoto}", result.CanAccessProfile, result.CanAccessPhoto);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[GraphProfile] Ogólny błąd podczas testowania Graph API");
                result.ErrorMessage = $"General error: {ex.Message}";
                result.MeEndpointStatus = "Error";
                result.PhotoEndpointStatus = "Error";
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
        public BitmapImage? ProfilePicture { get; set; }
    }

    public class GraphTestResult
    {
        public bool CanAccessProfile { get; set; } = false;
        public bool CanAccessPhoto { get; set; } = false;
        public string MeEndpointStatus { get; set; } = "";
        public string PhotoEndpointStatus { get; set; } = "";
        public string ErrorMessage { get; set; } = "";
    }
} 