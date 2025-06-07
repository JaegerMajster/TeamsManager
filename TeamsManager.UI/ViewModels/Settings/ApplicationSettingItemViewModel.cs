using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Windows.Input;
using TeamsManager.Core.Models;
using TeamsManager.Core.Enums;
using TeamsManager.UI.ViewModels;

namespace TeamsManager.UI.ViewModels.Settings
{
    /// <summary>
    /// ViewModel dla pojedynczego ustawienia aplikacji
    /// Obsługuje edycję, walidację i konwersję typów
    /// </summary>
    public class ApplicationSettingItemViewModel : INotifyPropertyChanged, IDataErrorInfo
    {
        private ApplicationSetting _setting;
        private string _editableValue;
        private bool _isEditing;
        private bool _hasChanges;
        private bool _isValid = true;
        private string _validationError;

        public ApplicationSettingItemViewModel(ApplicationSetting setting)
        {
            _setting = setting ?? throw new ArgumentNullException(nameof(setting));
            _editableValue = setting.Value;
            
            // Inicjalizacja komend
            EditCommand = new RelayCommand(_ => StartEdit(), _ => !IsEditing);
            SaveCommand = new RelayCommand(_ => Save(), _ => CanSave());
            CancelCommand = new RelayCommand(_ => CancelEdit(), _ => IsEditing);
            ResetToDefaultCommand = new RelayCommand(_ => ResetToDefault(), _ => HasDefault);
            IncrementCommand = new RelayCommand(_ => IncrementValue(), _ => CanIncrement());
            DecrementCommand = new RelayCommand(_ => DecrementValue(), _ => CanDecrement());
        }

        #region Properties

        public string Id => _setting.Id;
        public string Key => _setting.Key;
        public string Description => _setting.Description;
        public string Category => _setting.Category;
        public SettingType Type => _setting.Type;
        public bool IsRequired => _setting.IsRequired;
        public bool IsVisible => _setting.IsVisible;
        public int DisplayOrder => _setting.DisplayOrder;
        public bool HasDefault => !string.IsNullOrEmpty(_setting.DefaultValue);
        public string DefaultValue => _setting.DefaultValue ?? string.Empty;

        public string Value
        {
            get => _setting.Value;
            set
            {
                if (_setting.Value != value)
                {
                    _setting.Value = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(DisplayValue));
                }
            }
        }

        public string EditableValue
        {
            get => _editableValue;
            set
            {
                if (_editableValue != value)
                {
                    _editableValue = value;
                    OnPropertyChanged();
                    Validate();
                    HasChanges = _editableValue != Value;
                }
            }
        }

        public bool IsEditing
        {
            get => _isEditing;
            private set
            {
                if (_isEditing != value)
                {
                    _isEditing = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool HasChanges
        {
            get => _hasChanges;
            private set
            {
                if (_hasChanges != value)
                {
                    _hasChanges = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool IsValid
        {
            get => _isValid;
            private set
            {
                if (_isValid != value)
                {
                    _isValid = value;
                    OnPropertyChanged();
                }
            }
        }

        public string ValidationError
        {
            get => _validationError;
            private set
            {
                if (_validationError != value)
                {
                    _validationError = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// Wartość sformatowana do wyświetlenia (np. dla Boolean: "Tak"/"Nie")
        /// </summary>
        public string DisplayValue
        {
            get
            {
                return Type switch
                {
                    SettingType.Boolean => _setting.GetBoolValue() ? "Tak" : "Nie",
                    SettingType.DateTime => _setting.GetDateTimeValue()?.ToString("yyyy-MM-dd HH:mm:ss") ?? "—",
                    SettingType.Decimal => _setting.GetDecimalValue().ToString("N2"),
                    SettingType.Integer => _setting.GetIntValue().ToString("N0"),
                    SettingType.Json => "[JSON Object]",
                    _ => _setting.Value
                };
            }
        }

        #endregion

        #region Commands

        public ICommand EditCommand { get; }
        public ICommand SaveCommand { get; }
        public ICommand CancelCommand { get; }
        public ICommand ResetToDefaultCommand { get; }
        public ICommand IncrementCommand { get; }
        public ICommand DecrementCommand { get; }

        #endregion

        #region Methods

        private void StartEdit()
        {
            IsEditing = true;
            EditableValue = Value;
            HasChanges = false;
        }

        private bool CanSave()
        {
            return IsEditing && HasChanges && IsValid;
        }

        private void Save()
        {
            if (!IsValid) return;

            Value = EditableValue;
            IsEditing = false;
            HasChanges = false;
            
            // Wyzwolenie zdarzenia zapisania (obsłużone przez parent ViewModel)
            SettingSaved?.Invoke(this, EventArgs.Empty);
        }

        private void CancelEdit()
        {
            EditableValue = Value;
            IsEditing = false;
            HasChanges = false;
            ValidationError = null;
            IsValid = true;
        }

        private void ResetToDefault()
        {
            if (HasDefault)
            {
                EditableValue = DefaultValue;
                if (!IsEditing)
                {
                    Value = DefaultValue;
                    SettingSaved?.Invoke(this, EventArgs.Empty);
                }
            }
        }

        private bool CanIncrement()
        {
            return Type == SettingType.Integer || Type == SettingType.Decimal;
        }

        private bool CanDecrement()
        {
            return Type == SettingType.Integer || Type == SettingType.Decimal;
        }

        private void IncrementValue()
        {
            if (!CanIncrement()) return;

            if (Type == SettingType.Integer)
            {
                if (int.TryParse(EditableValue, out int currentValue))
                {
                    EditableValue = (currentValue + 1).ToString();
                }
                else
                {
                    EditableValue = "1";
                }
            }
            else if (Type == SettingType.Decimal)
            {
                if (decimal.TryParse(EditableValue, out decimal currentValue))
                {
                    EditableValue = (currentValue + 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    EditableValue = "1.0";
                }
            }
        }

        private void DecrementValue()
        {
            if (!CanDecrement()) return;

            if (Type == SettingType.Integer)
            {
                if (int.TryParse(EditableValue, out int currentValue))
                {
                    EditableValue = (currentValue - 1).ToString();
                }
                else
                {
                    EditableValue = "-1";
                }
            }
            else if (Type == SettingType.Decimal)
            {
                if (decimal.TryParse(EditableValue, out decimal currentValue))
                {
                    EditableValue = (currentValue - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else
                {
                    EditableValue = "-1.0";
                }
            }
        }

        private void Validate()
        {
            ValidationError = null;
            IsValid = true;

            // Sprawdzenie czy wymagane
            if (IsRequired && string.IsNullOrWhiteSpace(EditableValue))
            {
                ValidationError = "To pole jest wymagane";
                IsValid = false;
                return;
            }

            // Walidacja typu
            var typeValidation = ValidateType();
            if (!string.IsNullOrEmpty(typeValidation))
            {
                ValidationError = typeValidation;
                IsValid = false;
                return;
            }

            // Walidacja wzorca (regex)
            if (!string.IsNullOrEmpty(_setting.ValidationPattern) && !string.IsNullOrWhiteSpace(EditableValue))
            {
                try
                {
                    var regex = new Regex(_setting.ValidationPattern);
                    if (!regex.IsMatch(EditableValue))
                    {
                        ValidationError = _setting.ValidationMessage ?? "Wartość nie spełnia wymaganego formatu";
                        IsValid = false;
                    }
                }
                catch
                {
                    // Błędny regex - ignorujemy walidację
                }
            }
        }

        private string ValidateType()
        {
            if (string.IsNullOrWhiteSpace(EditableValue))
                return null;

            return Type switch
            {
                SettingType.Integer => !int.TryParse(EditableValue, out _) ? "Wartość musi być liczbą całkowitą" : null,
                SettingType.Decimal => !decimal.TryParse(EditableValue, System.Globalization.NumberStyles.Any, 
                    System.Globalization.CultureInfo.InvariantCulture, out _) ? "Wartość musi być liczbą dziesiętną" : null,
                SettingType.Boolean => !bool.TryParse(EditableValue, out _) ? "Wartość musi być 'true' lub 'false'" : null,
                SettingType.DateTime => !DateTime.TryParse(EditableValue, out _) ? "Wartość musi być prawidłową datą" : null,
                SettingType.Json => !IsValidJson(EditableValue) ? "Wartość musi być prawidłowym JSON" : null,
                _ => null
            };
        }

        private bool IsValidJson(string value)
        {
            try
            {
                System.Text.Json.JsonDocument.Parse(value);
                return true;
            }
            catch
            {
                return false;
            }
        }

        public ApplicationSetting GetModel() => _setting;

        #endregion

        #region IDataErrorInfo

        public string Error => ValidationError;

        public string this[string columnName]
        {
            get
            {
                if (columnName == nameof(EditableValue))
                    return ValidationError;
                return null;
            }
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler SettingSaved;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion
    }
} 