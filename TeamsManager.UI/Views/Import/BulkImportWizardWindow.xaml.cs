using System.Windows;
using TeamsManager.UI.ViewModels.Import;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.Views.Import
{
    /// <summary>
    /// Interaction logic for BulkImportWizardWindow.xaml
    /// </summary>
    public partial class BulkImportWizardWindow : Window
    {
        public BulkImportWizardWindow(BulkImportWizardViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));

            // Ustaw komendę zamknięcia okna
            viewModel.CloseDialogCommand = new RelayCommand<bool?>(result =>
            {
                DialogResult = result;
                Close();
            });

            // Subskrypcja na zdarzenia ViewModel
            Loaded += BulkImportWizardWindow_Loaded;
            Closing += BulkImportWizardWindow_Closing;
        }

        private void BulkImportWizardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Fokus na oknie po załadowaniu
            Focus();
        }

        private void BulkImportWizardWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            // Sprawdź czy import jest w toku
            if (DataContext is BulkImportWizardViewModel viewModel && viewModel.IsImporting)
            {
                var result = MessageBox.Show(
                    "Import jest w toku. Czy na pewno chcesz zamknąć okno?\n\nOperacja zostanie anulowana.",
                    "Potwierdzenie zamknięcia",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.No)
                {
                    e.Cancel = true;
                    return;
                }

                // Anuluj import
                if (viewModel.Progress.CanCancel)
                {
                    _ = viewModel.Progress.CancelImportAsync();
                }
            }
        }

        /// <summary>
        /// Właściwość helper dla wyniku okna dialogowego
        /// </summary>
        public bool? Result => DialogResult;
    }
} 