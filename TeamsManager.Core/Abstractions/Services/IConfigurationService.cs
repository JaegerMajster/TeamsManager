using System;
using System.Threading.Tasks;

namespace TeamsManager.Core.Abstractions.Services
{
    /// <summary>
    /// Interfejs serwisu konfiguracji zgodny z Clean Architecture
    /// Definiuje kontrakt dla zarządzania konfiguracją aplikacji
    /// </summary>
    public interface IConfigurationService
    {
        /// <summary>
        /// Pobiera konfigurację określonego typu
        /// </summary>
        /// <typeparam name="T">Typ konfiguracji</typeparam>
        /// <param name="configurationName">Nazwa konfiguracji</param>
        /// <returns>Obiekt konfiguracji lub null jeśli nie istnieje</returns>
        Task<T?> GetConfigurationAsync<T>(string configurationName) where T : class;

        /// <summary>
        /// Zapisuje konfigurację określonego typu
        /// </summary>
        /// <typeparam name="T">Typ konfiguracji</typeparam>
        /// <param name="configurationName">Nazwa konfiguracji</param>
        /// <param name="configuration">Obiekt konfiguracji do zapisu</param>
        Task SaveConfigurationAsync<T>(string configurationName, T configuration) where T : class;

        /// <summary>
        /// Usuwa konfigurację
        /// </summary>
        /// <param name="configurationName">Nazwa konfiguracji do usunięcia</param>
        Task DeleteConfigurationAsync(string configurationName);

        /// <summary>
        /// Sprawdza czy konfiguracja istnieje
        /// </summary>
        /// <param name="configurationName">Nazwa konfiguracji</param>
        /// <returns>True jeśli konfiguracja istnieje</returns>
        Task<bool> ConfigurationExistsAsync(string configurationName);

        /// <summary>
        /// Event wywoływany gdy konfiguracja się zmienia
        /// </summary>
        event EventHandler<ConfigurationChangedEventArgs>? ConfigurationChanged;
    }

    /// <summary>
    /// Argumenty zdarzenia zmiany konfiguracji
    /// </summary>
    public class ConfigurationChangedEventArgs : EventArgs
    {
        public string ConfigurationName { get; }
        public Type ConfigurationType { get; }

        public ConfigurationChangedEventArgs(string configurationName, Type configurationType)
        {
            ConfigurationName = configurationName;
            ConfigurationType = configurationType;
        }
    }
} 