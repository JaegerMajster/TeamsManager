using System.Windows.Controls;
using TeamsManager.UI.ViewModels.SchoolYears;

namespace TeamsManager.UI.Views.SchoolYears
{
    /// <summary>
    /// Interaction logic for SchoolYearListView.xaml
    /// </summary>
    public partial class SchoolYearListView : UserControl
    {
        public SchoolYearListView()
        {
            InitializeComponent();
        }

        public SchoolYearListView(SchoolYearListViewModel viewModel) : this()
        {
            DataContext = viewModel;
        }
    }
} 