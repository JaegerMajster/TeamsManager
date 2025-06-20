using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Tools;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using MaterialDesignThemes.Wpf;

namespace TeamsManager.UI.Views.Diagnostics
{
    /// <summary>
    /// Okno diagnostyczne Graph API
    /// </summary>
    public partial class GraphApiDiagnosticWindow : Window
    {
        private readonly GraphApiDiagnosticTool _diagnosticTool;
        private readonly ILogger<GraphApiDiagnosticWindow> _logger;

        public GraphApiDiagnosticWindow(IServiceProvider serviceProvider)
        {
            InitializeComponent();
            
            var apiService = serviceProvider.GetRequiredService<ITeamsManagerApiService>();
            var logger = serviceProvider.GetRequiredService<ILogger<GraphApiDiagnosticTool>>();
            _diagnosticTool = new GraphApiDiagnosticTool(apiService, logger);
            
            _logger = serviceProvider.GetRequiredService<ILogger<GraphApiDiagnosticWindow>>();
            
            Loaded += GraphApiDiagnosticWindow_Loaded;
        }

        private async void GraphApiDiagnosticWindow_Loaded(object sender, RoutedEventArgs e)
        {
            await RunDiagnosticAsync();
        }

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await RunDiagnosticAsync();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async System.Threading.Tasks.Task RunDiagnosticAsync()
        {
            try
            {
                // Pokaż loading
                LoadingCard.Visibility = Visibility.Visible;
                TestResultsItemsControl.Visibility = Visibility.Collapsed;
                RecommendationsItemsControl.Visibility = Visibility.Collapsed;

                _logger.LogInformation("Rozpoczynanie diagnostyki Graph API w oknie");

                var report = await _diagnosticTool.RunFullDiagnosticAsync();

                // Ukryj loading
                LoadingCard.Visibility = Visibility.Collapsed;
                TestResultsItemsControl.Visibility = Visibility.Visible;
                RecommendationsItemsControl.Visibility = Visibility.Visible;

                // Zaktualizuj UI
                UpdateOverallStatus(report.OverallStatus);
                TimestampText.Text = $"Ostatnia aktualizacja: {report.Timestamp:yyyy-MM-dd HH:mm:ss}";
                
                TestResultsItemsControl.ItemsSource = report.TestResults;
                RecommendationsItemsControl.ItemsSource = report.Recommendations;

                _logger.LogInformation("Diagnostyka Graph API zakończona w oknie. Status: {Status}", report.OverallStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania diagnostyki Graph API w oknie");
                
                // Ukryj loading
                LoadingCard.Visibility = Visibility.Collapsed;
                TestResultsItemsControl.Visibility = Visibility.Visible;
                RecommendationsItemsControl.Visibility = Visibility.Visible;

                // Pokaż błąd
                UpdateOverallStatus("Critical");
                OverallStatusText.Text = "Błąd podczas diagnostyki";
                TimestampText.Text = $"Błąd: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";

                var errorResults = new[]
                {
                    new DiagnosticTestResult
                    {
                        TestName = "Diagnostyka Graph API",
                        Status = "Critical",
                        ErrorMessage = ex.Message,
                        Details = "Sprawdź logi aplikacji dla szczegółowych informacji"
                    }
                };

                var errorRecommendations = new[]
                {
                    "🔧 KRYTYCZNE: Sprawdź logi aplikacji",
                    "🔧 KRYTYCZNE: Sprawdź połączenie z internetem",
                    "🔧 KRYTYCZNE: Sprawdź konfigurację Azure AD"
                };

                TestResultsItemsControl.ItemsSource = errorResults;
                RecommendationsItemsControl.ItemsSource = errorRecommendations;
            }
        }

        private void UpdateOverallStatus(string status)
        {
            switch (status.ToLower())
            {
                case "healthy":
                    StatusIcon.Kind = PackIconKind.CheckCircle;
                    StatusIcon.Foreground = Brushes.Green;
                    OverallStatusText.Text = "Graph API - System działa prawidłowo";
                    OverallStatusText.Foreground = Brushes.Green;
                    break;
                
                case "warning":
                    StatusIcon.Kind = PackIconKind.AlertCircle;
                    StatusIcon.Foreground = Brushes.Orange;
                    OverallStatusText.Text = "Graph API - Wykryto ostrzeżenia";
                    OverallStatusText.Foreground = Brushes.Orange;
                    break;
                
                case "critical":
                    StatusIcon.Kind = PackIconKind.CloseCircle;
                    StatusIcon.Foreground = Brushes.Red;
                    OverallStatusText.Text = "Graph API - Problemy krytyczne";
                    OverallStatusText.Foreground = Brushes.Red;
                    break;
                
                default:
                    StatusIcon.Kind = PackIconKind.QuestionMarkCircle;
                    StatusIcon.Foreground = Brushes.Gray;
                    OverallStatusText.Text = "Graph API - Status nieznany";
                    OverallStatusText.Foreground = Brushes.Gray;
                    break;
            }
        }

        /// <summary>
        /// Otwiera okno diagnostyczne Graph API
        /// </summary>
        public static void ShowDiagnostic(IServiceProvider serviceProvider)
        {
            try
            {
                var window = new GraphApiDiagnosticWindow(serviceProvider);
                window.Show();
            }
            catch (Exception ex)
            {
                var logger = serviceProvider.GetService<ILogger<GraphApiDiagnosticWindow>>();
                logger?.LogError(ex, "Błąd podczas otwierania okna diagnostyki Graph API");
                
                MessageBox.Show($"Nie można otworzyć okna diagnostyki Graph API: {ex.Message}", 
                    "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
} 