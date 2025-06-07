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
        }

        // ViewModel będzie wstrzykiwany przez DI container
        // podczas nawigacji do tego widoku
    }
} 