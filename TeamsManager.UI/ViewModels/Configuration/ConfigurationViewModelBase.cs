using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;

namespace TeamsManager.UI.ViewModels.Configuration
{
    /// <summary>
    /// Bazowa klasa dla wszystkich ViewModels konfiguracji
    /// Implementuje INotifyPropertyChanged i wspólną logikę
    /// </summary>
    public abstract class ConfigurationViewModelBase : INotifyPropertyChanged
    {
        private bool _isValid;
        private bool _isBusy;
        private string _busyMessage = string.Empty;

        /// <summary>
        /// Czy formularz jest prawidłowo wypełniony
        /// </summary>
        public bool IsValid
        {
            get => _isValid;
            set
            {
                _isValid = value;
                OnPropertyChanged();
                // Powiadom też o zmianie CanExecute dla komend
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Czy ViewModel jest zajęty (np. zapisuje dane)
        /// </summary>
        public bool IsBusy
        {
            get => _isBusy;
            set
            {
                _isBusy = value;
                OnPropertyChanged();
                CommandManager.InvalidateRequerySuggested();
            }
        }

        /// <summary>
        /// Komunikat wyświetlany gdy IsBusy = true
        /// </summary>
        public string BusyMessage
        {
            get => _busyMessage;
            set
            {
                _busyMessage = value;
                OnPropertyChanged();
            }
        }

        /// <summary>
        /// Lista błędów walidacji
        /// </summary>
        public ObservableCollection<string> ValidationErrors { get; } = new ObservableCollection<string>();

        /// <summary>
        /// Komenda przejścia do następnego kroku
        /// </summary>
        public ICommand? NextCommand { get; protected set; }

        /// <summary>
        /// Komenda powrotu do poprzedniego kroku
        /// </summary>
        public ICommand? BackCommand { get; protected set; }

        /// <summary>
        /// Komenda anulowania całego procesu
        /// </summary>
        public ICommand? CancelCommand { get; protected set; }

        /// <summary>
        /// Metoda walidacji - do nadpisania w klasach pochodnych
        /// </summary>
        protected virtual void Validate()
        {
            ValidationErrors.Clear();
            // Implementacja w klasach pochodnych
        }

        /// <summary>
        /// Pomocnicza metoda do walidacji GUID
        /// </summary>
        protected bool IsValidGuid(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            // Akceptuj też "common" i "organizations" jako specjalne wartości tenant
            if (value == "common" || value == "organizations")
                return true;

            return Guid.TryParse(value, out _);
        }

        /// <summary>
        /// Pomocnicza metoda do walidacji URI
        /// </summary>
        protected bool IsValidUri(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return Uri.TryCreate(value, UriKind.Absolute, out var uri) &&
                   (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        /// <summary>
        /// Pomocnicza metoda do walidacji API Scope
        /// Format: api://[guid]/scope_name
        /// </summary>
        protected bool IsValidApiScope(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            if (!value.StartsWith("api://"))
                return false;

            var parts = value.Split('/');
            return parts.Length >= 3; // api:// + guid + scope_name
        }

        /// <summary>
        /// Pomocnicza metoda do walidacji API Audience
        /// Format: api://[guid]
        /// </summary>
        protected bool IsValidApiAudience(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return false;

            return value.StartsWith("api://") && value.Length > 6;
        }

        /// <summary>
        /// Dodaje błąd walidacji i ustawia IsValid na false
        /// </summary>
        protected void AddValidationError(string error)
        {
            ValidationErrors.Add(error);
            IsValid = false;
        }

        /// <summary>
        /// Czyści błędy walidacji
        /// </summary>
        protected void ClearValidationErrors()
        {
            ValidationErrors.Clear();
            IsValid = true;
        }

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        /// <summary>
        /// Ustawia wartość właściwości i wywołuje PropertyChanged jeśli wartość się zmieniła
        /// </summary>
        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(field, value))
                return false;

            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        #endregion
    }
}