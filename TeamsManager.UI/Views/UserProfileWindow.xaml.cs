using System.Windows;
using System.Windows.Input;
using TeamsManager.UI.ViewModels.Shell;

namespace TeamsManager.UI.Views
{
    public partial class UserProfileWindow : Window
    {
        public UserProfileWindow(MainShellViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
        }

        private void Header_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                DragMove();
            }
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
} 