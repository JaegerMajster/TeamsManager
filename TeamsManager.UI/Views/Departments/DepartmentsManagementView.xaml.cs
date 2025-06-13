using System.Windows;
using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Departments;
using TeamsManager.UI.ViewModels.OrganizationalUnits;

namespace TeamsManager.UI.Views.Departments
{
    /// <summary>
    /// Interaction logic for DepartmentsManagementView.xaml
    /// </summary>
    public partial class DepartmentsManagementView : UserControl
    {
        public DepartmentsManagementView(DepartmentsManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is DepartmentsManagementViewModel viewModel && e.NewValue is OrganizationalUnitTreeItemViewModel selectedItem)
            {
                viewModel.SelectedItem = selectedItem;
            }
        }
    }
} 