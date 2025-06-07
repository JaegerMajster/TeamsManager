using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using TeamsManager.UI.ViewModels.Monitoring.Widgets;

namespace TeamsManager.UI.Views.Monitoring.Widgets
{
    /// <summary>
    /// Code-behind dla zaawansowanego widgetu wykresów wydajności
    /// </summary>
    public partial class AdvancedPerformanceChartWidget : UserControl
    {
        public AdvancedPerformanceChartWidget()
        {
            InitializeComponent();
            
            // Pobierz ViewModel z DI jeśli dostępne
            if (App.ServiceProvider != null)
            {
                try
                {
                    var viewModel = App.ServiceProvider.GetRequiredService<AdvancedPerformanceChartWidgetViewModel>();
                    DataContext = viewModel;
                }
                catch
                {
                    // Fallback - DataContext zostanie ustawiony zewnętrznie
                }
            }
        }
    }
} 