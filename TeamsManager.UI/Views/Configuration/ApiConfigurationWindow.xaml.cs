using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Navigation;
using TeamsManager.UI.ViewModels.Configuration;

namespace TeamsManager.UI.Views.Configuration
{
    /// <summary>
    /// Logika interakcji dla ApiConfigurationWindow.xaml
    /// </summary>
    public partial class ApiConfigurationWindow : Window
    {
        private readonly ApiConfigurationViewModel _viewModel;

        public ApiConfigurationWindow()
        {
            InitializeComponent();
            _viewModel = new ApiConfigurationViewModel();
            DataContext = _viewModel;

            _viewModel.RequestClose += (sender, e) =>
            {
                DialogResult = e;
                Close();
            };
        }

        private void ApiSecretBox_PasswordChanged(object sender, RoutedEventArgs e)
        {
            if (_viewModel != null && sender is PasswordBox passwordBox)
            {
                _viewModel.ApiClientSecret = passwordBox.Password;
            }
        }

        private void Hyperlink_RequestNavigate(object sender, RequestNavigateEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = e.Uri.AbsoluteUri,
                UseShellExecute = true
            });
            e.Handled = true;
        }
    }
}