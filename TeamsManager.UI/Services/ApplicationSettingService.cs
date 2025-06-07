using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;

namespace TeamsManager.UI.Services
{
    /// <summary>
    /// Serwis do komunikacji z API ApplicationSettings
    /// Implementuje cache'owanie i obsługę błędów
    /// </summary>
    public class ApplicationSettingService
    {
        private readonly HttpClient _httpClient;
        private readonly ILogger<ApplicationSettingService> _logger;
        private const string ApiPath = "api/v1.0/ApplicationSettings";
        
        // Cache ustawień - odświeżany przy każdym pobraniu wszystkich
        private List<ApplicationSetting> _cachedSettings;
        private DateTime _cacheExpiry = DateTime.MinValue;
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(5);

        public ApplicationSettingService(HttpClient httpClient, ILogger<ApplicationSettingService> logger)
        {
            _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// Pobiera wszystkie aktywne ustawienia aplikacji
        /// </summary>
        public async Task<List<ApplicationSetting>> GetAllSettingsAsync(bool forceRefresh = false)
        {
            try
            {
                // Sprawdzenie cache
                if (!forceRefresh && _cachedSettings != null && DateTime.UtcNow < _cacheExpiry)
                {
                    _logger.LogDebug("Zwracanie ustawień z cache");
                    return new List<ApplicationSetting>(_cachedSettings);
                }

                _logger.LogInformation("Pobieranie wszystkich ustawień aplikacji z API");
                var response = await _httpClient.GetAsync(ApiPath);
                
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<List<ApplicationSetting>>();
                    
                    // Aktualizacja cache
                    _cachedSettings = settings;
                    _cacheExpiry = DateTime.UtcNow.Add(_cacheDuration);
                    
                    _logger.LogInformation("Pobrano {Count} ustawień aplikacji", settings?.Count ?? 0);
                    return settings ?? new List<ApplicationSetting>();
                }
                
                _logger.LogWarning("Błąd pobierania ustawień. Status: {StatusCode}", response.StatusCode);
                return new List<ApplicationSetting>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ustawień aplikacji");
                
                // Zwróć cache jeśli dostępny
                if (_cachedSettings != null)
                {
                    _logger.LogInformation("Zwracanie ustawień z cache po błędzie");
                    return new List<ApplicationSetting>(_cachedSettings);
                }
                
                return new List<ApplicationSetting>();
            }
        }

        /// <summary>
        /// Pobiera ustawienie po kluczu
        /// </summary>
        public async Task<ApplicationSetting> GetSettingByKeyAsync(string key)
        {
            try
            {
                _logger.LogInformation("Pobieranie ustawienia o kluczu: {Key}", key);
                var encodedKey = Uri.EscapeDataString(key);
                var response = await _httpClient.GetAsync($"{ApiPath}/key/{encodedKey}");
                
                if (response.IsSuccessStatusCode)
                {
                    return await response.Content.ReadFromJsonAsync<ApplicationSetting>();
                }
                
                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    _logger.LogInformation("Ustawienie o kluczu {Key} nie zostało znalezione", key);
                }
                else
                {
                    _logger.LogWarning("Błąd pobierania ustawienia {Key}. Status: {StatusCode}", key, response.StatusCode);
                }
                
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ustawienia o kluczu: {Key}", key);
                return null;
            }
        }

        /// <summary>
        /// Pobiera ustawienia według kategorii
        /// </summary>
        public async Task<List<ApplicationSetting>> GetSettingsByCategoryAsync(string category)
        {
            try
            {
                // Najpierw spróbuj z cache
                if (_cachedSettings != null && DateTime.UtcNow < _cacheExpiry)
                {
                    return _cachedSettings.Where(s => s.Category.Equals(category, StringComparison.OrdinalIgnoreCase)).ToList();
                }

                _logger.LogInformation("Pobieranie ustawień dla kategorii: {Category}", category);
                var encodedCategory = Uri.EscapeDataString(category);
                var response = await _httpClient.GetAsync($"{ApiPath}/category/{encodedCategory}");
                
                if (response.IsSuccessStatusCode)
                {
                    var settings = await response.Content.ReadFromJsonAsync<List<ApplicationSetting>>();
                    return settings ?? new List<ApplicationSetting>();
                }
                
                _logger.LogWarning("Błąd pobierania ustawień dla kategorii {Category}. Status: {StatusCode}", category, response.StatusCode);
                return new List<ApplicationSetting>();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania ustawień dla kategorii: {Category}", category);
                return new List<ApplicationSetting>();
            }
        }

        /// <summary>
        /// Zapisuje lub aktualizuje ustawienie
        /// </summary>
        public async Task<bool> SaveSettingAsync(ApplicationSetting setting)
        {
            try
            {
                _logger.LogInformation("Zapisywanie ustawienia: {Key}", setting.Key);
                
                var dto = new
                {
                    Key = setting.Key,
                    Value = setting.Value,
                    Type = setting.Type,
                    Description = setting.Description,
                    Category = setting.Category
                };
                
                var response = await _httpClient.PostAsJsonAsync(ApiPath, dto);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Ustawienie {Key} zapisane pomyślnie", setting.Key);
                    
                    // Wyczyść cache
                    _cacheExpiry = DateTime.MinValue;
                    
                    return true;
                }
                
                _logger.LogWarning("Błąd zapisywania ustawienia {Key}. Status: {StatusCode}", setting.Key, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania ustawienia: {Key}", setting.Key);
                return false;
            }
        }

        /// <summary>
        /// Aktualizuje istniejące ustawienie
        /// </summary>
        public async Task<bool> UpdateSettingAsync(ApplicationSetting setting)
        {
            try
            {
                _logger.LogInformation("Aktualizacja ustawienia ID: {Id}, Key: {Key}", setting.Id, setting.Key);
                
                var dto = new
                {
                    Key = setting.Key,
                    Value = setting.Value,
                    Description = setting.Description,
                    Type = setting.Type,
                    Category = setting.Category,
                    IsRequired = setting.IsRequired,
                    IsVisible = setting.IsVisible,
                    DefaultValue = setting.DefaultValue,
                    ValidationPattern = setting.ValidationPattern,
                    ValidationMessage = setting.ValidationMessage,
                    DisplayOrder = setting.DisplayOrder,
                    IsActive = setting.IsActive
                };
                
                var response = await _httpClient.PutAsJsonAsync($"{ApiPath}/{setting.Id}", dto);
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Ustawienie {Key} zaktualizowane pomyślnie", setting.Key);
                    
                    // Wyczyść cache
                    _cacheExpiry = DateTime.MinValue;
                    
                    return true;
                }
                
                _logger.LogWarning("Błąd aktualizacji ustawienia {Key}. Status: {StatusCode}", setting.Key, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas aktualizacji ustawienia: {Key}", setting.Key);
                return false;
            }
        }

        /// <summary>
        /// Usuwa (dezaktywuje) ustawienie
        /// </summary>
        public async Task<bool> DeleteSettingAsync(string key)
        {
            try
            {
                _logger.LogInformation("Usuwanie ustawienia o kluczu: {Key}", key);
                var encodedKey = Uri.EscapeDataString(key);
                var response = await _httpClient.DeleteAsync($"{ApiPath}/key/{encodedKey}");
                
                if (response.IsSuccessStatusCode)
                {
                    _logger.LogInformation("Ustawienie {Key} usunięte pomyślnie", key);
                    
                    // Wyczyść cache
                    _cacheExpiry = DateTime.MinValue;
                    
                    return true;
                }
                
                _logger.LogWarning("Błąd usuwania ustawienia {Key}. Status: {StatusCode}", key, response.StatusCode);
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania ustawienia: {Key}", key);
                return false;
            }
        }

        /// <summary>
        /// Weryfikuje połączenie z API
        /// </summary>
        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                var response = await _httpClient.GetAsync($"{ApiPath}?$top=1");
                return response.IsSuccessStatusCode;
            }
            catch
            {
                return false;
            }
        }
    }
} 