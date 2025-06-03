using System.Windows;
using TeamsManager.UI.ViewModels.Configuration;

namespace TeamsManager.UI.Views.Configuration
{
    /// <summary>
    /// Logika interakcji dla TestConnectionWindow.xaml
    /// </summary>
    public partial class TestConnectionWindow : Window
    {
        private readonly TestConnectionViewModel _viewModel;

        public TestConnectionWindow()
        {
            InitializeComponent();
            _viewModel = new TestConnectionViewModel();
            DataContext = _viewModel;

            _viewModel.RequestClose += (sender, e) =>
            {
                DialogResult = e;
                Close();
            };

            _viewModel.RequestNavigateBack += (sender, e) =>
            {
                // Powrót do poprzedniego okna
                // Musimy przekazać dane z powrotem
                var configManager = new Services.Configuration.ConfigurationManager();
                var oauthConfig = configManager.LoadOAuthConfigAsync().Result;
                if (oauthConfig != null)
                {
                    var uiConfigWindow = new UiConfigurationWindow(oauthConfig.TenantId, oauthConfig.ApiScope);
                    uiConfigWindow.Show();
                    Close();
                }
            };

            _viewModel.RequestRestart += (sender, e) =>
            {
                // Restart aplikacji
                System.Diagnostics.Process.Start(Application.ResourceAssembly.Location);
                Application.Current.Shutdown();
            };
        }
    }
}