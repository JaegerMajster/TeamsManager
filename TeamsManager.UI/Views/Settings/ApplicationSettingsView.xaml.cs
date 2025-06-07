using System;
using System.Windows;
using UserControl = System.Windows.Controls.UserControl;
using TeamsManager.UI.ViewModels.Settings;

namespace TeamsManager.UI.Views.Settings
{
    /// <summary>
    /// Interaction logic for ApplicationSettingsView.xaml
    /// </summary>
    public partial class ApplicationSettingsView : UserControl
    {
        private readonly ApplicationSettingsViewModel _viewModel;

        public ApplicationSettingsView(ApplicationSettingsViewModel viewModel)
        {
            InitializeComponent();
            
            _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
            DataContext = _viewModel;
            
            // Inicjalizacja asynchroniczna
            Loaded += OnLoaded;
        }

        private async void OnLoaded(object sender, RoutedEventArgs e)
        {
            // Załaduj dane tylko przy pierwszym wyświetleniu
            if (_viewModel.TotalSettings == 0)
            {
                await _viewModel.InitializeAsync();
            }
        }
    }
} 