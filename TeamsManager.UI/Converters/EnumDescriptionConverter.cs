using System;
using System.ComponentModel;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter enum wartości na opis z atrybutu Description lub przyjazną nazwę.
    /// </summary>
    public class EnumDescriptionConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null)
                return string.Empty;

            if (!value.GetType().IsEnum)
                return value.ToString();

            // Pobierz pole enum
            var fieldInfo = value.GetType().GetField(value.ToString());
            if (fieldInfo == null)
                return value.ToString();

            // Sprawdź czy jest atrybut Description
            var descriptionAttribute = fieldInfo.GetCustomAttribute<DescriptionAttribute>();
            if (descriptionAttribute != null)
                return descriptionAttribute.Description;

            // Fallback: skonwertuj CamelCase na spacje
            return ConvertCamelCaseToSpaces(value.ToString());
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null || !targetType.IsEnum)
                return Binding.DoNothing;

            var stringValue = value.ToString();

            // Spróbuj znaleźć enum value po Description
            foreach (var field in targetType.GetFields())
            {
                if (field.IsLiteral)
                {
                    var descriptionAttribute = field.GetCustomAttribute<DescriptionAttribute>();
                    if (descriptionAttribute?.Description == stringValue)
                    {
                        return Enum.Parse(targetType, field.Name);
                    }
                }
            }

            // Spróbuj bezpośrednio
            try
            {
                return Enum.Parse(targetType, stringValue, true);
            }
            catch
            {
                return Binding.DoNothing;
            }
        }

        private static string ConvertCamelCaseToSpaces(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            // Dodaj spacje przed wielkimi literami (ale nie na początku)
            var result = string.Concat(input.Select((x, i) => i > 0 && char.IsUpper(x) ? " " + x : x.ToString()));
            
            // Kapitalizuj pierwszą literę
            if (result.Length > 0)
                result = char.ToUpper(result[0]) + result.Substring(1);

            return result;
        }
    }
} 