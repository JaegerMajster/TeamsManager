using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace TeamsManager.UI.ViewModels.Teams
{
    /// <summary>
    /// Model reprezentujący placeholder token w edytorze szablonów zespołów.
    /// Zawiera informacje o placeholderze, jego opisie, przykładzie i aktualnej wartości.
    /// </summary>
    public class PlaceholderToken : INotifyPropertyChanged
    {
        private string _name = string.Empty;
        private string _description = string.Empty;
        private string _exampleValue = string.Empty;
        private bool _isRequired;
        private string _currentValue = string.Empty;

        /// <summary>
        /// Nazwa placeholdera (bez nawiasów klamrowych)
        /// </summary>
        public string Name 
        { 
            get => _name; 
            set 
            { 
                if (SetProperty(ref _name, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                }
            }
        }

        /// <summary>
        /// Opis placeholdera dla użytkownika
        /// </summary>
        public string Description 
        { 
            get => _description; 
            set => SetProperty(ref _description, value);
        }

        /// <summary>
        /// Przykładowa wartość placeholdera
        /// </summary>
        public string ExampleValue 
        { 
            get => _exampleValue; 
            set => SetProperty(ref _exampleValue, value);
        }

        /// <summary>
        /// Czy placeholder jest wymagany
        /// </summary>
        public bool IsRequired 
        { 
            get => _isRequired; 
            set => SetProperty(ref _isRequired, value);
        }

        /// <summary>
        /// Aktualna wartość placeholdera ustawiona przez użytkownika
        /// </summary>
        public string CurrentValue 
        { 
            get => _currentValue; 
            set 
            { 
                if (SetProperty(ref _currentValue, value))
                {
                    ValueChanged?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        /// <summary>
        /// Nazwa placeholdera z nawiasami klamrowymi (do wyświetlania w UI)
        /// </summary>
        public string DisplayName => $"{{{Name}}}";

        /// <summary>
        /// Zdarzenie wywoływane gdy wartość placeholdera się zmienia
        /// </summary>
        public event EventHandler? ValueChanged;

        /// <summary>
        /// Implementacja INotifyPropertyChanged
        /// </summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// Pomocnicza metoda do ustawiania właściwości z powiadomieniem o zmianie
        /// </summary>
        protected bool SetProperty<T>(ref T storage, T value, [CallerMemberName] string? propertyName = null)
        {
            if (Equals(storage, value))
                return false;

            storage = value;
            OnPropertyChanged(propertyName);
            return true;
        }

        /// <summary>
        /// Wywoływanie zdarzenia PropertyChanged
        /// </summary>
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
} 