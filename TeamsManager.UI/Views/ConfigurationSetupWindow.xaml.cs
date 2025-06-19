using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Configuration;
using System.Collections.Generic;

namespace TeamsManager.UI.Views
{
    public partial class ConfigurationSetupWindow : Window
    {
        private readonly ConfigurationSetupViewModel _viewModel;
        private readonly ILogger<ConfigurationSetupWindow> _logger;

        public ConfigurationSetupWindow(
            IConfigurationManagerV2 configManager,
            ConfigurationInitializer configInitializer,
            ILogger<ConfigurationSetupWindow> logger)
        {
            InitializeComponent();
            
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            
            // Utwórz ViewModel z zależnościami
            var viewModelLogger = App.ServiceProvider.GetRequiredService<ILogger<ConfigurationSetupViewModel>>();
            _viewModel = new ConfigurationSetupViewModel(configManager, configInitializer, viewModelLogger);
            
            DataContext = _viewModel;
            
            // Subskrybuj zdarzenia
            _viewModel.RequestClose += OnRequestClose;
            
            _logger.LogInformation("ConfigurationSetupWindow zainicjalizowane");
        }

        private void OnRequestClose()
        {
            try
            {
                // Sprawdź czy są niezapisane zmiany
                if (_viewModel.HasChanges)
                {
                    var result = MessageBox.Show(
                        "Masz niezapisane zmiany. Czy chcesz je zapisać przed zamknięciem?",
                        "Niezapisane zmiany",
                        MessageBoxButton.YesNoCancel,
                        MessageBoxImage.Question);

                    if (result == MessageBoxResult.Yes)
                    {
                        // Zapisz i zamknij
                        if (_viewModel.SaveCommand.CanExecute(null))
                        {
                            _viewModel.SaveCommand.Execute(null);
                            // Okno zostanie zamknięte po zapisaniu
                            return;
                        }
                    }
                    else if (result == MessageBoxResult.Cancel)
                    {
                        // Anuluj zamknięcie
                        return;
                    }
                    // Jeśli No, po prostu zamknij bez zapisywania
                }

                DialogResult = true;
                Close();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zamykania okna konfiguracji");
                MessageBox.Show($"Błąd podczas zamykania: {ex.Message}", 
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            try
            {
                // Wyczyść subskrypcje
                if (_viewModel != null)
                {
                    _viewModel.RequestClose -= OnRequestClose;
                }
                
                _logger.LogInformation("ConfigurationSetupWindow zamknięte");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas zamykania ConfigurationSetupWindow");
            }
            finally
            {
                base.OnClosed(e);
            }
        }

        // Metoda pomocnicza do sprawdzania konfiguracji przy starcie aplikacji
        public static async Task<bool> CheckConfigurationAsync(IServiceProvider serviceProvider)
        {
            ILogger<ConfigurationSetupWindow>? logger = null;
            try
            {
                logger = serviceProvider.GetRequiredService<ILogger<ConfigurationSetupWindow>>();
                logger.LogInformation("🔍 SPRAWDZANIE KONFIGURACJI - rozpoczęcie");
                
                var configManager = serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                var configInitializer = serviceProvider.GetRequiredService<ConfigurationInitializer>();
                
                logger.LogInformation("🔍 Sprawdzam czy konfiguracja wymaga inicjalizacji...");
                
                // Sprawdź czy konfiguracja wymaga inicjalizacji
                var requiresInit = await configInitializer.RequiresInitializationAsync();
                logger.LogInformation($"🔍 RequiresInitialization: {requiresInit}");
                
                if (requiresInit)
                {
                    logger.LogWarning("❌ KONFIGURACJA NIEKOMPLETNA - wymaga inicjalizacji");
                    return false; // Wymaga konfiguracji
                }

                logger.LogInformation("🔍 Sprawdzam konfigurację Azure AD...");
                
                // Sprawdź kompletność konfiguracji
                var azureConfig = await configManager.LoadAzureAdConfigurationAsync();
                if (azureConfig == null)
                {
                    logger.LogWarning("❌ KONFIGURACJA NIEKOMPLETNA - brak konfiguracji Azure AD");
                    return false;
                }
                
                // Szczegółowa walidacja pól
                var missingFields = new List<string>();
                
                if (string.IsNullOrWhiteSpace(azureConfig.Ui.ClientId))
                    missingFields.Add("UI.ClientId");
                if (string.IsNullOrWhiteSpace(azureConfig.Api.ClientId))
                    missingFields.Add("API.ClientId");
                if (string.IsNullOrWhiteSpace(azureConfig.TenantId))
                    missingFields.Add("TenantId");
                if (string.IsNullOrWhiteSpace(azureConfig.Api.ClientSecret))
                    missingFields.Add("API.ClientSecret");
                if (string.IsNullOrWhiteSpace(azureConfig.Api.Audience))
                    missingFields.Add("API.Audience");
                
                if (missingFields.Count > 0)
                {
                    logger.LogWarning($"❌ KONFIGURACJA NIEKOMPLETNA - brakujące pola: {string.Join(", ", missingFields)}");
                    return false; // Brakuje konfiguracji Azure AD
                }

                logger.LogInformation("✅ KONFIGURACJA KOMPLETNA - wszystkie wymagane pola są wypełnione");
                logger.LogInformation($"✅ Tenant ID: {azureConfig.TenantId}");
                logger.LogInformation($"✅ UI Client ID: {azureConfig.Ui.ClientId}");
                logger.LogInformation($"✅ API Client ID: {azureConfig.Api.ClientId}");
                logger.LogInformation($"✅ API Audience: {azureConfig.Api.Audience}");
                logger.LogInformation($"✅ Client Secret: {(string.IsNullOrEmpty(azureConfig.Api.ClientSecret) ? "BRAK" : "USTAWIONY")}");
                
                return true; // Konfiguracja jest kompletna
            }
            catch (Exception ex)
            {
                logger?.LogError(ex, "❌ BŁĄD podczas sprawdzania konfiguracji");
                return false; // Błąd = wymaga konfiguracji
            }
        }

        // Metoda do pokazania okna konfiguracji
        public static bool? ShowConfigurationDialog(Window owner, IServiceProvider serviceProvider)
        {
            try
            {
                var configManager = serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                var configInitializer = serviceProvider.GetRequiredService<ConfigurationInitializer>();
                var logger = serviceProvider.GetRequiredService<ILogger<ConfigurationSetupWindow>>();

                var window = new ConfigurationSetupWindow(configManager, configInitializer, logger)
                {
                    Owner = owner
                };

                return window.ShowDialog();
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetRequiredService<ILogger<ConfigurationSetupWindow>>();
                logger.LogError(ex, "Błąd podczas otwierania okna konfiguracji");
                
                MessageBox.Show($"Błąd podczas otwierania okna konfiguracji: {ex.Message}",
                              "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
                return false;
            }
        }
    }
} 