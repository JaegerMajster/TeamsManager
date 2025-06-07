using System;
using System.Windows;
using System.Windows.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services.Abstractions;
using TeamsManager.UI.Services.Configuration;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.Views
{
    public partial class LoginWindow : Window
    {
        private readonly LoginViewModel _viewModel;
        
        public LoginWindow(
            IMsalAuthService msalAuthService,
            ConfigurationManager configManager,
            ILogger<LoginViewModel> logger)
        {
            InitializeComponent();
            
            // Utwórz ViewModel z wstrzykniętymi zależnościami
            _viewModel = new LoginViewModel(msalAuthService, configManager, logger);
            DataContext = _viewModel;
            
            // Subskrybuj zdarzenia
            _viewModel.LoginCompleted += OnLoginCompleted;
            _viewModel.CancelRequested += OnCancelRequested;
        }
        
        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }
        
        private void OnLoginCompleted(object? sender, bool success)
        {
            if (success)
            {
                DialogResult = true;
                Close();
            }
        }
        
        private void OnCancelRequested(object? sender, EventArgs e)
        {
            DialogResult = false;
            Close();
        }
        
        protected override void OnClosed(EventArgs e)
        {
            // Odsubskrybuj zdarzenia
            _viewModel.LoginCompleted -= OnLoginCompleted;
            _viewModel.CancelRequested -= OnCancelRequested;
            
            base.OnClosed(e);
        }
    }
} 