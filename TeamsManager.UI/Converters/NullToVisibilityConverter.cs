using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    public class NullToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool isInverse = parameter != null && parameter.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
            bool isNull = value == null;
            
            if (isInverse)
            {
                // Inverse: pokazuj gdy null, ukryj gdy nie null
                return isNull ? Visibility.Visible : Visibility.Collapsed;
            }
            else
            {
                // Normal: pokazuj gdy nie null, ukryj gdy null
                return isNull ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 