using System.Windows;
using TeamsManager.UI.ViewModels.OrganizationalUnits;

namespace TeamsManager.UI.Views.OrganizationalUnits
{
    /// <summary>
    /// Interaction logic for OrganizationalUnitEditDialog.xaml
    /// </summary>
    public partial class OrganizationalUnitEditDialog : Window
    {
        public OrganizationalUnitEditDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is OrganizationalUnitEditViewModel viewModel)
            {
                viewModel.RequestClose += OnRequestClose;
            }
        }

        private void OnRequestClose(bool result)
        {
            DialogResult = result;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (DataContext is OrganizationalUnitEditViewModel viewModel)
            {
                viewModel.RequestClose -= OnRequestClose;
            }
            base.OnClosed(e);
        }
    }
} 