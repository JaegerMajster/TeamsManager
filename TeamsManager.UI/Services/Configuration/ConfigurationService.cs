using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;

namespace TeamsManager.UI.Services.Configuration
{
    /// <summary>
    /// Implementacja serwisu konfiguracji dla UI
    /// Wykorzystuje istniejący system V2.0 z szyfrowaniem
    /// </summary>
    public class ConfigurationService : IConfigurationService
    {
        private readonly IConfigurationManagerV2 _configurationManagerV2;
        private readonly ILogger<ConfigurationService> _logger;

        public event EventHandler<Core.Abstractions.Services.ConfigurationChangedEventArgs>? ConfigurationChanged;

        public ConfigurationService(
            IConfigurationManagerV2 configurationManagerV2,
            ILogger<ConfigurationService> logger)
        {
            _configurationManagerV2 = configurationManagerV2 ?? throw new ArgumentNullException(nameof(configurationManagerV2));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            // Przekieruj eventy z V2 do nowego interfejsu
            _configurationManagerV2.ConfigurationChanged += OnV2ConfigurationChanged;
        }

        public async Task<T?> GetConfigurationAsync<T>(string configurationName) where T : class
        {
            try
            {
                _logger.LogInformation("Pobieranie konfiguracji {ConfigurationName} typu {Type}", configurationName, typeof(T).Name);

                // Używaj tylko istniejące metody z V2
                return await _configurationManagerV2.GetConfigurationAsync<T>(configurationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas pobierania konfiguracji {ConfigurationName}", configurationName);
                throw;
            }
        }

        public async Task SaveConfigurationAsync<T>(string configurationName, T configuration) where T : class
        {
            try
            {
                _logger.LogInformation("Zapisywanie konfiguracji {ConfigurationName} typu {Type}", configurationName, typeof(T).Name);

                if (configuration == null)
                    throw new ArgumentNullException(nameof(configuration));

                // Używaj tylko istniejące metody z V2
                await _configurationManagerV2.SaveConfigurationAsync(configurationName, configuration);

                _logger.LogInformation("Konfiguracja {ConfigurationName} zapisana pomyślnie", configurationName);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zapisywania konfiguracji {ConfigurationName}", configurationName);
                throw;
            }
        }

        public async Task DeleteConfigurationAsync(string configurationName)
        {
            try
            {
                _logger.LogInformation("Usuwanie konfiguracji {ConfigurationName}", configurationName);

                // Dla uproszczenia - nie implementujemy usuwania w pierwszej wersji
                _logger.LogWarning("Usuwanie konfiguracji nie jest jeszcze implementowane");
                await Task.CompletedTask;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas usuwania konfiguracji {ConfigurationName}", configurationName);
                throw;
            }
        }

        public async Task<bool> ConfigurationExistsAsync(string configurationName)
        {
            try
            {
                var config = await GetConfigurationAsync<object>(configurationName);
                return config != null;
            }
            catch
            {
                return false;
            }
        }

        private void OnV2ConfigurationChanged(object? sender, ConfigurationChangedEventArgs e)
        {
            // Przekonwertuj event z V2 na nowy format
            var newEventArgs = new Core.Abstractions.Services.ConfigurationChangedEventArgs(
                e.ConfigurationName, 
                e.ConfigurationType);
            
            ConfigurationChanged?.Invoke(this, newEventArgs);
        }

        public void Dispose()
        {
            if (_configurationManagerV2 != null)
            {
                _configurationManagerV2.ConfigurationChanged -= OnV2ConfigurationChanged;
            }
        }
    }
} 