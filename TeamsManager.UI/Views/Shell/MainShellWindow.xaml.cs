using System;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels.Shell;

namespace TeamsManager.UI.Views.Shell
{
    public partial class MainShellWindow : Window
    {
        private readonly ILogger<MainShellWindow> _logger;
        private readonly MainShellViewModel _viewModel;
        private LoginWindow? _loginWindow;

        public MainShellWindow(MainShellViewModel viewModel, ILogger<MainShellWindow> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            
            InitializeComponent();
            
            DataContext = _viewModel;
            
            // Dodaj obsługę kliknięcia na przycisk profilu
            UserProfileButton.Click += UserProfileButton_Click;
            
            // Sprawdź auto-login po załadowaniu okna
            Loaded += async (s, e) => await CheckAutoLoginAsync();
            
            _logger.LogDebug("MainShellWindow utworzone pomyślnie");
        }

        private void UserProfileButton_Click(object sender, RoutedEventArgs e)
        {
            // Otwórz context menu przy przycisku
            var button = sender as System.Windows.Controls.Button;
            if (button?.ContextMenu != null)
            {
                button.ContextMenu.PlacementTarget = button;
                button.ContextMenu.IsOpen = true;
            }
        }

        protected override void OnContentRendered(EventArgs e)
        {
            base.OnContentRendered(e);
            _logger.LogInformation("MainShellWindow wyrenderowane");
        }

        private async Task CheckAutoLoginAsync()
        {
            // Sprawdź auto-login
            var autoLoginSuccess = await _viewModel.CheckAutoLoginAsync();
            
            if (!autoLoginSuccess)
            {
                // Pokaż okno logowania
                ShowLoginWindow();
            }
        }

        private void ShowLoginWindow()
        {
            try
            {
                var serviceProvider = App.ServiceProvider;
                
                // Pokaż overlay
                _viewModel.IsDialogOpen = true;
                
                _loginWindow = serviceProvider.GetRequiredService<LoginWindow>();
                
                if (_loginWindow.ShowDialog() == true)
                {
                    // Logowanie zakończone sukcesem
                    _logger.LogInformation("Użytkownik zalogowany pomyślnie");
                    
                    // Odśwież informacje o użytkowniku
                    _viewModel.LoadUserInfo();
                    
                    // Załaduj szczegółowy profil z Microsoft Graph
                    _ = _viewModel.LoadDetailedUserProfileAsync();
                }
                else
                {
                    // Użytkownik anulował logowanie
                    _logger.LogWarning("Logowanie anulowane przez użytkownika");
                    
                    // Zamknij aplikację
                    System.Windows.Application.Current.Shutdown();
                }
            }
            finally
            {
                // Ukryj overlay
                _viewModel.IsDialogOpen = false;
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            _logger.LogInformation("MainShellWindow zamknięte");
                            System.Windows.Application.Current.Shutdown();
        }
    }
} 