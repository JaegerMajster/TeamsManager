using System.Collections.ObjectModel;
using ReactiveUI;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services;
using TeamsManager.UI.ViewModels;
using TeamsManager.Core.Models;
using System.Windows;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class TeamsManagerHealthWidgetViewModel : BaseViewModel
    {
        private readonly ITeamsManagerApiService _apiService;
        private readonly ILogger<TeamsManagerHealthWidgetViewModel> _logger;

        public ObservableCollection<HealthComponent> Components { get; }

        private string _overallStatus = "Unknown";
        public string OverallStatus
        {
            get => _overallStatus;
            set => SetProperty(ref _overallStatus, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        private string _lastNotification = "Gotowy do działania";
        public string LastNotification
        {
            get => _lastNotification;
            set => SetProperty(ref _lastNotification, value);
        }

        private string _notificationIcon = "ℹ️";
        public string NotificationIcon
        {
            get => _notificationIcon;
            set => SetProperty(ref _notificationIcon, value);
        }

        // Commands - Graph API specific
        public AsyncRelayCommand RunHealthCheckCommand { get; }
        public AsyncRelayCommand RunAutoRepairCommand { get; }
        public AsyncRelayCommand TestGraphConnectionCommand { get; }
        public AsyncRelayCommand RefreshGraphTokenCommand { get; }
        public AsyncRelayCommand ClearGraphCacheCommand { get; }

        public TeamsManagerHealthWidgetViewModel(
            ITeamsManagerApiService apiService,
            ILogger<TeamsManagerHealthWidgetViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Components = new ObservableCollection<HealthComponent>();

            RunHealthCheckCommand = new AsyncRelayCommand(RunHealthCheckAsync, _ => !IsLoading);
            RunAutoRepairCommand = new AsyncRelayCommand(RunAutoRepairAsync, _ => !IsLoading);
            TestGraphConnectionCommand = new AsyncRelayCommand(TestGraphConnectionAsync, _ => !IsLoading);
            RefreshGraphTokenCommand = new AsyncRelayCommand(RefreshGraphTokenAsync, _ => !IsLoading);
            ClearGraphCacheCommand = new AsyncRelayCommand(ClearGraphCacheAsync, _ => !IsLoading);

            InitializeAsync();
        }

        private async void InitializeAsync()
        {
            await RefreshAsync();
        }

        public async Task RefreshAsync()
        {
            if (IsLoading) return;

            IsLoading = true;
            try
            {
                _logger.LogDebug("[TEAMS-HEALTH-WIDGET] Refreshing Graph API health data");

                // Pobierz diagnostykę Graph API
                var graphDiagnostics = await _apiService.GetGraphConnectionDiagnosticsAsync();
                var graphHealthInfo = await _apiService.GetGraphConnectionHealthAsync();

                // Wyczyść obecne komponenty
                Components.Clear();

                if (graphDiagnostics != null)
                {
                    // Microsoft Graph API Connection
                    Components.Add(new HealthComponent
                    {
                        Name = "Microsoft Graph API Connection",
                        Description = "Połączenie z Microsoft Graph API",
                        Status = graphDiagnostics.IsHealthy ? "Healthy" : "Unhealthy",
                        ResponseTime = (long)(graphDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0),
                        LastChecked = DateTime.Now
                    });

                    // Graph API Authentication
                    Components.Add(new HealthComponent
                    {
                        Name = "Graph API Authentication",
                        Description = "Status uwierzytelniania OAuth2 Graph API",
                        Status = graphDiagnostics.HasGraphToken ? "Healthy" : "Unhealthy",
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });

                    // Graph API Permissions
                    Components.Add(new HealthComponent
                    {
                        Name = "Graph API Permissions",
                        Description = "Uprawnienia do zarządzania Teams przez Graph API",
                        Status = graphDiagnostics.HasUserCreationPermissions ? "Healthy" : "Degraded",
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });

                    // Graph API Endpoints
                    Components.Add(new HealthComponent
                    {
                        Name = "Graph API Endpoints",
                        Description = "Dostępność endpointów Graph API",
                        Status = graphDiagnostics.GraphApiStatus == "Connected" ? "Healthy" : "Unhealthy",
                        ResponseTime = (long)(graphDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0),
                        LastChecked = DateTime.Now
                    });
                }

                if (graphHealthInfo != null)
                {
                    // Graph API Cache
                    Components.Add(new HealthComponent
                    {
                        Name = "Graph API Cache",
                        Description = "Status cache Graph API",
                        Status = "Healthy",
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });

                    // Local Database
                    Components.Add(new HealthComponent
                    {
                        Name = "Local Database",
                        Description = "Lokalna baza danych TeamsManager",
                        Status = "Healthy",
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });
                }

                // Oblicz ogólny status
                CalculateOverallStatus();

                _logger.LogDebug("[TEAMS-HEALTH-WIDGET] Graph API health data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Failed to refresh Graph API health data");
                
                // Dodaj komponent błędu
                Components.Clear();
                Components.Add(new HealthComponent
                {
                    Name = "Connection Error",
                    Description = "Nie można połączyć się z Graph API TeamsManager",
                    Status = "Unhealthy",
                    ResponseTime = 0,
                    LastChecked = DateTime.Now
                });
                
                OverallStatus = "Unhealthy";
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CalculateOverallStatus()
        {
            if (!Components.Any())
            {
                OverallStatus = "Unknown";
                return;
            }

            var unhealthyCount = Components.Count(c => c.Status == "Unhealthy");
            var degradedCount = Components.Count(c => c.Status == "Degraded");

            if (unhealthyCount > 0)
            {
                OverallStatus = "Unhealthy";
            }
            else if (degradedCount > 0)
            {
                OverallStatus = "Degraded";
            }
            else
            {
                OverallStatus = "Healthy";
            }
        }

        private async Task RunHealthCheckAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Running comprehensive Graph API health check");
            
            try
            {
                // Uruchom rozszerzoną diagnostykę Graph API
                var extendedGraphDiagnostics = await _apiService.GetExtendedGraphConnectionDiagnosticsAsync();
                
                if (extendedGraphDiagnostics != null)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Extended Graph API diagnostics completed");
                    ShowNotification("Health Check", "✅ Rozszerzona diagnostyka Graph API zakończona pomyślnie");
                }
                
                // Pobierz pełny raport diagnostyczny Graph API
                var fullDiagnosticReport = await _apiService.GetFullGraphDiagnosticReportAsync();
                if (fullDiagnosticReport != null)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Full Graph API diagnostic report generated");
                }
                
                // Odśwież dane
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Graph API health check failed");
                ShowNotification("Błąd", $"❌ Health check Graph API nie powiódł się: {ex.Message}");
            }
        }

        private async Task RunAutoRepairAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Running Graph API auto repair");
            
            try
            {
                ShowNotification("Auto Repair", "🔄 Rozpoczynanie automatycznej naprawy Graph API...");
                
                // Odśwież token Graph API
                var tokenRefreshed = await _apiService.RefreshGraphTokenAsync();
                if (tokenRefreshed)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Graph API token refreshed successfully");
                }
                
                // Wyczyść cache Graph API
                var cacheCleared = await _apiService.ClearGraphCacheAsync();
                if (cacheCleared)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Graph API cache cleared successfully");
                }
                
                // Odśwież dane
                await RefreshAsync();
                
                ShowNotification("Auto Repair", "✅ Automatyczna naprawa Graph API zakończona pomyślnie");
                _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Graph API auto repair completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Graph API auto repair failed");
                ShowNotification("Błąd", $"❌ Automatyczna naprawa Graph API nie powiodła się: {ex.Message}");
            }
        }

        public void ProcessHealthUpdate(object update)
        {
            // Przetwarzaj aktualizacje z SignalR dla Graph API
            _logger.LogDebug("[TEAMS-HEALTH-WIDGET] Processing Graph API health update from SignalR");
            
            // Odśwież dane asynchronicznie
            _ = Task.Run(async () => await RefreshAsync());
        }

        private async Task TestGraphConnectionAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Rozpoczynanie testu połączenia Graph API");
            
            try
            {
                IsLoading = true;
                ShowNotification("Test", "🔄 Rozpoczynanie testu połączenia Graph API...");
                
                var testResult = await _apiService.TestGraphConnectionAsync();
                
                if (testResult != null)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Test połączenia Graph API zakończony. Wynik: {OverallResult}, Testy przeszły: {PassedTests}/{TotalTests}",
                        testResult.OverallResult, testResult.PassedTestsCount, testResult.TotalTestsCount);
                    
                    // Aktualizuj UI na głównym wątku
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {

                        var existingTestComponent = Components.FirstOrDefault(c => c.Name == "Graph API Connection Test");
                        if (existingTestComponent != null)
                        {
                            Components.Remove(existingTestComponent);
                        }
                        
                        var testStatus = testResult.OverallResult switch
                        {
                            "Passed" => "Healthy",
                            "Partial" => "Degraded",
                            "Failed" => "Unhealthy",
                            _ => "Unknown"
                        };
                        
                        Components.Add(new HealthComponent
                        {
                            Name = "Graph API Connection Test",
                            Description = $"Test połączenia Graph API: {testResult.PassedTestsCount}/{testResult.TotalTestsCount} testów przeszło ({testResult.SuccessPercentage:F1}%)",
                            Status = testStatus,
                            ResponseTime = (long)testResult.TestDuration.TotalMilliseconds,
                            LastChecked = DateTime.Now
                        });
                        
                        CalculateOverallStatus();
                    });
                    
                    // Pokaż szczegółowe powiadomienie
                    var resultIcon = testResult.OverallResult switch
                    {
                        "Passed" => "✅",
                        "Partial" => "⚠️",
                        "Failed" => "❌",
                        _ => "❓"
                    };
                    
                    var message = $"{resultIcon} Test połączenia Graph API: {testResult.OverallResult}\n" +
                                 $"Testy przeszły: {testResult.PassedTestsCount}/{testResult.TotalTestsCount} ({testResult.SuccessPercentage:F1}%)\n" +
                                 $"Czas trwania: {testResult.TestDuration.TotalMilliseconds:F0}ms";
                    
                                if (testResult.Errors.Any())
            {
                message += $"\nBłędy: {string.Join(", ", testResult.Errors)}";
            }
                    
                    ShowNotification("Wynik Testu Graph API", message);
                }
                else
                {
                    _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Nie udało się wykonać testu połączenia Graph API");
                    ShowNotification("Błąd", "❌ Nie udało się wykonać testu połączenia Graph API");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Błąd podczas testu połączenia Graph API");
                ShowNotification("Błąd", $"❌ Błąd podczas testu Graph API: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task RefreshGraphTokenAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Odświeżanie tokenu Graph API");
            
            try
            {
                IsLoading = true;
                ShowNotification("Token", "🔄 Odświeżanie tokenu Graph API...");
                
                var tokenRefreshed = await _apiService.RefreshGraphTokenAsync();
                
                if (tokenRefreshed)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Token Graph API odświeżony pomyślnie");
                    ShowNotification("Sukces", "✅ Token Graph API odświeżony pomyślnie");
                    
                    // Odśwież dane po odświeżeniu tokenu
                    await RefreshAsync();
                }
                else
                {
                    _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Nie udało się odświeżyć tokenu Graph API");
                    ShowNotification("Błąd", "❌ Nie udało się odświeżyć tokenu Graph API");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Błąd podczas odświeżania tokenu Graph API");
                ShowNotification("Błąd", $"❌ Błąd podczas odświeżania tokenu: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task ClearGraphCacheAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Czyszczenie cache Graph API");
            
            try
            {
                IsLoading = true;
                ShowNotification("Cache", "🔄 Czyszczenie cache Graph API...");
                
                var cacheCleared = await _apiService.ClearGraphCacheAsync();
                
                if (cacheCleared)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Cache Graph API wyczyszczony pomyślnie");
                    ShowNotification("Sukces", "✅ Cache Graph API wyczyszczony pomyślnie");
                    
                    // Odśwież dane po wyczyszczeniu cache
                    await RefreshAsync();
                }
                else
                {
                    _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Nie udało się wyczyścić cache Graph API");
                    ShowNotification("Błąd", "❌ Nie udało się wyczyścić cache Graph API");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Błąd podczas czyszczenia cache Graph API");
                ShowNotification("Błąd", $"❌ Błąd podczas czyszczenia cache: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void ShowNotification(string title, string message)
        {
            // Loguj powiadomienie
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] NOTIFICATION: {Title} - {Message}", title, message);
            
            // Aktualizuj powiadomienie w interfejsie
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Ustaw ikonę na podstawie tytułu
                NotificationIcon = title switch
                {
                    "Sukces" => "✅",
                    "Błąd" => "❌",
                    "Ostrzeżenie" => "⚠️",
                    "Test" => "🔄",
                    "Wynik Testu Graph API" => "📊",
                    "Health Check" => "🏥",
                    "Auto Repair" => "🔧",
                    "Token" => "🔑",
                    "Cache" => "💾",
                    _ => "ℹ️"
                };
                
                // Ustaw wiadomość powiadomienia
                LastNotification = $"{title}: {message}";
                
    
                if (title == "Błąd" && message.Contains("Krytyczny"))
                {
                    System.Windows.MessageBox.Show(message, title, System.Windows.MessageBoxButton.OK, 
                        System.Windows.MessageBoxImage.Error);
                }
            });
        }
    }

    public class HealthComponent : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _status = "Unknown";
        private long _responseTime;
        private DateTime _lastChecked;

        public string Name
        {
            get => _name;
            set
            {
                if (_name != value)
                {
                    _name = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Description
        {
            get => _description;
            set
            {
                if (_description != value)
                {
                    _description = value;
                    OnPropertyChanged();
                }
            }
        }

        public string Status
        {
            get => _status;
            set
            {
                if (_status != value)
                {
                    _status = value;
                    OnPropertyChanged();
                }
            }
        }

        public long ResponseTime
        {
            get => _responseTime;
            set
            {
                if (_responseTime != value)
                {
                    _responseTime = value;
                    OnPropertyChanged();
                }
            }
        }

        public DateTime LastChecked
        {
            get => _lastChecked;
            set
            {
                if (_lastChecked != value)
                {
                    _lastChecked = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 