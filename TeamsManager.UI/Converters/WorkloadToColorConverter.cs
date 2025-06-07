using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter obciążenia workload na kolor paska postępu.
    /// Zielony dla normalnego obciążenia, pomarańczowy dla wysokiego, czerwony dla przekroczenia.
    /// </summary>
    public class WorkloadToColorConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values?.Length > 0 && values[0] is decimal workload)
            {
                return workload switch
                {
                    > 100 => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Czerwony - przekroczenie
                    > 80 => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // Pomarańczowy - wysokie obciążenie
                    _ => new SolidColorBrush(Color.FromRgb(76, 175, 80))      // Zielony - normalne
                };
            }

            return new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Domyślny niebieski
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("WorkloadToColorConverter nie obsługuje konwersji zwrotnej");
        }
    }

    /// <summary>
    /// Pojedyncza wersja konwertera dla prostszego użycia
    /// </summary>
    public class WorkloadToColorSingleConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is decimal workload)
            {
                return workload switch
                {
                    > 100 => new SolidColorBrush(Color.FromRgb(244, 67, 54)), // Czerwony
                    > 80 => new SolidColorBrush(Color.FromRgb(255, 152, 0)),  // Pomarańczowy
                    _ => new SolidColorBrush(Color.FromRgb(76, 175, 80))      // Zielony
                };
            }

            return new SolidColorBrush(Color.FromRgb(0, 120, 212)); // Domyślny niebieski
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("WorkloadToColorSingleConverter nie obsługuje konwersji zwrotnej");
        }
    }
} 