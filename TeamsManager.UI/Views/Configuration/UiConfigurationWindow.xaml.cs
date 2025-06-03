using System.Windows;
using TeamsManager.UI.ViewModels.Configuration;

namespace TeamsManager.UI.Views.Configuration
{
    /// <summary>
    /// Logika interakcji dla UiConfigurationWindow.xaml
    /// </summary>
    public partial class UiConfigurationWindow : Window
    {
        private readonly UiConfigurationViewModel _viewModel;

        public UiConfigurationWindow(string tenantId, string apiScope)
        {
            InitializeComponent();
            _viewModel = new UiConfigurationViewModel(tenantId, apiScope);
            DataContext = _viewModel;

            _viewModel.RequestClose += (sender, e) =>
            {
                DialogResult = e;
                Close();
            };

            _viewModel.RequestNavigateBack += (sender, e) =>
            {
                // Powrót do poprzedniego okna
                var apiConfigWindow = new ApiConfigurationWindow();
                apiConfigWindow.Show();
                Close();
            };
        }
    }
}