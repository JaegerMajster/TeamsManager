using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;
using Microsoft.Extensions.Logging;
using TeamsManager.Core.Abstractions.Services;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Dashboard
{
    /// <summary>
    /// ViewModel dla Dashboard'a głównego
    /// Obsługuje logikę biznesową i powiązanie danych dla interfejsu użytkownika
    /// </summary>
    public class DashboardViewModel : INotifyPropertyChanged
    {
        private readonly ILogger<DashboardViewModel> _logger;
        private readonly ITeamService _teamService;
        private readonly IUserService _userService;
        private readonly IOperationHistoryService _operationHistoryService;

        private int _activeTeamsCount;
        private int _totalUsersCount;
        private int _todayOperationsCount;
        private double _activityPercentage;
        private bool _isLoading;
        private string _currentDate;
        private string _activityDescription;
        private ObservableCollection<OperationHistoryItem> _recentOperations;
        private ObservableCollection<NotificationItem> _notifications;

        public DashboardViewModel(
            ILogger<DashboardViewModel> logger,
            ITeamService teamService,
            IUserService userService,
            IOperationHistoryService operationHistoryService)
        {
            _logger = logger;
            _teamService = teamService;
            _userService = userService;
            _operationHistoryService = operationHistoryService;

            // Inicjalizacja kolekcji
            _recentOperations = new ObservableCollection<OperationHistoryItem>();
            _notifications = new ObservableCollection<NotificationItem>();

            // Inicjalizacja komend
            RefreshCommand = new RelayCommand(async _ => await LoadStatisticsAsync());
            CreateTeamCommand = new RelayCommand(_ => NavigateToCreateTeam());
            ManageUsersCommand = new RelayCommand(_ => NavigateToManageUsers());
            GenerateReportsCommand = new RelayCommand(_ => NavigateToReports());
            ViewAllOperationsCommand = new RelayCommand(_ => NavigateToOperationHistory());

            // Ustaw datę
            CurrentDate = DateTime.Now.ToString("dddd, d MMMM yyyy");
            ActivityDescription = "Obliczanie...";
        }

        #region Properties

        /// <summary>
        /// Liczba aktywnych zespołów
        /// </summary>
        public int ActiveTeamsCount
        {
            get => _activeTeamsCount;
            set => SetProperty(ref _activeTeamsCount, value);
        }

        /// <summary>
        /// Całkowita liczba użytkowników
        /// </summary>
        public int TotalUsersCount
        {
            get => _totalUsersCount;
            set => SetProperty(ref _totalUsersCount, value);
        }

        /// <summary>
        /// Liczba operacji wykonanych dzisiaj
        /// </summary>
        public int TodayOperationsCount
        {
            get => _todayOperationsCount;
            set => SetProperty(ref _todayOperationsCount, value);
        }

        /// <summary>
        /// Procent aktywności w porównaniu do wczoraj
        /// </summary>
        public double ActivityPercentage
        {
            get => _activityPercentage;
            set => SetProperty(ref _activityPercentage, value);
        }

        /// <summary>
        /// Czy dane są aktualnie ładowane
        /// </summary>
        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        /// <summary>
        /// Data aktualna
        /// </summary>
        public string CurrentDate
        {
            get => _currentDate;
            set => SetProperty(ref _currentDate, value);
        }

        /// <summary>
        /// Opis aktywności
        /// </summary>
        public string ActivityDescription
        {
            get => _activityDescription;
            set => SetProperty(ref _activityDescription, value);
        }

        /// <summary>
        /// Lista najnowszych operacji
        /// </summary>
        public ObservableCollection<OperationHistoryItem> RecentOperations
        {
            get => _recentOperations;
            set => SetProperty(ref _recentOperations, value);
        }

        /// <summary>
        /// Lista powiadomień
        /// </summary>
        public ObservableCollection<NotificationItem> Notifications
        {
            get => _notifications;
            set => SetProperty(ref _notifications, value);
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand CreateTeamCommand { get; }
        public ICommand ManageUsersCommand { get; }
        public ICommand GenerateReportsCommand { get; }
        public ICommand ViewAllOperationsCommand { get; }

        #endregion

        #region Methods

        /// <summary>
        /// Ładuje statystyki Dashboard'a
        /// </summary>
        public async Task LoadStatisticsAsync()
        {
            try
            {
                IsLoading = true;
                _logger.LogInformation("Loading dashboard statistics...");

                // Równoległe ładowanie danych
                var teamsTask = LoadTeamsCountAsync();
                var usersTask = LoadUsersCountAsync();
                var operationsTask = LoadRecentOperationsAsync();

                await Task.WhenAll(teamsTask, usersTask, operationsTask);

                // Oblicz aktywność
                CalculateActivity();

                // Załaduj powiadomienia
                LoadNotifications();

                _logger.LogInformation("Dashboard statistics loaded successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading dashboard statistics");
                AddNotification("Błąd podczas ładowania statystyk", NotificationType.Error);
                
                // Załaduj mockowe dane w przypadku błędu
                LoadMockData();
            }
            finally
            {
                IsLoading = false;
            }
        }

        private async Task LoadTeamsCountAsync()
        {
            try
            {
                // Używamy GetActiveTeamsAsync zamiast GetAllAsync z filtrowaniem
                var teams = await _teamService.GetActiveTeamsAsync(forceRefresh: false);
                ActiveTeamsCount = teams?.Count() ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading teams count");
                ActiveTeamsCount = 4; // Fallback value
            }
        }

        private async Task LoadUsersCountAsync()
        {
            try
            {
                // Używamy GetAllActiveUsersAsync
                var users = await _userService.GetAllActiveUsersAsync(forceRefresh: false);
                TotalUsersCount = users?.Count() ?? 0;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading users count");
                TotalUsersCount = 25; // Fallback value
            }
        }

        private async Task LoadRecentOperationsAsync()
        {
            try
            {
                // Używamy GetHistoryByFilterAsync z filtrem na dzisiaj
                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                
                var operations = await _operationHistoryService.GetHistoryByFilterAsync(
                    startDate: today,
                    endDate: tomorrow,
                    page: 1,
                    pageSize: 10);
                
                RecentOperations.Clear();
                foreach (var op in operations.Take(10))
                {
                    RecentOperations.Add(new OperationHistoryItem
                    {
                        Id = op.Id.ToString(),
                        OperationType = op.Type.ToString(),
                        ExecutedAt = op.StartedAt,
                        ExecutedBy = op.CreatedBy ?? "System",
                        IsSuccess = op.Status == Core.Enums.OperationStatus.Completed,
                        Status = op.Status == Core.Enums.OperationStatus.Completed ? "Sukces" : 
                                op.Status == Core.Enums.OperationStatus.Failed ? "Błąd" : "W trakcie",
                        Details = op.OperationDetails ?? string.Empty,
                        ErrorMessage = op.ErrorMessage ?? string.Empty
                    });
                }

                // Oblicz operacje z dzisiaj
                TodayOperationsCount = operations.Count();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading recent operations");
                TodayOperationsCount = 0;
                
                // Dodaj mockowe dane dla demonstracji
                LoadMockOperations();
            }
        }

        private void LoadMockData()
        {
            ActiveTeamsCount = 4;
            TotalUsersCount = 25;
            TodayOperationsCount = 3;
            LoadMockOperations();
        }

        private void LoadMockOperations()
        {
            RecentOperations.Clear();
            var mockOperations = new[]
            {
                new OperationHistoryItem
                {
                    Id = "1",
                    OperationType = "Utworzenie zespołu",
                    ExecutedAt = DateTime.Now.AddMinutes(-15),
                    ExecutedBy = "Jan Kowalski",
                    IsSuccess = true,
                    Status = "Sukces"
                },
                new OperationHistoryItem
                {
                    Id = "2",
                    OperationType = "Dodanie użytkownika",
                    ExecutedAt = DateTime.Now.AddMinutes(-32),
                    ExecutedBy = "Anna Nowak",
                    IsSuccess = true,
                    Status = "Sukces"
                },
                new OperationHistoryItem
                {
                    Id = "3",
                    OperationType = "Archiwizacja zespołu",
                    ExecutedAt = DateTime.Now.AddHours(-1),
                    ExecutedBy = "Piotr Wiśniewski",
                    IsSuccess = false,
                    Status = "Błąd"
                }
            };

            foreach (var op in mockOperations)
            {
                RecentOperations.Add(op);
            }

            TodayOperationsCount = mockOperations.Count(o => o.ExecutedAt.Date == DateTime.Today);
        }

        private void CalculateActivity()
        {
            // Przykładowa logika obliczania aktywności
            var yesterday = DateTime.Today.AddDays(-1);
            var yesterdayOps = RecentOperations.Count(o => o.ExecutedAt.Date == yesterday);
            
            if (yesterdayOps > 0)
            {
                var percentChange = ((double)TodayOperationsCount / yesterdayOps) * 100;
                ActivityPercentage = Math.Min(percentChange, 100);
                ActivityDescription = $"{percentChange:F0}% więcej niż wczoraj";
            }
            else
            {
                ActivityPercentage = TodayOperationsCount > 0 ? 100 : 0;
                ActivityDescription = TodayOperationsCount > 0 ? "Pierwsze operacje dzisiaj!" : "Brak aktywności";
            }
        }

        private void LoadNotifications()
        {
            Notifications.Clear();
            
            // Przykładowe powiadomienia
            if (TodayOperationsCount == 0)
            {
                AddNotification("Brak operacji wykonanych dzisiaj", NotificationType.Warning);
            }
            
            if (ActiveTeamsCount == 0)
            {
                AddNotification("Nie masz aktywnych zespołów", NotificationType.Info);
            }
            
            // Sprawdź ostatnią synchronizację
            var lastSync = RecentOperations.FirstOrDefault(o => o.OperationType.Contains("Synchronizacja"));
            if (lastSync == null || (DateTime.Now - lastSync.ExecutedAt).TotalHours > 24)
            {
                AddNotification("Synchronizacja nie była wykonywana od ponad 24h", NotificationType.Warning);
            }
        }

        private void AddNotification(string message, NotificationType type)
        {
            Notifications.Add(new NotificationItem
            {
                Message = message,
                Type = type,
                Timestamp = DateTime.Now
            });
        }

        private void NavigateToCreateTeam()
        {
            _logger.LogInformation("Navigating to Create Team");
            // TODO: Implementacja nawigacji w przyszłych etapach
        }

        private void NavigateToManageUsers()
        {
            _logger.LogInformation("Navigating to Manage Users");
            // TODO: Implementacja nawigacji w przyszłych etapach
        }

        private void NavigateToReports()
        {
            _logger.LogInformation("Navigating to Reports");
            // TODO: Implementacja nawigacji w przyszłych etapach
        }

        private void NavigateToOperationHistory()
        {
            _logger.LogInformation("Navigating to Operation History");
            // TODO: Implementacja nawigacji w przyszłych etapach
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value)) return false;
            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }

    /// <summary>
    /// Model dla elementu historii operacji
    /// </summary>
    public class OperationHistoryItem
    {
        public string Id { get; set; } = string.Empty;
        public string OperationType { get; set; } = string.Empty;
        public DateTime ExecutedAt { get; set; }
        public string ExecutedBy { get; set; } = string.Empty;
        public bool IsSuccess { get; set; }
        public string Status { get; set; } = string.Empty;
        public string Details { get; set; } = string.Empty;
        public string ErrorMessage { get; set; } = string.Empty;
    }

    public class NotificationItem
    {
        public string Message { get; set; } = string.Empty;
        public NotificationType Type { get; set; }
        public DateTime Timestamp { get; set; }
    }

    public enum NotificationType
    {
        Info,
        Warning,
        Error,
        Success
    }
} 