using System;
using System.Threading.Tasks;
using TeamsManager.UI.Models.Configuration;

namespace TeamsManager.UI.Services.Configuration
{
    public interface IConfigurationManagerV2
    {
        // Metody specyficzne dla poszczególnych konfiguracji
        Task<LoginSettingsConfiguration?> LoadLoginSettingsAsync();
        Task SaveLoginSettingsAsync(LoginSettingsConfiguration settings);
        Task ClearLoginSettingsAsync();
        Task<ApplicationConfiguration?> LoadApplicationConfigurationAsync();
        Task SaveApplicationConfigurationAsync(ApplicationConfiguration config);
        Task<AzureAdConfiguration?> LoadAzureAdConfigurationAsync();
        Task SaveAzureAdConfigurationAsync(AzureAdConfiguration config);
        
        // Metody ogólne
        Task<T?> GetConfigurationAsync<T>(string configName) where T : class;
        Task SaveConfigurationAsync<T>(string configName, T configuration) where T : class;
        Task<bool> ValidateConfigurationAsync();
        Task BackupConfigurationAsync();
        Task RestoreConfigurationAsync(string backupPath);
        Task ReencryptForCurrentUserAsync(string configName);
        
        // Zdarzenia
        event EventHandler<ConfigurationChangedEventArgs> ConfigurationChanged;
    }
    
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