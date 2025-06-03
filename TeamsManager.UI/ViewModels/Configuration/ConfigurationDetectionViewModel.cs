using System;
using System.Windows;
using System.Windows.Input;
using TeamsManager.UI.Models.Configuration;

namespace TeamsManager.UI.ViewModels.Configuration
{
    /// <summary>
    /// ViewModel dla okna wykrywania braku konfiguracji
    /// </summary>
    public class ConfigurationDetectionViewModel : ConfigurationViewModelBase
    {
        private string _errorMessage = "Nie znaleziono plików konfiguracyjnych.";
        private readonly ConfigurationValidationResult _validationResult;
        private readonly Window _window;

        /// <summary>
        /// Komunikat błędu do wyświetlenia
        /// </summary>
        public string ErrorMessage
        {
            get => _errorMessage;
            set => SetProperty(ref _errorMessage, value);
        }

        /// <summary>
        /// Komenda rozpoczęcia konfiguracji
        /// </summary>
        public ICommand StartConfigurationCommand { get; }

        /// <summary>
        /// Komenda zamknięcia okna/aplikacji
        /// </summary>
        public ICommand CloseCommand { get; }

        public ConfigurationDetectionViewModel(Window window, ConfigurationValidationResult validationResult)
        {
            _window = window ?? throw new ArgumentNullException(nameof(window));
            _validationResult = validationResult ?? throw new ArgumentNullException(nameof(validationResult));

            // Ustaw komunikat błędu na podstawie wyniku walidacji
            SetErrorMessage();

            // Inicjalizuj komendy
            StartConfigurationCommand = new RelayCommand(StartConfiguration);
            CloseCommand = new RelayCommand(Close);
        }

        /// <summary>
        /// Ustawia komunikat błędu na podstawie statusu walidacji
        /// </summary>
        private void SetErrorMessage()
        {
            ErrorMessage = _validationResult.Status switch
            {
                ConfigurationStatus.Missing => "Nie znaleziono plików konfiguracyjnych.",
                ConfigurationStatus.Invalid => "Konfiguracja zawiera błędy i wymaga poprawy.",
                ConfigurationStatus.ConnectionError => "Nie można połączyć się z usługami Microsoft. Sprawdź połączenie internetowe.",
                ConfigurationStatus.Unauthorized => "Brak uprawnień do zasobów. Wymagana ponowna konfiguracja.",
                _ => _validationResult.DetailedMessage ?? "Wystąpił nieznany problem z konfiguracją."
            };

            // Jeśli są szczegółowe błędy, dodaj pierwszy z nich
            if (_validationResult.Errors.Count > 0)
            {
                ErrorMessage += $"\n\nSzczegóły: {_validationResult.Errors[0]}";
            }
        }

        /// <summary>
        /// Rozpoczyna proces konfiguracji
        /// </summary>
        private void StartConfiguration()
        {
            try
            {
                // Ustaw DialogResult na true, co oznacza że użytkownik chce kontynuować
                _window.DialogResult = true;
                _window.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Błąd podczas rozpoczynania konfiguracji: {ex.Message}",
                    "Błąd",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error
                );
            }
        }

        /// <summary>
        /// Zamyka okno/aplikację
        /// </summary>
        private void Close()
        {
            try
            {
                // Ustaw DialogResult na false, co oznacza anulowanie
                _window.DialogResult = false;
                _window.Close();
            }
            catch (Exception ex)
            {
                // Jeśli nie możemy ustawić DialogResult (np. okno nie jest dialogiem), po prostu zamknij
                _window.Close();
            }
        }
    }
}