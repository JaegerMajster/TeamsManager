using System.Windows;
using System.Windows.Input;
using TeamsManager.UI.Models;
using TeamsManager.UI.ViewModels.Dialogs;

namespace TeamsManager.UI.Views.Dialogs
{
    /// <summary>
    /// Interaction logic for UniversalDialog.xaml
    /// </summary>
    public partial class UniversalDialog : Window
    {
        private DialogResponse? _result;

        public UniversalDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        /// <summary>
        /// Wynik dialogu
        /// </summary>
        public DialogResponse? Result => _result;

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is UniversalDialogViewModel viewModel)
            {
                viewModel.DialogClosed += OnDialogClosed;
            }

            // Ustaw fokus na głównym przycisku jeśli jest domyślny
            if (DataContext is UniversalDialogViewModel vm && vm.IsPrimaryDefault)
            {
                MoveFocus(new TraversalRequest(FocusNavigationDirection.First));
            }
        }

        private void OnDialogClosed(DialogResponse response)
        {
            _result = response;
            
            // Ustaw DialogResult dla kompatybilności z ShowDialog()
            DialogResult = response.IsPrimary;
            
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (DataContext is UniversalDialogViewModel viewModel)
            {
                viewModel.DialogClosed -= OnDialogClosed;
            }
            base.OnClosed(e);
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            // Obsługa klawiszy Escape i Enter
            if (e.Key == Key.Escape && DataContext is UniversalDialogViewModel viewModel)
            {
                if (viewModel.ShowSecondaryButton && viewModel.IsSecondaryCancel)
                {
                    viewModel.SecondaryCommand.Execute(null);
                }
                else
                {
                    viewModel.CloseCommand.Execute(null);
                }
                e.Handled = true;
            }
            else if (e.Key == Key.Enter && DataContext is UniversalDialogViewModel vm)
            {
                if (vm.IsPrimaryDefault)
                {
                    vm.PrimaryCommand.Execute(null);
                    e.Handled = true;
                }
            }

            base.OnKeyDown(e);
        }
    }
} 