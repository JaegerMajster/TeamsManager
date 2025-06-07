using System;
using TeamsManager.Core.Models;
using TeamsManager.UI.ViewModels.SchoolTypes;
using Window = System.Windows.Window;
using RoutedEventArgs = System.Windows.RoutedEventArgs;
using Color = System.Windows.Media.Color;
using ColorConverter = System.Windows.Media.ColorConverter;

namespace TeamsManager.UI.Views.SchoolTypes
{
    /// <summary>
    /// Logika interakcji dla SchoolTypeEditDialog.xaml
    /// </summary>
    public partial class SchoolTypeEditDialog : Window
    {
        private SchoolTypeEditViewModel _viewModel;

        public SchoolTypeEditDialog(SchoolType? schoolType = null)
        {
            InitializeComponent();
            
            _viewModel = new SchoolTypeEditViewModel(schoolType);
            DataContext = _viewModel;

            // Obsługa zamknięcia okna
            _viewModel.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(SchoolTypeEditViewModel.DialogResult))
                {
                    DialogResult = _viewModel.DialogResult;
                    Close();
                }
            };
        }



        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            
            // Ustaw focus na pierwsze pole
            Loaded += (s, e) =>
            {
                var firstTextBox = this.FindName("ShortNameTextBox") as System.Windows.Controls.TextBox;
                if (firstTextBox == null)
                {
                    // Znajdź pierwsze TextBox w drzewie wizualnym
                    MoveFocus(new System.Windows.Input.TraversalRequest(System.Windows.Input.FocusNavigationDirection.First));
                }
                else
                {
                    firstTextBox.Focus();
                }
            };
        }
    }
} 