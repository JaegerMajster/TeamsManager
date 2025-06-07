using System;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Windows;
using TeamsManager.UI.ViewModels.Users;

namespace TeamsManager.UI.Views.Users
{
    /// <summary>
    /// Okno szczegółów/edycji użytkownika.
    /// Obsługuje tryby tworzenia i edycji użytkowników z zakładkami.
    /// </summary>
    public partial class UserDetailWindow : Window
    {
        private readonly UserDetailViewModel _viewModel;

        public UserDetailWindow(UserDetailViewModel viewModel)
        {
            InitializeComponent();
            _viewModel = viewModel;
            DataContext = _viewModel;
            
            // Obsługa zamykania okna
            _viewModel.PropertyChanged += ViewModel_PropertyChanged;
        }

        /// <summary>
        /// Inicjalizuje okno dla edycji istniejącego użytkownika lub tworzenia nowego.
        /// </summary>
        /// <param name="userId">ID użytkownika do edycji (null dla nowego użytkownika)</param>
        public async Task InitializeAsync(string? userId = null)
        {
            await _viewModel.InitializeAsync(userId);
        }

        /// <summary>
        /// Obsługuje zmiany właściwości ViewModel, szczególnie DialogResult.
        /// </summary>
        private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(UserDetailViewModel.DialogResult))
            {
                if (_viewModel.DialogResult.HasValue)
                {
                    DialogResult = _viewModel.DialogResult.Value;
                    Close();
                }
            }
        }

        /// <summary>
        /// Czyści event handlery przy zamykaniu okna.
        /// </summary>
        protected override void OnClosed(EventArgs e)
        {
            if (_viewModel != null)
            {
                _viewModel.PropertyChanged -= ViewModel_PropertyChanged;
            }
            base.OnClosed(e);
        }
    }
} 