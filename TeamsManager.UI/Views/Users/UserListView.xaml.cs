using System.Linq;
using System.Windows.Controls;
using TeamsManager.UI.ViewModels.Users;

namespace TeamsManager.UI.Views.Users
{
    /// <summary>
    /// Interaction logic for UserListView.xaml
    /// </summary>
    public partial class UserListView : UserControl
    {
        public UserListView()
        {
            InitializeComponent();
            
            // Event handler dla inicjalizacji ViewModelu po załadowaniu widoku
            Loaded += UserListView_Loaded;
        }

        private void UserListView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            // Inicjalizuj ViewModel po załadowaniu widoku
            if (DataContext is UserListViewModel viewModel)
            {
                // Wywołaj inicjalizację tylko jeśli ViewModel jeszcze nie ma danych
                if (!viewModel.Users.Any())
                {
                    viewModel.InitializeAsync();
                }
            }
        }
    }
} 