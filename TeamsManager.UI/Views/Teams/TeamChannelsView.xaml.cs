using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.Views.Teams
{
    /// <summary>
    /// Interaction logic for TeamChannelsView.xaml
    /// </summary>
    public partial class TeamChannelsView : UserControl
    {
        public TeamChannelsView()
        {
            InitializeComponent();
        }

        public TeamChannelsView(TeamChannelsViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
} 