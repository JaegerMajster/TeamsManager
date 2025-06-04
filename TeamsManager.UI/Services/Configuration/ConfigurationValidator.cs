using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TeamsManager.UI.Models.Configuration;

namespace TeamsManager.UI.Services.Configuration
{
    /// <summary>
    /// Serwis do walidacji konfiguracji aplikacji
    /// </summary>
    public class ConfigurationValidator
    {
        private readonly ConfigurationManager _configManager;

        public ConfigurationValidator(ConfigurationManager configManager)
        {
            _configManager = configManager ?? throw new ArgumentNullException(nameof(configManager));
        }

        /// <summary>
        /// Waliduje pełną konfigurację aplikacji
        /// </summary>
        public async Task<ConfigurationValidationResult> ValidateFullConfigurationAsync()
        {
            var result = new ConfigurationValidationResult
            {
                IsValid = true,
                Errors = new List<string>()
            };

            try
            {
                // Sprawdź czy pliki konfiguracyjne istnieją
                if (!_configManager.ConfigurationExists())
                {
                    result.IsValid = false;
                    result.Status = ConfigurationStatus.Missing;
                    result.DetailedMessage = "Brak plików konfiguracyjnych. Wymagana jest początkowa konfiguracja aplikacji.";
                    result.Errors.Add("Nie znaleziono plików konfiguracyjnych");
                    return result;
                }

                // Waliduj konfigurację OAuth
                var oauthResult = await ValidateOAuthConfigurationAsync();
                if (!oauthResult.IsValid)
                {
                    result.IsValid = false;
                    result.Status = oauthResult.Status;
                    result.Errors.AddRange(oauthResult.Errors);
                }

                // Waliduj konfigurację API (potrzebną dla przepływu OBO)
                var apiResult = await ValidateApiConfigurationAsync();
                if (!apiResult.IsValid)
                {
                    result.IsValid = false;
                    result.Status = apiResult.Status;
                    result.Errors.AddRange(apiResult.Errors);
                }

                // Jeśli wszystko OK
                if (result.IsValid)
                {
                    result.Status = ConfigurationStatus.Valid;
                    result.DetailedMessage = "Konfiguracja jest prawidłowa";
                }
                else if (result.Status == ConfigurationStatus.Valid)
                {
                    // Jeśli są błędy ale status nie został ustawiony
                    result.Status = ConfigurationStatus.Invalid;
                    result.DetailedMessage = "Konfiguracja zawiera błędy";
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Status = ConfigurationStatus.Unknown;
                result.DetailedMessage = $"Nieoczekiwany błąd podczas walidacji: {ex.Message}";
                result.Errors.Add($"Błąd walidacji: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Waliduje konfigurację OAuth
        /// </summary>
        private async Task<ConfigurationValidationResult> ValidateOAuthConfigurationAsync()
        {
            var result = new ConfigurationValidationResult
            {
                IsValid = true,
                Errors = new List<string>()
            };

            try
            {
                var config = await _configManager.LoadOAuthConfigAsync();

                if (config == null)
                {
                    result.IsValid = false;
                    result.Status = ConfigurationStatus.Missing;
                    result.Errors.Add("Brak konfiguracji OAuth");
                    return result;
                }

                // Waliduj Scopes
                if (config.Scopes == null || config.Scopes.Count == 0)
                {
                    result.Errors.Add("Brak Scopes w konfiguracji OAuth");
                }

                // Waliduj AzureAd sekcję
                if (config.AzureAd == null)
                {
                    result.Errors.Add("Brak sekcji AzureAd w konfiguracji OAuth");
                    result.IsValid = false;
                    result.Status = ConfigurationStatus.Invalid;
                    return result;
                }

                // Waliduj TenantId
                if (string.IsNullOrWhiteSpace(config.AzureAd.TenantId))
                {
                    result.Errors.Add("Brak Tenant ID w konfiguracji OAuth");
                }
                else if (!IsValidGuid(config.AzureAd.TenantId) && config.AzureAd.TenantId != "common" && config.AzureAd.TenantId != "organizations")
                {
                    result.Errors.Add("Nieprawidłowy format Tenant ID");
                }

                // Waliduj ClientId
                if (string.IsNullOrWhiteSpace(config.AzureAd.ClientId))
                {
                    result.Errors.Add("Brak Client ID aplikacji UI");
                }
                else if (!IsValidGuid(config.AzureAd.ClientId))
                {
                    result.Errors.Add("Nieprawidłowy format Client ID aplikacji UI");
                }

                // Waliduj ApiScope
                if (string.IsNullOrWhiteSpace(config.AzureAd.ApiScope))
                {
                    result.Errors.Add("Brak API Scope");
                }
                else if (!IsValidApiScope(config.AzureAd.ApiScope))
                {
                    result.Errors.Add("Nieprawidłowy format API Scope (oczekiwany: api://[guid]/scope)");
                }

                // Waliduj Instance
                if (string.IsNullOrWhiteSpace(config.AzureAd.Instance))
                {
                    result.Errors.Add("Brak Instance URL");
                }
                else if (!IsValidUri(config.AzureAd.Instance))
                {
                    result.Errors.Add("Nieprawidłowy format Instance URL");
                }

                // Waliduj RedirectUri
                if (string.IsNullOrWhiteSpace(config.AzureAd.RedirectUri))
                {
                    result.Errors.Add("Brak Redirect URI");
                }

                // Waliduj ApiBaseUrl
                if (string.IsNullOrWhiteSpace(config.AzureAd.ApiBaseUrl))
                {
                    result.Errors.Add("Brak API Base URL");
                }
                else if (!IsValidUri(config.AzureAd.ApiBaseUrl))
                {
                    result.Errors.Add("Nieprawidłowy format API Base URL");
                }

                if (result.Errors.Count > 0)
                {
                    result.IsValid = false;
                    result.Status = ConfigurationStatus.Invalid;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Status = ConfigurationStatus.Unknown;
                result.Errors.Add($"Błąd podczas walidacji OAuth: {ex.Message}");
            }

            return result;
        }

        /// <summary>
        /// Waliduje konfigurację API (potrzebną dla przepływu OBO)
        /// </summary>
        private async Task<ConfigurationValidationResult> ValidateApiConfigurationAsync()
        {
            var result = new ConfigurationValidationResult
            {
                IsValid = true,
                Errors = new List<string>()
            };

            try
            {
                var config = await _configManager.LoadApiConfigAsync();

                if (config == null)
                {
                    result.IsValid = false;
                    result.Status = ConfigurationStatus.Missing;
                    result.Errors.Add("Brak konfiguracji API");
                    return result;
                }

                // Waliduj ApiScope
                if (string.IsNullOrWhiteSpace(config.ApiScope))
                {
                    result.Errors.Add("Brak API Scope");
                }
                else if (!IsValidApiScope(config.ApiScope))
                {
                    result.Errors.Add("Nieprawidłowy format API Scope (oczekiwany: api://[guid]/scope)");
                }

                // Waliduj ApiAudience
                if (string.IsNullOrWhiteSpace(config.ApiAudience))
                {
                    result.Errors.Add("Brak API Audience");
                }
                else if (!IsValidApiAudience(config.ApiAudience))
                {
                    result.Errors.Add("Nieprawidłowy format API Audience (oczekiwany: api://[guid])");
                }

                // Waliduj ApiBaseUrl
                if (string.IsNullOrWhiteSpace(config.ApiBaseUrl))
                {
                    result.Errors.Add("Brak API Base URL");
                }
                else if (!IsValidUri(config.ApiBaseUrl))
                {
                    result.Errors.Add("Nieprawidłowy format API Base URL");
                }

                if (result.Errors.Count > 0)
                {
                    result.IsValid = false;
                    result.Status = ConfigurationStatus.Invalid;
                }
            }
            catch (Exception ex)
            {
                result.IsValid = false;
                result.Status = ConfigurationStatus.Unknown;
                result.Errors.Add($"Błąd podczas walidacji API: {ex.Message}");
            }

            return result;
        }

        #region Helper Methods

        /// <summary>
        /// Sprawdza czy string jest prawidłowym GUID
        /// </summary>
        private bool IsValidGuid(string value)
        {
            return Guid.TryParse(value, out _);
        }

        /// <summary>
        /// Sprawdza czy string jest prawidłowym URI
        /// </summary>
        private bool IsValidUri(string value)
        {
            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Sprawdza czy API Scope ma prawidłowy format
        /// </summary>
        private bool IsValidApiScope(string scope)
        {
            // Format: api://[guid]/scope_name
            if (string.IsNullOrWhiteSpace(scope))
                return false;

            if (!scope.StartsWith("api://"))
                return false;

            var parts = scope.Split('/');
            if (parts.Length < 3)
                return false;

            // Sprawdź czy druga część (po api://) to GUID
            var guidPart = parts[2]; // api://[guid]/scope
            return IsValidGuid(guidPart) || !string.IsNullOrWhiteSpace(guidPart);
        }

        /// <summary>
        /// Sprawdza czy API Audience ma prawidłowy format
        /// </summary>
        private bool IsValidApiAudience(string audience)
        {
            // Format: api://[guid]
            if (string.IsNullOrWhiteSpace(audience))
                return false;

            if (!audience.StartsWith("api://"))
                return false;

            var guidPart = audience.Substring(6); // Usuń "api://"
            return IsValidGuid(guidPart) || !string.IsNullOrWhiteSpace(guidPart);
        }

        #endregion
    }
}