using ReactiveUI;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services;
using TeamsManager.UI.ViewModels;
using System.Diagnostics;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class TeamsManagerMetricsWidgetViewModel : BaseViewModel
    {
        private readonly ITeamsManagerApiService _apiService;
        private readonly ILogger<TeamsManagerMetricsWidgetViewModel> _logger;

        // Graph API Response Times
        private long _graphApiResponseTime;
        public long GraphApiResponseTime
        {
            get => _graphApiResponseTime;
            set => SetProperty(ref _graphApiResponseTime, value);
        }

        private double _cacheHitRate;
        public double CacheHitRate
        {
            get => _cacheHitRate;
            set => SetProperty(ref _cacheHitRate, value);
        }

        // Graph API Rate Limiting
        private int _rateLimitRemaining;
        public int RateLimitRemaining
        {
            get => _rateLimitRemaining;
            set => SetProperty(ref _rateLimitRemaining, value);
        }

        private DateTime _rateLimitResetTime;
        public DateTime RateLimitResetTime
        {
            get => _rateLimitResetTime;
            set => SetProperty(ref _rateLimitResetTime, value);
        }

        private bool _isThrottled;
        public bool IsThrottled
        {
            get => _isThrottled;
            set => SetProperty(ref _isThrottled, value);
        }

        private int _graphApiRequestsPerMinute;
        public int GraphApiRequestsPerMinute
        {
            get => _graphApiRequestsPerMinute;
            set => SetProperty(ref _graphApiRequestsPerMinute, value);
        }

        // Operations Today
        private int _teamsOperationsToday;
        public int TeamsOperationsToday
        {
            get => _teamsOperationsToday;
            set => SetProperty(ref _teamsOperationsToday, value);
        }

        private int _usersOperationsToday;
        public int UsersOperationsToday
        {
            get => _usersOperationsToday;
            set => SetProperty(ref _usersOperationsToday, value);
        }

        private int _channelsOperationsToday;
        public int ChannelsOperationsToday
        {
            get => _channelsOperationsToday;
            set => SetProperty(ref _channelsOperationsToday, value);
        }

        private int _batchOperationsToday;
        public int BatchOperationsToday
        {
            get => _batchOperationsToday;
            set => SetProperty(ref _batchOperationsToday, value);
        }

        // System Metrics
        private double _memoryUsageMB;
        public double MemoryUsageMB
        {
            get => _memoryUsageMB;
            set => SetProperty(ref _memoryUsageMB, value);
        }

        private int _activeConnections;
        public int ActiveConnections
        {
            get => _activeConnections;
            set => SetProperty(ref _activeConnections, value);
        }

        // Error Statistics
        private int _errorsLastHour;
        public int ErrorsLastHour
        {
            get => _errorsLastHour;
            set => SetProperty(ref _errorsLastHour, value);
        }

        private int _warningsLastHour;
        public int WarningsLastHour
        {
            get => _warningsLastHour;
            set => SetProperty(ref _warningsLastHour, value);
        }

        // Graph API Status
        private string _graphApiConnectionStatus = "Unknown";
        public string GraphApiConnectionStatus
        {
            get => _graphApiConnectionStatus;
            set => SetProperty(ref _graphApiConnectionStatus, value);
        }

        private bool _graphApiHealthy = true;
        public bool GraphApiHealthy
        {
            get => _graphApiHealthy;
            set => SetProperty(ref _graphApiHealthy, value);
        }

        private string _graphApiVersion = "v1.0";
        public string GraphApiVersion
        {
            get => _graphApiVersion;
            set => SetProperty(ref _graphApiVersion, value);
        }

        private bool _batchOperationsSupported = true;
        public bool BatchOperationsSupported
        {
            get => _batchOperationsSupported;
            set => SetProperty(ref _batchOperationsSupported, value);
        }

        // Notifications
        private string _lastNotification = "Graph API gotowy do działania";
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

        private string _notificationLevel = "Info";
        public string NotificationLevel
        {
            get => _notificationLevel;
            set => SetProperty(ref _notificationLevel, value);
        }

        private bool _isLoading;
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public TeamsManagerMetricsWidgetViewModel(
            ITeamsManagerApiService apiService,
            ILogger<TeamsManagerMetricsWidgetViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

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
                _logger.LogDebug("[TEAMS-METRICS-WIDGET] Refreshing Graph API metrics data");

                // Pobierz diagnostykę Graph API dla czasów odpowiedzi
                var graphDiagnostics = await _apiService.GetGraphConnectionDiagnosticsAsync();
                
                if (graphDiagnostics != null)
                {
                    GraphApiResponseTime = (long)(graphDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0);
                    GraphApiConnectionStatus = graphDiagnostics.GraphApiStatus ?? "Unknown";
                    GraphApiHealthy = graphDiagnostics.IsHealthy;
                }

                // Pobierz status rate limiting Graph API
                try
                {
                    var rateLimitStatus = await _apiService.GetGraphRateLimitStatusAsync();
                    if (rateLimitStatus != null)
                    {
                        RateLimitRemaining = rateLimitStatus.RemainingRequests ?? 0;
                        RateLimitResetTime = rateLimitStatus.ResetTime ?? DateTime.Now;
                        IsThrottled = rateLimitStatus.IsThrottled;
                        
                        // Sprawdź czy rate limit jest niski i wyślij powiadomienie
                        CheckRateLimitNotifications(rateLimitStatus);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TEAMS-METRICS-WIDGET] Failed to get Graph API rate limit status");
                }

                // Pobierz metryki Graph API
                try
                {
                    var graphMetrics = await _apiService.GetGraphMetricsAsync();
                    if (graphMetrics != null)
                    {
                        GraphApiRequestsPerMinute = graphMetrics.RequestsPerMinute;
                        CacheHitRate = graphMetrics.CacheHitRate;
                        BatchOperationsToday = graphMetrics.BatchOperationsCount;
                        
                        // Sprawdź metryki i wyślij powiadomienia jeśli potrzeba
                        CheckMetricsNotifications(graphMetrics);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "[TEAMS-METRICS-WIDGET] Failed to get Graph API metrics");
                }

                // Pobierz metryki pamięci z procesu
                var currentProcess = Process.GetCurrentProcess();
                MemoryUsageMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);

                // Symulowane dane dla operacji (w przyszłości z prawdziwych metryk Graph API)
                TeamsOperationsToday = Random.Shared.Next(10, 50);
                UsersOperationsToday = Random.Shared.Next(5, 25);
                ChannelsOperationsToday = Random.Shared.Next(2, 15);

                // Symulowane połączenia Graph API
                ActiveConnections = Random.Shared.Next(1, 5);

                // Symulowane błędy Graph API
                ErrorsLastHour = Random.Shared.Next(0, 3);
                WarningsLastHour = Random.Shared.Next(0, 8);

                // Sprawdź błędy i wyślij powiadomienia
                CheckErrorNotifications();

                _logger.LogDebug("[TEAMS-METRICS-WIDGET] Graph API metrics data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-METRICS-WIDGET] Failed to refresh Graph API metrics data");
                
                // Ustaw wartości domyślne w przypadku błędu
                GraphApiResponseTime = 0;
                CacheHitRate = 0;
                RateLimitRemaining = 0;
                GraphApiRequestsPerMinute = 0;
                TeamsOperationsToday = 0;
                UsersOperationsToday = 0;
                ChannelsOperationsToday = 0;
                BatchOperationsToday = 0;
                MemoryUsageMB = 0;
                ActiveConnections = 0;
                ErrorsLastHour = 0;
                WarningsLastHour = 0;
                GraphApiConnectionStatus = "Error";
                GraphApiHealthy = false;
                
                // Wyślij powiadomienie o błędzie
                ShowNotification("Błąd", $"❌ Nie udało się odświeżyć metryk Graph API: {ex.Message}", "Error");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private void CheckRateLimitNotifications(dynamic rateLimitStatus)
        {
            if (rateLimitStatus.IsThrottled)
            {
                ShowNotification("Rate Limiting", "⚠️ Graph API jest obecnie ograniczane - operacje mogą być wolniejsze", "Warning");
            }
            else if (rateLimitStatus.RemainingRequests < 100)
            {
                ShowNotification("Rate Limit", $"⚠️ Pozostało tylko {rateLimitStatus.RemainingRequests} requestów Graph API", "Warning");
            }
            else if (rateLimitStatus.RemainingRequests < 500)
            {
                ShowNotification("Rate Limit", $"ℹ️ Pozostało {rateLimitStatus.RemainingRequests} requestów Graph API", "Info");
            }
        }

        private void CheckMetricsNotifications(dynamic graphMetrics)
        {
            // Sprawdź wysokie użycie requestów
            if (graphMetrics.RequestsPerMinute > 100)
            {
                ShowNotification("Wysokie Użycie", $"⚠️ Wysokie użycie Graph API: {graphMetrics.RequestsPerMinute} req/min", "Warning");
            }
            
            // Sprawdź niski cache hit rate
            if (graphMetrics.CacheHitRate < 50)
            {
                ShowNotification("Cache", $"⚠️ Niski cache hit rate: {graphMetrics.CacheHitRate:F1}%", "Warning");
            }
            
            // Sprawdź batch operations
            if (graphMetrics.BatchOperationsCount > 50)
            {
                ShowNotification("Batch Operations", $"ℹ️ Wysokie użycie batch operations: {graphMetrics.BatchOperationsCount}", "Info");
            }
        }

        private void CheckErrorNotifications()
        {
            if (ErrorsLastHour > 5)
            {
                ShowNotification("Błędy", $"❌ Wysoka liczba błędów Graph API: {ErrorsLastHour} w ostatniej godzinie", "Error");
            }
            else if (WarningsLastHour > 10)
            {
                ShowNotification("Ostrzeżenia", $"⚠️ Wysoka liczba ostrzeżeń Graph API: {WarningsLastHour} w ostatniej godzinie", "Warning");
            }
        }

        private void ShowNotification(string title, string message, string level)
        {
            // Loguj powiadomienie
            _logger.LogInformation("[TEAMS-METRICS-WIDGET] NOTIFICATION: {Title} - {Message} [{Level}]", title, message, level);
            
            // Aktualizuj powiadomienie w interfejsie
            System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                // Ustaw ikonę na podstawie poziomu
                NotificationIcon = level switch
                {
                    "Error" => "❌",
                    "Warning" => "⚠️",
                    "Info" => "ℹ️",
                    "Success" => "✅",
                    _ => "ℹ️"
                };
                
                // Ustaw poziom powiadomienia
                NotificationLevel = level;
                
                // Ustaw wiadomość powiadomienia
                LastNotification = $"{title}: {message}";
            });
        }

        public void ProcessMetricsUpdate(object metrics)
        {
            // Przetwarzaj aktualizacje metryk Graph API z SignalR
            _logger.LogDebug("[TEAMS-METRICS-WIDGET] Processing Graph API metrics update from SignalR");
            
            // Odśwież dane asynchronicznie
            _ = Task.Run(async () => await RefreshAsync());
        }
    }
} 