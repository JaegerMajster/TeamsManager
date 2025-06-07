using System.Windows.Controls;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.ViewModels.Operations;

namespace TeamsManager.UI.Views.Operations
{
    /// <summary>
    /// Interaction logic for OperationHistoryView.xaml
    /// </summary>
    public partial class OperationHistoryView : UserControl
    {
        private readonly ILogger<OperationHistoryView> _logger;

        // Konstruktor bezparametrowy dla XAML
        public OperationHistoryView()
        {
            InitializeComponent();
            _logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<OperationHistoryView>.Instance;
        }

        public OperationHistoryView(
            OperationHistoryViewModel viewModel,
            ILogger<OperationHistoryView> logger)
        {
            InitializeComponent();
            
            _logger = logger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<OperationHistoryView>.Instance;
            
            // Ustaw DataContext z DI
            DataContext = viewModel;
            
            _logger.LogDebug("OperationHistoryView created with DI");
            
            Loaded += OperationHistoryView_Loaded;
        }

        private void OperationHistoryView_Loaded(object sender, System.Windows.RoutedEventArgs e)
        {
            _logger.LogDebug("OperationHistoryView loaded");
        }
    }
} 