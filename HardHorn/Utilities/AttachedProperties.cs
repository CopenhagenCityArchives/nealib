using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace HardHorn.Utilities
{
    public static class AttachedProperties
    {
        public static readonly DependencyProperty TableColumnsSourceProperty =
            DependencyProperty.RegisterAttached
                (
                    "TableColumnsSource",
                    typeof(Table),
                    typeof(AttachedProperties),
                    new UIPropertyMetadata(null, TableColumnsSource_Changed)
                );

        public static GridViewColumnCollection GetTableColumnsSource(DependencyObject obj)
        {
            return (GridViewColumnCollection)obj.GetValue(TableColumnsSourceProperty);
        }

        public static void SetTableColumnsSource(DependencyObject obj, Table value)
        {
            obj.SetValue(TableColumnsSourceProperty, value);
        }

        private static void TableColumnsSource_Changed(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var gridView = obj as GridView;
            if (gridView == null)
                throw new Exception(string.Format("Object of type '{0}' does not support ColumnsSource.", obj.GetType()));

            var table = (Table)e.NewValue;
            if (table == null)
                return;

            gridView.Columns.Clear();
            foreach (var column in table.Columns)
            {
                var browseColumn = new GridViewColumn();
                browseColumn.DisplayMemberBinding = new Binding(string.Format("Posts[{0}].Data", column.ColumnIdNumber - 1));
                browseColumn.Header = column.Name;
                gridView.Columns.Add(browseColumn);
            }

        }
    }
}
