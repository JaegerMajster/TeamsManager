using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels;
using TeamsManager.UI.Services.Configuration;

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
            try
            {
                var configManager = serviceProvider.GetRequiredService<IConfigurationManagerV2>();
                var configInitializer = serviceProvider.GetRequiredService<ConfigurationInitializer>();
                
                // Sprawdź czy konfiguracja wymaga inicjalizacji
                var requiresInit = await configInitializer.RequiresInitializationAsync();
                if (requiresInit)
                {
                    return false; // Wymaga konfiguracji
                }

                // Sprawdź kompletność konfiguracji
                var azureConfig = await configManager.LoadAzureAdConfigurationAsync();
                if (azureConfig == null || 
                    string.IsNullOrWhiteSpace(azureConfig.Ui.ClientId) ||
                    string.IsNullOrWhiteSpace(azureConfig.Api.ClientId) ||
                    string.IsNullOrWhiteSpace(azureConfig.TenantId) ||
                    string.IsNullOrWhiteSpace(azureConfig.Api.ClientSecret) ||
                    string.IsNullOrWhiteSpace(azureConfig.Api.Audience))
                {
                    return false; // Brakuje konfiguracji Azure AD
                }

                return true; // Konfiguracja jest kompletna
            }
            catch (Exception)
            {
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