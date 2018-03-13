﻿using System;
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
using System.Windows.Controls;
using Caliburn.Micro;
using System.Data;

namespace HardHorn.Utilities
{
    public class KeyTestResultListToDataTable : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var keyTestResults = value as Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>;
            if (keyTestResults == null)
            {
                return null;
            }
            var resultsList = keyTestResults.Item4;
            var foreignKey = keyTestResults.Item1;

            if (foreignKey == null || keyTestResults == null)
            {
                return null;
            }

            var dataTable = new DataTable();
            foreach (var reference in foreignKey.References)
            {
                dataTable.Columns.Add(reference.ColumnName.Replace("_", "__"), typeof(string));
            }
            dataTable.Columns.Add("Antal fejl", typeof(int));

            foreach (var result in resultsList)
            {
                var objs = new List<object>(result.Key.Values);
                objs.Add(result.Value);
                dataTable.Rows.Add(objs.ToArray());
            }

            return dataTable;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CellIsEmptyConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[1] is System.Data.DataRow)
            {
                var cell = values[0] as System.Windows.Controls.DataGridCell;
                var row = values[1] as System.Data.DataRow;
                var columnName = cell.Column.SortMemberPath;

                var post = row[columnName] as Post;
                if (!post.IsNull && string.IsNullOrEmpty(post.Data))
                {
                    return true;
                }
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class CellIsNullConverter : IMultiValueConverter
    {
        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            if (values[1] is System.Data.DataRow)
            {
                var cell = values[0] as System.Windows.Controls.DataGridCell;
                var row = values[1] as System.Data.DataRow;
                var columnName = cell.Column.SortMemberPath;

                if ((row[columnName] as Post).IsNull)
                {
                    return true;
                }
            }
            return false;
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class RecentLocationsMenuItemConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var locations = value as IEnumerable<string>;

            if (locations == null || locations.Count() == 0)
            {
                return Enumerable.Empty<MenuItem>();
            }

            var menuItems = new List<Control>();
            int i = 1;
            foreach (var location in locations)
            {
                var menuItem = new MenuItem();
                menuItem.Header = i++ + ": " + location;
                Message.SetAttach(menuItem, "LoadLocation('" + location + "')");
                menuItems.Add(menuItem);
            }

            if (menuItems.Count > 0)
            {
                menuItems.Add(new Separator());
            }

            return menuItems;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class ParameterizedDataTypeToParameterStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var paramDataType = value as ParameterizedDataType;
            if (paramDataType == null || paramDataType.Parameter == null)
            {
                return null;
            }

            return paramDataType.Parameter.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }


    public class ParameterToStringConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var param = value as Archiving.Parameter;
            return param == null ? "" : param.ToString();
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

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
