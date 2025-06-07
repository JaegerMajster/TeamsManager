using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.UserControls
{
    /// <summary>
    /// Interaction logic for ChannelCard.xaml
    /// </summary>
    public partial class ChannelCard : UserControl
    {
        public ChannelCard()
        {
            InitializeComponent();
        }

        public ChannelCard(ChannelCardViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
} 