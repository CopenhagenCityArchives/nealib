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
                nullTrigger.Setters.Add(new Setter(TextBox.TextProperty, "null", "PostTextBox"));
                nullTrigger.Setters.Add(new Setter(TextBox.BackgroundProperty, System.Windows.Media.Brushes.DarkBlue, "PostTextBox"));
                nullTrigger.Setters.Add(new Setter(TextBox.ForegroundProperty, System.Windows.Media.Brushes.White, "PostTextBox"));
                nullTrigger.Setters.Add(new Setter(TextBox.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"), "PostTextBox"));
                nullTrigger.Setters.Add(new Setter(TextBox.FontWeightProperty, FontWeights.Bold, "PostTextBox"));
                nullTrigger.Setters.Add(new Setter(TextBox.FontSizeProperty, 10.0, "PostTextBox"));
                var emptyTrigger = new DataTrigger() { Binding = new Binding(string.Format("Posts[{0}].Data", column.ColumnIdNumber - 1)), Value = "" };
                emptyTrigger.Setters.Add(new Setter(TextBox.TextProperty, "tom", "PostTextBox"));
                emptyTrigger.Setters.Add(new Setter(TextBox.BackgroundProperty, System.Windows.Media.Brushes.Brown, "PostTextBox"));
                emptyTrigger.Setters.Add(new Setter(TextBox.ForegroundProperty, System.Windows.Media.Brushes.White, "PostTextBox"));
                emptyTrigger.Setters.Add(new Setter(TextBox.FontFamilyProperty, new System.Windows.Media.FontFamily("Consolas"), "PostTextBox"));
                emptyTrigger.Setters.Add(new Setter(TextBox.FontWeightProperty, FontWeights.Bold, "PostTextBox"));
                emptyTrigger.Setters.Add(new Setter(TextBox.FontSizeProperty, 10.0, "PostTextBox"));
                var selectedTrigger = new DataTrigger() {
                    Binding = new Binding("IsSelected"),
                    Value = true
                };
                selectedTrigger.Setters.Add(new Setter(TextBox.ForegroundProperty, SystemColors.HighlightTextBrush, "PostTextBox"));
                browseColumn.CellTemplate.Triggers.Add(selectedTrigger);
                browseColumn.CellTemplate.Triggers.Add(emptyTrigger);
                browseColumn.CellTemplate.Triggers.Add(nullTrigger);
                browseColumn.CellTemplate.VisualTree = new FrameworkElementFactory(typeof(Border), null);
                var textBox = new FrameworkElementFactory(typeof(TextBox), "PostTextBox");
                textBox.SetBinding(TextBox.TextProperty, new Binding(string.Format("Posts[{0}].Data", column.ColumnIdNumber - 1)));
                textBox.SetValue(TextBox.MaxLinesProperty, 1);
                textBox.SetValue(TextBox.BorderThicknessProperty, new Thickness(0.0));
                textBox.SetValue(TextBox.IsReadOnlyProperty, true);
                textBox.SetValue(TextBox.BackgroundProperty, System.Windows.Media.Brushes.Transparent);
                browseColumn.CellTemplate.VisualTree.AppendChild(textBox);
                gridView.Columns.Add(browseColumn);
            }

        }
    }
}
