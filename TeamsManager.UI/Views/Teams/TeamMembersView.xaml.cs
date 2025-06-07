using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.Views.Teams
{
    /// <summary>
    /// Interaction logic for TeamMembersView.xaml
    /// </summary>
    public partial class TeamMembersView : UserControl
    {
        public TeamMembersView()
        {
            InitializeComponent();
        }

        public TeamMembersView(TeamMembersViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
} 