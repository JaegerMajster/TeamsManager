using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using TeamsManager.UI.ViewModels;
using Microsoft.Extensions.Logging;

namespace TeamsManager.UI.Views
{
    /// <summary>
    /// Dashboard główny aplikacji Teams Manager
    /// Ekran 2: 📊 DASHBOARD GŁÓWNY
    /// </summary>
    public partial class DashboardWindow : Window
    {
        private readonly DashboardViewModel _viewModel;
        private readonly DispatcherTimer _refreshTimer;
        private readonly DispatcherTimer _uptimeTimer;
        private DateTime _applicationStartTime;

        public DashboardWindow()
        {
            InitializeComponent();
            
            _applicationStartTime = DateTime.Now;
            _viewModel = new DashboardViewModel();
            DataContext = _viewModel;

            // Timer do automatycznego odświeżania danych co 5 minut
            _refreshTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(5)
            };
            _refreshTimer.Tick += RefreshTimer_Tick;

            // Timer do aktualizacji uptime co minutę
            _uptimeTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMinutes(1)
            };
            _uptimeTimer.Tick += UptimeTimer_Tick;

            Loaded += DashboardWindow_Loaded;
            Unloaded += DashboardWindow_Unloaded;
        }

        private async void DashboardWindow_Loaded(object sender, RoutedEventArgs e)
        {
            // Animacja wejścia
            var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromSeconds(0.5));
            BeginAnimation(OpacityProperty, fadeIn);

            // Uruchom timery
            _refreshTimer.Start();
            _uptimeTimer.Start();

            // Załaduj początkowe dane
            await LoadDashboardDataAsync();
        }

        private void DashboardWindow_Unloaded(object sender, RoutedEventArgs e)
        {
            // Zatrzymaj timery
            _refreshTimer?.Stop();
            _uptimeTimer?.Stop();
        }

        private async void RefreshTimer_Tick(object sender, EventArgs e)
        {
            await LoadDashboardDataAsync();
        }

        private void UptimeTimer_Tick(object sender, EventArgs e)
        {
            UpdateUptimeDisplay();
        }

        private async Task LoadDashboardDataAsync()
        {
            try
            {
                ShowLoading(true);

                // Załaduj dane statystyk
                await _viewModel.LoadStatisticsAsync();

                // Aktualizuj interfejs
                UpdateStatisticsDisplay();
                UpdateUptimeDisplay();

                // Załaduj najnowsze operacje
                await LoadRecentOperationsAsync();

                // Aktualizuj powitanie
                UpdateWelcomeMessage();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Błąd podczas ładowania danych: {ex.Message}");
                ShowErrorNotification("Błąd podczas ładowania danych Dashboard'a");
            }
            finally
            {
                ShowLoading(false);
            }
        }

        private void UpdateStatisticsDisplay()
        {
            // Aktualizuj liczniki z animacją
            AnimateCounterUpdate(ActiveTeamsCount, _viewModel.ActiveTeamsCount);
            AnimateCounterUpdate(TotalUsersCount, _viewModel.TotalUsersCount);
            AnimateCounterUpdate(TodayOperationsCount, _viewModel.TodayOperationsCount);

            // Aktualizuj progress bar aktywności
            var progressAnimation = new DoubleAnimation(ActivityProgress.Value, _viewModel.ActivityPercentage, TimeSpan.FromSeconds(1));
            ActivityProgress.BeginAnimation(System.Windows.Controls.Primitives.RangeBase.ValueProperty, progressAnimation);
        }

        private void AnimateCounterUpdate(TextBlock textBlock, int targetValue)
        {
            if (int.TryParse(textBlock.Text, out int currentValue))
            {
                // Użyj prostej animacji z DispatcherTimer
                var steps = 20;
                var increment = (targetValue - currentValue) / (double)steps;
                var currentStep = 0;
                
                var timer = new DispatcherTimer
                {
                    Interval = TimeSpan.FromMilliseconds(50)
                };
                
                timer.Tick += (s, e) =>
                {
                    currentStep++;
                    var newValue = (int)(currentValue + (increment * currentStep));
                    
                    if (currentStep >= steps)
                    {
                        textBlock.Text = targetValue.ToString();
                        timer.Stop();
                    }
                    else
                    {
                        textBlock.Text = newValue.ToString();
                    }
                };
                
                timer.Start();
            }
            else
            {
                textBlock.Text = targetValue.ToString();
            }
        }

        private async Task LoadRecentOperationsAsync()
        {
            try
            {
                var operations = await _viewModel.GetRecentOperationsAsync();
                RecentOperationsDataGrid.ItemsSource = operations;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Dashboard] Błąd podczas ładowania operacji: {ex.Message}");
            }
        }

        private void UpdateWelcomeMessage()
        {
            var timeOfDay = DateTime.Now.Hour switch
            {
                >= 5 and < 12 => "Dzień dobry",
                >= 12 and < 17 => "Dzień dobry",
                >= 17 and < 22 => "Dobry wieczór",
                _ => "Dobranoc"
            };

            WelcomeText.Text = $"{timeOfDay}! Witaj w Teams Manager - zarządzaj zespołami Microsoft Teams z łatwością";
        }

        private void UpdateUptimeDisplay()
        {
            var uptime = DateTime.Now - _applicationStartTime;
            var uptimeText = uptime.Days > 0 
                ? $"Uptime: {uptime.Days}d {uptime.Hours}h {uptime.Minutes}m"
                : $"Uptime: {uptime.Hours}h {uptime.Minutes}m";
            
            UptimeText.Text = uptimeText;
        }

        #region Event Handlers

        private async void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            await LoadDashboardDataAsync();
            ShowSuccessNotification("Dane zostały odświeżone");
        }

        private void UserMenuButton_Click(object sender, RoutedEventArgs e)
        {
            // TODO: Implementuj menu użytkownika
            ShowInfoNotification("Menu użytkownika - w przygotowaniu");
        }

        private void CreateTeamButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Otwórz okno tworzenia zespołu
                ShowInfoNotification("Funkcja tworzenia zespołu - w przygotowaniu");
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Błąd podczas otwierania okna tworzenia zespołu: {ex.Message}");
            }
        }

        private void ManageUsersButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Otwórz okno zarządzania użytkownikami
                ShowInfoNotification("Funkcja zarządzania użytkownikami - w przygotowaniu");
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Błąd podczas otwierania okna zarządzania użytkownikami: {ex.Message}");
            }
        }

        private void ReportsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Otwórz okno raportów
                ShowInfoNotification("Funkcja generowania raportów - w przygotowaniu");
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Błąd podczas otwierania okna raportów: {ex.Message}");
            }
        }

        private void ViewAllOperationsButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // TODO: Otwórz okno pełnej historii operacji
                ShowInfoNotification("Pełna historia operacji - w przygotowaniu");
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Błąd podczas otwierania historii operacji: {ex.Message}");
            }
        }

        private void FloatingActionButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Szybka akcja - domyślnie tworzenie zespołu
                CreateTeamButton_Click(sender, e);
            }
            catch (Exception ex)
            {
                ShowErrorNotification($"Błąd podczas wykonywania szybkiej akcji: {ex.Message}");
            }
        }

        #endregion

        #region UI Helpers

        private void ShowLoading(bool show)
        {
            if (show)
            {
                LoadingOverlay.Visibility = Visibility.Visible;
                var fadeIn = new DoubleAnimation(0, 0.95, TimeSpan.FromSeconds(0.2));
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeIn);
            }
            else
            {
                var fadeOut = new DoubleAnimation(0.95, 0, TimeSpan.FromSeconds(0.2));
                fadeOut.Completed += (s, e) => LoadingOverlay.Visibility = Visibility.Collapsed;
                LoadingOverlay.BeginAnimation(OpacityProperty, fadeOut);
            }
        }

        private void ShowSuccessNotification(string message)
        {
            // TODO: Implementuj system powiadomień Material Design
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Sukces: {message}");
        }

        private void ShowErrorNotification(string message)
        {
            // TODO: Implementuj system powiadomień Material Design
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Błąd: {message}");
            MessageBox.Show(message, "Błąd", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ShowInfoNotification(string message)
        {
            // TODO: Implementuj system powiadomień Material Design
            System.Diagnostics.Debug.WriteLine($"[Dashboard] Info: {message}");
            MessageBox.Show(message, "Informacja", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        #endregion
    }
} 