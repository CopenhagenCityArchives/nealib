using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Data;
using HardHorn.Archiving;
using System.Windows.Media;
using System.Windows;
using HardHorn.Analysis;

namespace HardHorn.Utilities
{
    public class HasErrorsColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool? errors = value as bool?;

            if (errors.HasValue && errors.Value)
            {
                if (parameter == SystemColors.HighlightTextBrush)
                {
                    return new SolidColorBrush(Colors.Tomato);
                }
                else
                {
                    return new SolidColorBrush(Colors.Red);
                }
            }
            return parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ErrorCountColorConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            int? errorCount = value as int?;

            if (errorCount.HasValue && errorCount.Value > 0)
            {
                if (parameter == SystemColors.HighlightTextBrush)
                {
                    return new SolidColorBrush(Colors.Tomato);
                }
                else
                {
                    return new SolidColorBrush(Colors.Red);
                }
            }
            return parameter;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class DataTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var datatype = value as DataType?;

            return datatype.ToString().Replace("_", " ");
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class AnalysisErrorTypeToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var error = value as AnalysisTestType?;

            switch (error)
            {
                case AnalysisTestType.UNDERFLOW:
                    return "Underudfyldelse";
                case AnalysisTestType.OVERFLOW:
                    return "Overskridelse";
                case AnalysisTestType.BLANK:
                    return "Foran- eller efterstillede blanktegn";
                case AnalysisTestType.FORMAT:
                    return "Formateringsfejl";
                default:
                    return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }

        public static string ConvertErrorType(AnalysisTestType type)
        {
            return (new AnalysisErrorTypeToStringConverter()).Convert(type, typeof(string), null, CultureInfo.CurrentCulture) as string;
        }
    }

    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var truthy = value as bool?;

            if (truthy.HasValue && truthy.Value)
            {
                return Visibility.Visible;
            }
            else
            {
                return Visibility.Collapsed;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
