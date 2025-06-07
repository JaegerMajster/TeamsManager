using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace TeamsManager.UI.Converters
{
    /// <summary>
    /// Konwerter mapujący status operacji na odpowiedni kolor
    /// </summary>
    public class OperationStatusToColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessBrush = new(Color.FromRgb(76, 175, 80));    // Green 500
        private static readonly SolidColorBrush ErrorBrush = new(Color.FromRgb(244, 67, 54));      // Red 500
        private static readonly SolidColorBrush WarningBrush = new(Color.FromRgb(255, 152, 0));    // Orange 500
        private static readonly SolidColorBrush InfoBrush = new(Color.FromRgb(33, 150, 243));      // Blue 500
        private static readonly SolidColorBrush DefaultBrush = new(Color.FromRgb(158, 158, 158));  // Grey 500

        static OperationStatusToColorConverter()
        {
            // Zamrożenie brushów dla lepszej wydajności
            SuccessBrush.Freeze();
            ErrorBrush.Freeze();
            WarningBrush.Freeze();
            InfoBrush.Freeze();
            DefaultBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string status)
                return DefaultBrush;

            return status.ToLowerInvariant() switch
            {
                // Sukces
                "completed" or "success" or "succeeded" => SuccessBrush,
                
                // Błąd
                "failed" or "error" or "exception" or "cancelled" => ErrorBrush,
                
                // Ostrzeżenie / Częściowy sukces
                "partialsuccess" or "warning" or "partiallysuccessful" => WarningBrush,
                
                // W toku
                "inprogress" or "running" or "executing" or "pending" => InfoBrush,
                
                // Domyślny dla nieznanych statusów
                _ => DefaultBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for OperationStatusToColorConverter");
        }
    }

    /// <summary>
    /// Konwerter mapujący status operacji na kolor tekstu (dla lepszej czytelności)
    /// </summary>
    public class OperationStatusToTextColorConverter : IValueConverter
    {
        private static readonly SolidColorBrush SuccessTextBrush = new(Color.FromRgb(27, 94, 32));    // Dark Green 900
        private static readonly SolidColorBrush ErrorTextBrush = new(Color.FromRgb(183, 28, 28));      // Dark Red 900
        private static readonly SolidColorBrush WarningTextBrush = new(Color.FromRgb(230, 81, 0));     // Dark Orange 900
        private static readonly SolidColorBrush InfoTextBrush = new(Color.FromRgb(13, 71, 161));       // Dark Blue 900
        private static readonly SolidColorBrush DefaultTextBrush = new(Color.FromRgb(66, 66, 66));     // Dark Grey 800

        static OperationStatusToTextColorConverter()
        {
            SuccessTextBrush.Freeze();
            ErrorTextBrush.Freeze();
            WarningTextBrush.Freeze();
            InfoTextBrush.Freeze();
            DefaultTextBrush.Freeze();
        }

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is not string status)
                return DefaultTextBrush;

            return status.ToLowerInvariant() switch
            {
                "completed" or "success" or "succeeded" => SuccessTextBrush,
                "failed" or "error" or "exception" or "cancelled" => ErrorTextBrush,
                "partialsuccess" or "warning" or "partiallysuccessful" => WarningTextBrush,
                "inprogress" or "running" or "executing" or "pending" => InfoTextBrush,
                _ => DefaultTextBrush
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException("ConvertBack is not supported for OperationStatusToTextColorConverter");
        }
    }
} 