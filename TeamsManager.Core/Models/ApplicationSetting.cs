using System;
using System.Text.Json;
using TeamsManager.Core.Enums;

namespace TeamsManager.Core.Models
{
    /// <summary>
    /// Ustawienia aplikacji przechowywane w bazie danych
    /// Umożliwia dynamiczną konfigurację aplikacji bez przebudowy kodu
    /// </summary>
    public class ApplicationSetting : BaseEntity
    {
        /// <summary>
        /// Unikalny klucz ustawienia (np. "DefaultSchoolType", "MaxBulkOperations")
        /// </summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>
        /// Wartość ustawienia jako string
        /// Interpretacja zależy od typu ustawienia
        /// </summary>
        public string Value { get; set; } = string.Empty;

        /// <summary>
        /// Opis ustawienia - co oznacza i jak wpływa na działanie aplikacji
        /// </summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Typ danych ustawienia - określa jak interpretować wartość
        /// </summary>
        public SettingType Type { get; set; } = SettingType.String;

        /// <summary>
        /// Kategoria ustawienia do grupowania w interfejsie
        /// Np. "General", "Teams", "Security", "PowerShell", "Notifications"
        /// </summary>
        public string Category { get; set; } = "General";

        /// <summary>
        /// Czy ustawienie jest wymagane do działania aplikacji
        /// </summary>
        public bool IsRequired { get; set; } = false;

        /// <summary>
        /// Czy ustawienie jest widoczne w interfejsie użytkownika
        /// Niektóre ustawienia mogą być tylko wewnętrzne
        /// </summary>
        public bool IsVisible { get; set; } = true;

        /// <summary>
        /// Wartość domyślna ustawienia
        /// </summary>
        public string? DefaultValue { get; set; }

        /// <summary>
        /// Wyrażenie regularne do walidacji wartości (opcjonalne)
        /// </summary>
        public string? ValidationPattern { get; set; }

        /// <summary>
        /// Komunikat błędu walidacji
        /// </summary>
        public string? ValidationMessage { get; set; }

        /// <summary>
        /// Kolejność wyświetlania w interfejsie użytkownika
        /// </summary>
        public int DisplayOrder { get; set; } = 0;

        // ===== METODY POMOCNICZE DO KONWERSJI TYPÓW =====

        /// <summary>
        /// Pobiera wartość jako string
        /// </summary>
        public string GetStringValue() => Value;

        /// <summary>
        /// Pobiera wartość jako liczbę całkowitą
        /// </summary>
        public int GetIntValue()
        {
            return int.TryParse(Value, out var result) ? result : 0;
        }

        /// <summary>
        /// Pobiera wartość jako wartość logiczną
        /// </summary>
        public bool GetBoolValue()
        {
            return bool.TryParse(Value, out var result) && result;
        }

        /// <summary>
        /// Pobiera wartość jako datę
        /// </summary>
        public DateTime? GetDateTimeValue()
        {
            return DateTime.TryParse(Value, out var result) ? result : null;
        }

        /// <summary>
        /// Pobiera wartość jako liczbę dziesiętną
        /// </summary>
        public decimal GetDecimalValue()
        {
            return decimal.TryParse(Value, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var result) ? result : 0m;
        }

        /// <summary>
        /// Pobiera wartość jako obiekt JSON
        /// </summary>
        public T? GetJsonValue<T>() where T : class
        {
            try
            {
                return JsonSerializer.Deserialize<T>(Value);
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// Ustawia wartość ze stringa
        /// </summary>
        public void SetValue(string value)
        {
            Value = value;
            Type = SettingType.String;
        }

        /// <summary>
        /// Ustawia wartość z liczby całkowitej
        /// </summary>
        public void SetValue(int value)
        {
            Value = value.ToString();
            Type = SettingType.Integer;
        }

        /// <summary>
        /// Ustawia wartość z wartości logicznej
        /// </summary>
        public void SetValue(bool value)
        {
            Value = value.ToString().ToLower();
            Type = SettingType.Boolean;
        }

        /// <summary>
        /// Ustawia wartość z daty
        /// </summary>
        public void SetValue(DateTime value)
        {
            Value = value.ToString("yyyy-MM-dd HH:mm:ss");
            Type = SettingType.DateTime;
        }

        /// <summary>
        /// Ustawia wartość z liczby dziesiętnej
        /// </summary>
        public void SetValue(decimal value)
        {
            Value = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
            Type = SettingType.Decimal;
        }

        /// <summary>
        /// Ustawia wartość z obiektu JSON
        /// </summary>
        public void SetValue<T>(T value) where T : class
        {
            Value = JsonSerializer.Serialize(value);
            Type = SettingType.Json;
        }

        /// <summary>
        /// Waliduje wartość ustawienia
        /// </summary>
        public bool IsValid()
        {
            // Sprawdź czy wartość jest wymagana
            if (IsRequired && string.IsNullOrWhiteSpace(Value))
                return false;

            // Sprawdź wzorzec walidacji
            if (!string.IsNullOrWhiteSpace(ValidationPattern) && !string.IsNullOrWhiteSpace(Value))
            {
                try
                {
                    var regex = new System.Text.RegularExpressions.Regex(ValidationPattern);
                    if (!regex.IsMatch(Value))
                        return false;
                }
                catch
                {
                    return false;
                }
            }

            // Sprawdź zgodność z typem
            return Type switch
            {
                SettingType.Integer => int.TryParse(Value, out _),
                SettingType.Boolean => bool.TryParse(Value, out _),
                SettingType.DateTime => DateTime.TryParse(Value, out _),
                SettingType.Decimal => decimal.TryParse(Value, out _),
                SettingType.Json => IsValidJson(),
                _ => true // String zawsze jest poprawny
            };
        }

        /// <summary>
        /// Sprawdza czy wartość jest poprawnym JSON
        /// </summary>
        private bool IsValidJson()
        {
            if (string.IsNullOrWhiteSpace(Value)) return true;

            try
            {
                JsonSerializer.Deserialize<object>(Value);
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}