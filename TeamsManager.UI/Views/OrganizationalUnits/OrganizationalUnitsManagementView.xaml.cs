using System.Windows;
using System.Windows.Controls;
using TeamsManager.UI.ViewModels.OrganizationalUnits;

namespace TeamsManager.UI.Views.OrganizationalUnits
{
    /// <summary>
    /// Interaction logic for OrganizationalUnitsManagementView.xaml
    /// </summary>
    public partial class OrganizationalUnitsManagementView : UserControl
    {
        public OrganizationalUnitsManagementView(OrganizationalUnitsManagementViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
        }

        private void TreeView_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (DataContext is OrganizationalUnitsManagementViewModel viewModel && e.NewValue is OrganizationalUnitTreeItemViewModel selectedItem)
            {
                viewModel.SelectedOrganizationalUnit = selectedItem;
            }
        }
    }
} 