using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Tools;
using TeamsManager.UI.Services;
using TeamsManager.UI.Services.Abstractions;
using MaterialDesignThemes.Wpf;
using System.Linq;
using System.Collections.Generic;

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
            _diagnosticTool = new GraphApiDiagnosticTool(apiService, logger, serviceProvider);
            
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
                PermissionsCard.Visibility = Visibility.Collapsed;

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

                // Sprawdź czy mamy szczegółowe dane uprawnień i wyświetl je
                UpdatePermissionsSection(report);

                _logger.LogInformation("Diagnostyka Graph API zakończona w oknie. Status: {Status}", report.OverallStatus);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Błąd podczas wykonywania diagnostyki Graph API w oknie");
                
                // Ukryj loading
                LoadingCard.Visibility = Visibility.Collapsed;
                TestResultsItemsControl.Visibility = Visibility.Visible;
                RecommendationsItemsControl.Visibility = Visibility.Visible;
                PermissionsCard.Visibility = Visibility.Collapsed;

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

        private void UpdatePermissionsSection(GraphDiagnosticReport report)
        {
            try
            {
                // Znajdź test szczegółowych uprawnień
                var permissionsTest = report.TestResults?.FirstOrDefault(t => 
                    t.TestName.Contains("Szczegółowa analiza uprawnień biznesowych"));

                if (permissionsTest?.Data != null)
                {
                    // Pokaż sekcję uprawnień
                    PermissionsCard.Visibility = Visibility.Visible;

                    // Deserializuj dane z testu (używamy dynamic bo Data jest object)
                    dynamic data = permissionsTest.Data;
                    
                    if (data?.Summary != null && data?.Categories != null)
                    {
                        // Zaktualizuj podsumowanie
                        var summary = data.Summary;
                        PermissionSummaryText.Text = $"Status uprawnień: {summary.Status}";
                        PermissionCompletenessText.Text = $"Kompletność: {summary.OverallCompleteness:F1}% ({summary.TotalGranted}/{summary.TotalRequired} uprawnień)";

                        // Ustaw ikonę podsumowania
                        switch (summary.Status?.ToString()?.ToLower())
                        {
                            case "healthy":
                                PermissionSummaryIcon.Kind = PackIconKind.CheckCircle;
                                PermissionSummaryIcon.Foreground = Brushes.Green;
                                break;
                            case "warning":
                                PermissionSummaryIcon.Kind = PackIconKind.AlertCircle;
                                PermissionSummaryIcon.Foreground = Brushes.Orange;
                                break;
                            case "critical":
                                PermissionSummaryIcon.Kind = PackIconKind.CloseCircle;
                                PermissionSummaryIcon.Foreground = Brushes.Red;
                                break;
                            default:
                                PermissionSummaryIcon.Kind = PackIconKind.QuestionMarkCircle;
                                PermissionSummaryIcon.Foreground = Brushes.Gray;
                                break;
                        }

                        // Przygotuj dane kategorii
                        var categoryViewModels = new List<PermissionCategoryViewModel>();
                        
                        foreach (var categoryPair in data.Categories)
                        {
                            string categoryName = categoryPair.Key;
                            dynamic categoryData = categoryPair.Value;
                            
                            var permissionDetails = new List<PermissionDetailViewModel>();
                            if (categoryData?.Details != null)
                            {
                                foreach (var detail in categoryData.Details)
                                {
                                    permissionDetails.Add(new PermissionDetailViewModel
                                    {
                                        Permission = detail.Permission?.ToString() ?? "",
                                        Status = detail.Status?.ToString() ?? ""
                                    });
                                }
                            }

                            categoryViewModels.Add(new PermissionCategoryViewModel
                            {
                                CategoryName = categoryName,
                                Status = categoryData?.Status?.ToString() ?? "Unknown",
                                Completeness = Convert.ToDouble(categoryData?.Completeness ?? 0),
                                GrantedCount = categoryData?.Granted?.Count ?? 0,
                                TotalCount = (categoryData?.Granted?.Count ?? 0) + (categoryData?.Missing?.Count ?? 0),
                                Permissions = permissionDetails
                            });
                        }

                        PermissionCategoriesItemsControl.ItemsSource = categoryViewModels;
                    }
                }
                else
                {
                    // Ukryj sekcję uprawnień jeśli nie ma danych
                    PermissionsCard.Visibility = Visibility.Collapsed;
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Błąd podczas aktualizacji sekcji uprawnień");
                PermissionsCard.Visibility = Visibility.Collapsed;
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

    /// <summary>
    /// ViewModel dla kategorii uprawnień
    /// </summary>
    public class PermissionCategoryViewModel
    {
        public string CategoryName { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public double Completeness { get; set; }
        public int GrantedCount { get; set; }
        public int TotalCount { get; set; }
        public List<PermissionDetailViewModel> Permissions { get; set; } = new();
    }

    /// <summary>
    /// ViewModel dla szczegółów uprawnienia
    /// </summary>
    public class PermissionDetailViewModel
    {
        public string Permission { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
    }
} 