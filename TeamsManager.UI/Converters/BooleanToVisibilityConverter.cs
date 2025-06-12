using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter konwertujący wartość bool na Visibility
    /// true -> Visible, false -> Collapsed
    /// </summary>
    public class BooleanToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is bool boolValue)
            {
                bool isInverse = parameter != null && parameter.ToString()?.Equals("Inverse", StringComparison.OrdinalIgnoreCase) == true;
                
                if (isInverse)
                {
                    return boolValue ? Visibility.Collapsed : Visibility.Visible;
                }
                else
                {
                    return boolValue ? Visibility.Visible : Visibility.Collapsed;
                }
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }
} 