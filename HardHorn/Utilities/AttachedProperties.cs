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
                browseColumn.Header = column.Name;
                browseColumn.CellTemplate = new DataTemplate(typeof(Post));
                var nullTrigger = new DataTrigger() { Binding = new Binding(string.Format("Posts[{0}].IsNull", column.ColumnIdNumber - 1)), Value = true };
                nullTrigger.Setters.Add(new Setter(TextBlock.TextProperty, "null", "PostTextBlock"));
                nullTrigger.Setters.Add(new Setter(TextBlock.BackgroundProperty, System.Windows.Media.Brushes.DarkBlue, "PostTextBlock"));
                nullTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White, "PostTextBlock"));
                nullTrigger.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"), "PostTextBlock"));
                nullTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold, "PostTextBlock"));
                nullTrigger.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0, "PostTextBlock"));
                var emptyTrigger = new DataTrigger() { Binding = new Binding(string.Format("Posts[{0}].Data", column.ColumnIdNumber - 1)), Value = "" };
                emptyTrigger.Setters.Add(new Setter(TextBlock.TextProperty, "tom", "PostTextBlock"));
                emptyTrigger.Setters.Add(new Setter(TextBlock.BackgroundProperty, System.Windows.Media.Brushes.Brown, "PostTextBlock"));
                emptyTrigger.Setters.Add(new Setter(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White, "PostTextBlock"));
                emptyTrigger.Setters.Add(new Setter(TextBlock.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"), "PostTextBlock"));
                emptyTrigger.Setters.Add(new Setter(TextBlock.FontWeightProperty, FontWeights.Bold, "PostTextBlock"));
                emptyTrigger.Setters.Add(new Setter(TextBlock.FontSizeProperty, 10.0, "PostTextBlock"));
                browseColumn.CellTemplate.Triggers.Add(emptyTrigger);
                browseColumn.CellTemplate.Triggers.Add(nullTrigger);
                browseColumn.CellTemplate.VisualTree = new FrameworkElementFactory(typeof(TextBlock), "PostTextBlock");
                browseColumn.CellTemplate.VisualTree.SetBinding(TextBlock.TextProperty, new Binding(string.Format("Posts[{0}].Data", column.ColumnIdNumber - 1)));
                gridView.Columns.Add(browseColumn);
            }

        }
    }
}
