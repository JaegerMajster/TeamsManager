using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Input;

namespace TeamsManager.UI.ViewModels
{
    /// <summary>
    /// ViewModel dla Dashboard'a głównego
    /// Obsługuje logikę biznesową i powiązanie danych dla interfejsu użytkownika
    /// </summary>
    public class DashboardViewModel : INotifyPropertyChanged
    {
        #region Private Fields

        private int _activeTeamsCount;
        private int _totalUsersCount;
        private int _todayOperationsCount;
        private double _activityPercentage;
        private bool _isLoading;
        private string _errorMessage;
        private List<OperationHistoryItem> _recentOperations;

        #endregion

        #region Public Properties

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
        /// Komunikat błędu (jeśli wystąpił)
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Lista najnowszych operacji
        /// </summary>
        public List<OperationHistoryItem> RecentOperations
        {
            get => _recentOperations;
            set => SetProperty(ref _recentOperations, value);
        }

        #endregion

        #region Commands

        public ICommand RefreshCommand { get; }
        public ICommand CreateTeamCommand { get; }
        public ICommand ManageUsersCommand { get; }
        public ICommand GenerateReportsCommand { get; }

        #endregion

        #region Constructor

        public DashboardViewModel()
        {
            // Inicjalizuj komendy
            RefreshCommand = new RelayCommand(async () => await LoadStatisticsAsync());
            CreateTeamCommand = new RelayCommand(() => CreateTeam());
            ManageUsersCommand = new RelayCommand(() => ManageUsers());
            GenerateReportsCommand = new RelayCommand(() => GenerateReports());

            // Ustaw domyślne wartości
            ActiveTeamsCount = 0;
            TotalUsersCount = 0;
            TodayOperationsCount = 0;
            ActivityPercentage = 0;
            RecentOperations = new List<OperationHistoryItem>();
        }

        #endregion

        #region Public Methods

        /// <summary>
        /// Ładuje statystyki Dashboard'a
        /// </summary>
        public async Task LoadStatisticsAsync()
        {
            try
            {
                IsLoading = true;
                ErrorMessage = null;

                // TODO: Zastąp wywołaniami rzeczywistego API
                await Task.Delay(1000); // Symulacja opóźnienia API

                // Przykładowe dane (zastąp rzeczywistymi wywołaniami API)
                ActiveTeamsCount = await GetActiveTeamsCountAsync();
                TotalUsersCount = await GetTotalUsersCountAsync();
                TodayOperationsCount = await GetTodayOperationsCountAsync();
                ActivityPercentage = await GetActivityPercentageAsync();

                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Statystyki załadowane: Teams={ActiveTeamsCount}, Users={TotalUsersCount}, Operations={TodayOperationsCount}");
            }
            catch (Exception ex)
            {
                ErrorMessage = $"Błąd podczas ładowania statystyk: {ex.Message}";
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Błąd: {ErrorMessage}");
            }
            finally
            {
                IsLoading = false;
            }
        }

        /// <summary>
        /// Pobiera najnowsze operacje
        /// </summary>
        public async Task<List<OperationHistoryItem>> GetRecentOperationsAsync()
        {
            try
            {
                // TODO: Zastąp wywołaniem rzeczywistego API
                await Task.Delay(500);

                var operations = new List<OperationHistoryItem>
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
                    },
                    new OperationHistoryItem
                    {
                        Id = "4",
                        OperationType = "Generowanie raportu",
                        ExecutedAt = DateTime.Now.AddHours(-2),
                        ExecutedBy = "Maria Kowalczyk",
                        IsSuccess = true,
                        Status = "Sukces"
                    },
                    new OperationHistoryItem
                    {
                        Id = "5",
                        OperationType = "Import użytkowników CSV",
                        ExecutedAt = DateTime.Now.AddHours(-3),
                        ExecutedBy = "Tomasz Lewandowski",
                        IsSuccess = true,
                        Status = "Sukces"
                    }
                };

                RecentOperations = operations;
                return operations;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[DashboardViewModel] Błąd podczas ładowania operacji: {ex.Message}");
                return new List<OperationHistoryItem>();
            }
        }

        #endregion

        #region Private Methods

        private async Task<int> GetActiveTeamsCountAsync()
        {
            // TODO: Wywołaj API endpoint /api/v1.0/Teams
            await Task.Delay(200);
            return new Random().Next(15, 45); // Symulacja danych
        }

        private async Task<int> GetTotalUsersCountAsync()
        {
            // TODO: Wywołaj API endpoint /api/v1.0/Users
            await Task.Delay(200);
            return new Random().Next(150, 300); // Symulacja danych
        }

        private async Task<int> GetTodayOperationsCountAsync()
        {
            // TODO: Wywołaj API endpoint /api/v1.0/OperationHistories z filtrem daty
            await Task.Delay(200);
            return new Random().Next(5, 25); // Symulacja danych
        }

        private async Task<double> GetActivityPercentageAsync()
        {
            // TODO: Oblicz na podstawie danych z API
            await Task.Delay(200);
            return new Random().Next(60, 95); // Symulacja danych
        }

        private void CreateTeam()
        {
            // TODO: Implementuj otwieranie okna tworzenia zespołu
            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] CreateTeam clicked");
        }

        private void ManageUsers()
        {
            // TODO: Implementuj otwieranie okna zarządzania użytkownikami
            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] ManageUsers clicked");
        }

        private void GenerateReports()
        {
            // TODO: Implementuj otwieranie okna generowania raportów
            System.Diagnostics.Debug.WriteLine("[DashboardViewModel] GenerateReports clicked");
        }

        #endregion

        #region INotifyPropertyChanged Implementation

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
        {
            if (Equals(field, value)) return false;
            field = value;
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
        public string Id { get; set; }
        public string OperationType { get; set; }
        public DateTime ExecutedAt { get; set; }
        public string ExecutedBy { get; set; }
        public bool IsSuccess { get; set; }
        public string Status { get; set; }
        public string Details { get; set; }
        public string ErrorMessage { get; set; }
    }
} 