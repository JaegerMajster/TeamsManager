using System;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.UI.ViewModels.SchoolTypes;
using UserControl = System.Windows.Controls.UserControl;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using Application = System.Windows.Application;

namespace TeamsManager.UI.Views.SchoolTypes
{
    /// <summary>
    /// Logika interakcji dla SchoolTypesListView.xaml
    /// </summary>
    public partial class SchoolTypesListView : UserControl
    {
        private SchoolTypesListViewModel? _viewModel;
        private bool _isFirstLoad = true;

        public SchoolTypesListView()
        {
            InitializeComponent();
            
            // Ustaw ViewModel z DI
            if (System.Windows.Application.Current is App && App.ServiceProvider != null)
            {
                _viewModel = App.ServiceProvider.GetRequiredService<SchoolTypesListViewModel>();
                DataContext = _viewModel;
            }
        }

        private async void UserControl_Loaded(object sender, RoutedEventArgs e)
        {
            if (_isFirstLoad && _viewModel != null)
            {
                _isFirstLoad = false;
                await _viewModel.LoadDataAsync();
                
                // Focus na search box
                SearchBox?.Focus();
            }
        }
    }
} 
