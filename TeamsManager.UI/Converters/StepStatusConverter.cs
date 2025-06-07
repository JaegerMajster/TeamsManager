using System;
using System.Globalization;
using System.Windows.Data;

namespace TeamsManager.UI.Converters
{
    public class StepStatusConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values.Length == 2 && 
                values[0] is int currentStep && 
                values[1] is int stepIndex)
            {
                if (currentStep > stepIndex)
                    return "Completed";
                else if (currentStep == stepIndex)
                    return "Active";
                else
                    return "Inactive";
            }
            return "Inactive";
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
} 