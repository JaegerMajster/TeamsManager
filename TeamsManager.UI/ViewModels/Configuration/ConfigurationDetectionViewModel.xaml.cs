using System.Windows;
using TeamsManager.UI.Models.Configuration;
using TeamsManager.UI.ViewModels.Configuration;

namespace TeamsManager.UI.Views.Configuration
{
    /// <summary>
    /// Logika interakcji dla ConfigurationDetectionWindow.xaml
    /// </summary>
    public partial class ConfigurationDetectionWindow : Window
    {
        public ConfigurationDetectionWindow(ConfigurationValidationResult validationResult)
        {
            InitializeComponent();

            // Ustaw ViewModel
            var viewModel = new ConfigurationDetectionViewModel(this, validationResult);
            DataContext = viewModel;
        }
    }
}