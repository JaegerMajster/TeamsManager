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

namespace TeamsManager.UI.Services
{
    public class GraphUserProfileService : IDisposable
    {
        private readonly HttpClient _httpClient;
        private const string GraphBaseUrl = "https://graph.microsoft.com/v1.0";
        private bool _disposed = false;

        public GraphUserProfileService()
        {
            _httpClient = new HttpClient();
        }

        public async Task<UserProfile?> GetUserProfileAsync(string accessToken)
        {
            try
            {
                Debug.WriteLine($"[GraphProfile] Rozpoczynanie pobierania profilu użytkownika...");
                Debug.WriteLine($"[GraphProfile] Token length: {accessToken?.Length ?? 0}");
                Debug.WriteLine($"[GraphProfile] Token fragment: {accessToken?.Substring(0, Math.Min(accessToken?.Length ?? 0, 20))}...");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{GraphBaseUrl}/me";
                Debug.WriteLine($"[GraphProfile] Request URL: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl);
                
                Debug.WriteLine($"[GraphProfile] Response Status: {response.StatusCode}");
                Debug.WriteLine($"[GraphProfile] Response Headers: {response.Headers}");

                var responseContent = await response.Content.ReadAsStringAsync();
                Debug.WriteLine($"[GraphProfile] Response Content: {responseContent}");

                if (response.IsSuccessStatusCode)
                {
                    var userProfile = JsonSerializer.Deserialize<UserProfile>(responseContent, new JsonSerializerOptions
                    {
                        PropertyNameCaseInsensitive = true
                    });
                    
                    Debug.WriteLine($"[GraphProfile] Successfully parsed profile: {userProfile?.DisplayName}");
                    return userProfile;
                }
                else
                {
                    Debug.WriteLine($"[GraphProfile] Error response: {response.StatusCode} - {responseContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GraphProfile] Exception: {ex.Message}");
                Debug.WriteLine($"[GraphProfile] Stack trace: {ex.StackTrace}");
            }

            return null;
        }

        public async Task<BitmapImage?> GetUserPhotoAsync(string accessToken)
        {
            try
            {
                Debug.WriteLine($"[GraphPhoto] Rozpoczynanie pobierania zdjęcia użytkownika...");

                _httpClient.DefaultRequestHeaders.Clear();
                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                var requestUrl = $"{GraphBaseUrl}/me/photo/$value";
                Debug.WriteLine($"[GraphPhoto] Request URL: {requestUrl}");

                var response = await _httpClient.GetAsync(requestUrl);
                
                Debug.WriteLine($"[GraphPhoto] Response Status: {response.StatusCode}");

                if (response.IsSuccessStatusCode)
                {
                    var photoBytes = await response.Content.ReadAsByteArrayAsync();
                    Debug.WriteLine($"[GraphPhoto] Photo size: {photoBytes.Length} bytes");
                    
                    var bitmap = new BitmapImage();
                    bitmap.BeginInit();
                    bitmap.StreamSource = new MemoryStream(photoBytes);
                    bitmap.CacheOption = BitmapCacheOption.OnLoad;
                    bitmap.EndInit();
                    bitmap.Freeze(); // Dla thread safety
                    
                    Debug.WriteLine($"[GraphPhoto] Successfully created bitmap image");
                    return bitmap;
                }
                else
                {
                    var errorContent = await response.Content.ReadAsStringAsync();
                    Debug.WriteLine($"[GraphPhoto] Error response: {response.StatusCode} - {errorContent}");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GraphPhoto] Exception: {ex.Message}");
                // Brak zdjęcia to normalny przypadek
            }

            return null;
        }

        // Metoda testowa do sprawdzenia czy token ma odpowiednie uprawnienia
        public async Task<GraphTestResult> TestGraphAccessAsync(string accessToken)
        {
            var result = new GraphTestResult();
            
            try
            {
                Debug.WriteLine($"[GraphProfile] Sprawdzanie tokenów dla Graph API");
                Debug.WriteLine($"[GraphProfile] Token (pierwsze 50 znaków): {accessToken?.Substring(0, Math.Min(accessToken?.Length ?? 0, 50))}...");
                
                // Dekoduj token żeby sprawdzić scopes
                try
                {
                    var handler = new JwtSecurityTokenHandler();
                    var token = handler.ReadJwtToken(accessToken);
                    var scopes = token.Claims.FirstOrDefault(c => c.Type == "scp")?.Value;
                    var audience = token.Claims.FirstOrDefault(c => c.Type == "aud")?.Value;
                    
                    Debug.WriteLine($"[GraphProfile] Token Audience: {audience}");
                    Debug.WriteLine($"[GraphProfile] Token Scopes: {scopes}");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GraphProfile] Nie można zdekodować tokenu: {ex.Message}");
                }

                _httpClient.DefaultRequestHeaders.Authorization = 
                    new AuthenticationHeaderValue("Bearer", accessToken);

                // Test endpoint /me
                try
                {
                    Debug.WriteLine($"[GraphProfile] Wykonywanie zapytania GET {GraphBaseUrl}/me");
                    var meResponse = await _httpClient.GetAsync($"{GraphBaseUrl}/me");
                    result.MeEndpointStatus = meResponse.StatusCode.ToString();
                    result.CanAccessProfile = meResponse.IsSuccessStatusCode;
                    
                    Debug.WriteLine($"[GraphProfile] /me Response Status: {meResponse.StatusCode}");
                    
                    if (!meResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await meResponse.Content.ReadAsStringAsync();
                        Debug.WriteLine($"[GraphProfile] /me Error Content: {errorContent}");
                        result.ErrorMessage = $"Me endpoint error: {errorContent}";
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GraphProfile] Błąd podczas wywołania /me: {ex.Message}");
                    result.MeEndpointStatus = "Exception";
                    result.ErrorMessage = $"Me endpoint exception: {ex.Message}";
                    result.CanAccessProfile = false;
                }

                // Test endpoint /me/photo/$value
                try
                {
                    Debug.WriteLine($"[GraphProfile] Wykonywanie zapytania GET {GraphBaseUrl}/me/photo/$value");
                    var photoResponse = await _httpClient.GetAsync($"{GraphBaseUrl}/me/photo/$value");
                    result.PhotoEndpointStatus = photoResponse.StatusCode.ToString();
                    result.CanAccessPhoto = photoResponse.IsSuccessStatusCode;
                    
                    Debug.WriteLine($"[GraphProfile] /me/photo/$value Response Status: {photoResponse.StatusCode}");
                    
                    if (!photoResponse.IsSuccessStatusCode)
                    {
                        var errorContent = await photoResponse.Content.ReadAsStringAsync();
                        Debug.WriteLine($"[GraphProfile] /me/photo Error Content: {errorContent}");
                        if (string.IsNullOrEmpty(result.ErrorMessage))
                        {
                            result.ErrorMessage = $"Photo endpoint error: {errorContent}";
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[GraphProfile] Błąd podczas wywołania /me/photo: {ex.Message}");
                    result.PhotoEndpointStatus = "Exception";
                    if (string.IsNullOrEmpty(result.ErrorMessage))
                    {
                        result.ErrorMessage = $"Photo endpoint exception: {ex.Message}";
                    }
                    result.CanAccessPhoto = false;
                }

                Debug.WriteLine($"[GraphProfile] Test zakończony. Profile: {result.CanAccessProfile}, Photo: {result.CanAccessPhoto}");
                return result;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[GraphProfile] Ogólny błąd podczas testowania Graph API: {ex.Message}");
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
                _httpClient?.Dispose();
                _disposed = true;
            }
        }
    }

    public class UserProfile
    {
        public string? Id { get; set; }
        public string? DisplayName { get; set; }
        public string? UserPrincipalName { get; set; }
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