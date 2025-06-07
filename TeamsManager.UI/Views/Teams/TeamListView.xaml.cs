using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Teams;

namespace TeamsManager.UI.Views.Teams
{
    public partial class TeamListView : UserControl
    {
        public TeamListView()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }
        
        private async void OnLoaded(object sender, System.Windows.RoutedEventArgs e)
        {
            if (DataContext is TeamListViewModel viewModel)
            {
                await viewModel.InitializeAsync();
            }
        }
    }
} 