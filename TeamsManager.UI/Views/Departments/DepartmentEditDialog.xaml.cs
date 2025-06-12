using System.Windows;
using TeamsManager.UI.ViewModels.Departments;

namespace TeamsManager.UI.Views.Departments
{
    /// <summary>
    /// Interaction logic for DepartmentEditDialog.xaml
    /// </summary>
    public partial class DepartmentEditDialog : Window
    {
        public DepartmentEditDialog()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            if (DataContext is DepartmentEditViewModel viewModel)
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
            if (DataContext is DepartmentEditViewModel viewModel)
            {
                viewModel.RequestClose -= OnRequestClose;
            }
            base.OnClosed(e);
        }
    }
} 