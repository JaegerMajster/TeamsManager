using UserControl = System.Windows.Controls.UserControl;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels.Dashboard;

namespace TeamsManager.UI.Views.Dashboard
{
    /// <summary>
    /// Dashboard główny aplikacji Teams Manager
    /// </summary>
    public partial class DashboardView : UserControl
    {
        private readonly ILogger<DashboardView> _logger;

        public DashboardView(DashboardViewModel viewModel, ILogger<DashboardView> logger)
        {
            InitializeComponent();
            
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<DashboardView>.Instance;
            
            // Ustaw DataContext z DI
            DataContext = viewModel;
            
            _logger.LogDebug("DashboardView created with DI");
            
            Loaded += DashboardView_Loaded;
        }

        private async void DashboardView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _logger.LogDebug("DashboardView loaded");
            
            // ViewModel powinien być ustawiony przez DataContext
            if (DataContext is DashboardViewModel viewModel)
            {
                await viewModel.LoadStatisticsAsync();
            }
        }
    }
} 