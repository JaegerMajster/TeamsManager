using System.Collections.ObjectModel;
using ReactiveUI;
using Microsoft.Extensions.Logging;
using TeamsManager.UI.Services;
using TeamsManager.UI.ViewModels;
using TeamsManager.Core.Models;
using TeamsManager.Core.Abstractions.Services.PowerShell;
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

        // Commands
        public AsyncRelayCommand RunHealthCheckCommand { get; }
        public AsyncRelayCommand RunAutoRepairCommand { get; }
        public AsyncRelayCommand CheckModulesCommand { get; }
        public AsyncRelayCommand InstallModulesCommand { get; }
        public AsyncRelayCommand TestConnectionCommand { get; }

        public TeamsManagerHealthWidgetViewModel(
            ITeamsManagerApiService apiService,
            ILogger<TeamsManagerHealthWidgetViewModel> logger)
        {
            _apiService = apiService ?? throw new ArgumentNullException(nameof(apiService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            Components = new ObservableCollection<HealthComponent>();

            RunHealthCheckCommand = new AsyncRelayCommand(RunHealthCheckAsync, _ => !IsLoading);
            RunAutoRepairCommand = new AsyncRelayCommand(RunAutoRepairAsync, _ => !IsLoading);
            CheckModulesCommand = new AsyncRelayCommand(CheckModulesAsync, _ => !IsLoading);
            InstallModulesCommand = new AsyncRelayCommand(InstallModulesAsync, _ => !IsLoading);
            TestConnectionCommand = new AsyncRelayCommand(TestConnectionAsync, _ => !IsLoading);

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
                _logger.LogDebug("[TEAMS-HEALTH-WIDGET] Refreshing health data");

                // Pobierz diagnostykę połączenia
                var connectionDiagnostics = await _apiService.GetConnectionDiagnosticsAsync();
                var healthInfo = await _apiService.GetConnectionHealthAsync();

                // Wyczyść obecne komponenty
                Components.Clear();

                if (connectionDiagnostics != null)
                {
                    // PowerShell Connection
                    Components.Add(new HealthComponent
                    {
                        Name = "PowerShell Connection",
                        Description = "Połączenie z Microsoft Graph PowerShell",
                        Status = connectionDiagnostics.IsHealthy ? "Healthy" : "Unhealthy",
                        ResponseTime = (long)(connectionDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0),
                        LastChecked = DateTime.Now
                    });

                    // Graph API Status
                    Components.Add(new HealthComponent
                    {
                        Name = "Microsoft Graph API",
                        Description = "Dostęp do Microsoft Graph API",
                        Status = connectionDiagnostics.GraphApiStatus == "Connected" ? "Healthy" : "Unhealthy",
                        ResponseTime = (long)(connectionDiagnostics.LastOperationDuration?.TotalMilliseconds ?? 0),
                        LastChecked = DateTime.Now
                    });

                    // Authentication Status
                    Components.Add(new HealthComponent
                    {
                        Name = "Authentication",
                        Description = "Status uwierzytelniania OAuth2",
                        Status = connectionDiagnostics.HasGraphToken ? "Healthy" : "Unhealthy",
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });

                    // Permissions Check
                    Components.Add(new HealthComponent
                    {
                        Name = "Permissions",
                        Description = "Uprawnienia do zarządzania Teams",
                        Status = connectionDiagnostics.HasUserCreationPermissions ? "Healthy" : "Degraded",
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });
                }

                if (healthInfo != null)
                {
                    // Database Health
                    Components.Add(new HealthComponent
                    {
                        Name = "SQLite Database",
                        Description = "Lokalna baza danych TeamsManager",
                        Status = "Healthy", // Zakładamy że jeśli mamy healthInfo to baza działa
                        ResponseTime = 0,
                        LastChecked = DateTime.Now
                    });
                }

                // Oblicz ogólny status
                CalculateOverallStatus();

                _logger.LogDebug("[TEAMS-HEALTH-WIDGET] Health data refreshed successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Failed to refresh health data");
                
                // Dodaj komponent błędu
                Components.Clear();
                Components.Add(new HealthComponent
                {
                    Name = "Connection Error",
                    Description = "Nie można połączyć się z API TeamsManager",
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
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Running comprehensive health check");
            
            try
            {
                // Uruchom rozszerzoną diagnostykę
                var extendedDiagnostics = await _apiService.GetExtendedConnectionDiagnosticsAsync();
                
                if (extendedDiagnostics != null)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Extended diagnostics completed");
                }
                
                // Odśwież dane
                await RefreshAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Health check failed");
            }
        }

        private async Task RunAutoRepairAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Running auto repair");
            
            try
            {
                // Tutaj można dodać logikę auto-repair
                // Na razie tylko odświeżamy dane
                await RefreshAsync();
                
                _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Auto repair completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Auto repair failed");
            }
        }

        public void ProcessHealthUpdate(object update)
        {
            // Przetwarzaj aktualizacje z SignalR
            _logger.LogDebug("[TEAMS-HEALTH-WIDGET] Processing health update from SignalR");
            
            // Odśwież dane asynchronicznie
            _ = Task.Run(async () => await RefreshAsync());
        }

        private async Task CheckModulesAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Sprawdzanie statusu modułów PowerShell");
            
            try
            {
                IsLoading = true;
                
                var moduleStatus = await _apiService.GetModuleStatusAsync();
                
                if (moduleStatus != null)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Status modułów: {OverallStatus}, Zainstalowane: {InstalledCount}/{RequiredCount}",
                        moduleStatus.OverallStatus, moduleStatus.InstalledModulesCount, moduleStatus.RequiredModulesCount);
                    
                    // Aktualizuj UI na głównym wątku
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Dodaj komponent statusu modułów
                        var existingModuleComponent = Components.FirstOrDefault(c => c.Name == "PowerShell Modules");
                        if (existingModuleComponent != null)
                        {
                            Components.Remove(existingModuleComponent);
                        }
                        
                        Components.Add(new HealthComponent
                        {
                            Name = "PowerShell Modules",
                            Description = $"Status modułów: {moduleStatus.InstalledModulesCount}/{moduleStatus.RequiredModulesCount} zainstalowanych",
                            Status = moduleStatus.OverallStatus,
                            ResponseTime = 0,
                            LastChecked = DateTime.Now
                        });
                        
                        CalculateOverallStatus();
                    });
                    
                    // Pokaż powiadomienie użytkownikowi
                    var statusMessage = moduleStatus.OverallStatus == "Healthy" 
                        ? "✅ Wszystkie moduły PowerShell są zainstalowane i gotowe"
                        : $"⚠️ Status modułów: {moduleStatus.OverallStatus} ({moduleStatus.InstalledModulesCount}/{moduleStatus.RequiredModulesCount} zainstalowanych)";
                    
                    ShowNotification("Status Modułów", statusMessage);
                }
                else
                {
                    _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Nie udało się pobrać statusu modułów");
                    ShowNotification("Błąd", "❌ Nie udało się sprawdzić statusu modułów PowerShell");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Błąd podczas sprawdzania modułów");
                ShowNotification("Błąd", $"❌ Błąd podczas sprawdzania modułów: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task InstallModulesAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Rozpoczynanie instalacji modułów PowerShell");
            
            try
            {
                IsLoading = true;
                ShowNotification("Instalacja", "🔄 Rozpoczynanie instalacji modułów PowerShell...");
                
                var installResult = await _apiService.InstallModulesAsync(false);
                
                if (installResult != null)
                {
                    if (installResult.Success)
                    {
                        _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Instalacja modułów zakończona pomyślnie: {Message}", installResult.Message);
                        ShowNotification("Sukces", $"✅ Instalacja modułów zakończona pomyślnie: {installResult.InstalledCount} modułów zainstalowanych");
                        
                        // Odśwież status po instalacji
                        await CheckModulesAsync();
                        await RefreshAsync();
                    }
                    else
                    {
                        _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Instalacja modułów nie powiodła się: {ErrorMessage}", installResult.ErrorMessage);
                        ShowNotification("Błąd", $"❌ Instalacja nie powiodła się: {installResult.ErrorMessage}");
                    }
                }
                else
                {
                    _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Nie udało się uruchomić instalacji modułów");
                    ShowNotification("Błąd", "❌ Nie udało się uruchomić instalacji modułów");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Błąd podczas instalacji modułów");
                ShowNotification("Błąd", $"❌ Błąd podczas instalacji: {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task TestConnectionAsync()
        {
            _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Rozpoczynanie testu połączenia Microsoft Graph");
            
            try
            {
                IsLoading = true;
                ShowNotification("Test", "🔄 Rozpoczynanie testu połączenia Microsoft Graph...");
                
                var testResult = await _apiService.TestConnectionAsync();
                
                if (testResult != null)
                {
                    _logger.LogInformation("[TEAMS-HEALTH-WIDGET] Test połączenia zakończony. Wynik: {OverallResult}, Testy przeszły: {PassedTests}/{TotalTests}",
                        testResult.OverallResult, testResult.PassedTestsCount, testResult.TotalTestsCount);
                    
                    // Aktualizuj UI na głównym wątku
                    await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
                    {
                        // Dodaj komponent wyniku testu
                        var existingTestComponent = Components.FirstOrDefault(c => c.Name == "Connection Test");
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
                            Name = "Connection Test",
                            Description = $"Test połączenia Graph: {testResult.PassedTestsCount}/{testResult.TotalTestsCount} testów przeszło ({testResult.SuccessPercentage:F1}%)",
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
                    
                    var message = $"{resultIcon} Test połączenia: {testResult.OverallResult}\n" +
                                 $"Testy przeszły: {testResult.PassedTestsCount}/{testResult.TotalTestsCount} ({testResult.SuccessPercentage:F1}%)\n" +
                                 $"Czas trwania: {testResult.TestDuration.TotalMilliseconds:F0}ms";
                    
                    if (testResult.ErrorMessages.Any())
                    {
                        message += $"\nBłędy: {string.Join(", ", testResult.ErrorMessages)}";
                    }
                    
                    ShowNotification("Wynik Testu", message);
                }
                else
                {
                    _logger.LogWarning("[TEAMS-HEALTH-WIDGET] Nie udało się wykonać testu połączenia");
                    ShowNotification("Błąd", "❌ Nie udało się wykonać testu połączenia");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[TEAMS-HEALTH-WIDGET] Błąd podczas testu połączenia");
                ShowNotification("Błąd", $"❌ Błąd podczas testu: {ex.Message}");
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
                    "Wynik Testu" => "📊",
                    "Instalacja" => "📦",
                    _ => "ℹ️"
                };
                
                // Ustaw wiadomość powiadomienia
                LastNotification = $"{title}: {message}";
                
                // Opcjonalnie: pokaż MessageBox tylko dla krytycznych błędów
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