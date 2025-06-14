using ReactiveUI;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services;
using TeamsManager.UI.ViewModels;
using System.Diagnostics;

namespace TeamsManager.UI.ViewModels.Monitoring.Widgets
{
    public class TeamsManagerMetricsWidgetViewModel : BaseViewModel
    {
        private readonly ITeamsManagerApiService _apiService;
        private readonly ILogger<TeamsManagerMetricsWidgetViewModel> _logger;

        // Response Times
        private long _powerShellResponseTime;
        public long PowerShellResponseTime
        {
            get => _powerShellResponseTime;
            set => SetProperty(ref _powerShellResponseTime, value);
        }

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
                _logger.LogDebug("[TEAMS-METRICS-WIDGET] Refreshing metrics data");

                // Pobierz diagnostykę połączenia dla czasów odpowiedzi
                var connectionDiagnostics = await _apiService.GetConnectionDiagnosticsAsync();
                
                if (connectionDiagnostics != null)
                {
                    PowerShellResponseTime = (long)(connectionDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0);
                    GraphApiResponseTime = (long)(connectionDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0);
                }

                // Pobierz metryki pamięci z procesu
                var currentProcess = Process.GetCurrentProcess();
                MemoryUsageMB = currentProcess.WorkingSet64 / (1024.0 * 1024.0);

                // Symulowane dane dla operacji (w przyszłości z API)
                TeamsOperationsToday = Random.Shared.Next(10, 50);
                UsersOperationsToday = Random.Shared.Next(5, 25);
                ChannelsOperationsToday = Random.Shared.Next(2, 15);

                // Symulowane metryki cache
                CacheHitRate = Random.Shared.NextDouble() * 30 + 70; // 70-100%

                // Symulowane połączenia
                ActiveConnections = Random.Shared.Next(1, 5);

                // Symulowane błędy
                ErrorsLastHour = Random.Shared.Next(0, 3);
                WarningsLastHour = Random.Shared.Next(0, 8);

                _logger.LogDebug("[TEAMS-METRICS-WIDGET] Metrics data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-METRICS-WIDGET] Failed to refresh metrics data");
                
                // Ustaw wartości domyślne w przypadku błędu
                PowerShellResponseTime = 0;
                GraphApiResponseTime = 0;
                CacheHitRate = 0;
                TeamsOperationsToday = 0;
                UsersOperationsToday = 0;
                ChannelsOperationsToday = 0;
                MemoryUsageMB = 0;
                ActiveConnections = 0;
                ErrorsLastHour = 0;
                WarningsLastHour = 0;
            }
            finally
            {
                IsLoading = false;
            }
        }

        public void ProcessMetricsUpdate(object metrics)
        {
            // Przetwarzaj aktualizacje metryk z SignalR
            _logger.LogDebug("[TEAMS-METRICS-WIDGET] Processing metrics update from SignalR");
            
            // Odśwież dane asynchronicznie
            _ = Task.Run(async () => await RefreshAsync());
        }
    }
} 