using System.Windows;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.Views.Teams
{
    public partial class TeamCreationWizardWindow : Window
    {
        public TeamCreationWizardWindow()
        {
            InitializeComponent();
        }

        public TeamCreationWizardWindow(TeamCreationWizardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }

        public bool? ShowDialogResult()
        {
            var result = ShowDialog();
            if (DataContext is TeamCreationWizardViewModel vm)
            {
                return vm.DialogResult;
            }
            return result;
        }
    }
} 