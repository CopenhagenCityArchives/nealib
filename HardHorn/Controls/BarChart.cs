using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Dynamic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace HardHorn.Controls
{
    /// <summary>
    /// Follow steps 1a or 1b and then 2 to use this custom control in a XAML file.
    ///
    /// Step 1a) Using this custom control in a XAML file that exists in the current project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:HardHorn.Controls"
    ///
    ///
    /// Step 1b) Using this custom control in a XAML file that exists in a different project.
    /// Add this XmlNamespace attribute to the root element of the markup file where it is 
    /// to be used:
    ///
    ///     xmlns:MyNamespace="clr-namespace:HardHorn.Controls;assembly=HardHorn"
    ///
    /// You will also need to add a project reference from the project where the XAML file lives
    /// to this project and Rebuild to avoid compilation errors:
    ///
    ///     Right click on the target project in the Solution Explorer and
    ///     "Add Reference"->"Projects"->[Browse to and select this project]
    ///
    ///
    /// Step 2)
    /// Go ahead and use your control in the XAML file.
    ///
    ///     <MyNamespace:BarChart/>
    ///
    /// </summary>
    public class BarChart : Control
    {
        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register("Values", typeof(IEnumerable<int>), typeof(BarChart),
            new FrameworkPropertyMetadata(Enumerable.Empty<int>(), FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(ValuesPropertyChangedCallback)));

        public static void ValuesPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            var barChart = d as BarChart;
            if (barChart != null)
                barChart.ConfigureBarChart();
        }

        public IEnumerable<int> Values
        {
            get { return (IEnumerable<int>)GetValue(ValuesProperty); }
            set { SetValue(ValuesProperty, value); }
        }

        public static readonly DependencyProperty BucketCountProperty =
            DependencyProperty.Register("BucketCount", typeof(int), typeof(BarChart),
            new FrameworkPropertyMetadata(10, FrameworkPropertyMetadataOptions.None));

        public int? BucketCount
        {
            get { return (int?)GetValue(BucketCountProperty); }
            set { SetValue(BucketCountProperty, value); ConfigureBarChart(); }
        }

        public ObservableCollection<ExpandoObject> Buckets { get; set; }

        static BarChart()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BarChart), new FrameworkPropertyMetadata(typeof(BarChart)));
        }

        public BarChart()
        {
            Buckets = new ObservableCollection<ExpandoObject>();
            ConfigureBarChart();
        }

        public void ConfigureBarChart()
        {
            if (Values.Count() == 0 || !BucketCount.HasValue)
            {
                return;
            }

            var min = Values.Min();
            var max = Values.Max();
            float interval = (float)max / BucketCount.Value;

            for (int i = 0; i < BucketCount.Value; i++)
            {
                dynamic b = new ExpandoObject();
                b.Count = 0;
                b.IntervalStart = (int)Math.Ceiling(i * interval);
                b.IntervalEnd = (int)Math.Ceiling((i + 1) * interval);
                Buckets.Add(b);
            }

            foreach (var val in Values)
            {
                int i = (int)((val / interval) - 1.0f);
                dynamic b = Buckets[i];
                b.Count++;
            }

            int maxCount = 0;
            foreach (dynamic b in Buckets)
            {
                maxCount = Math.Max(b.Count, maxCount);
            }

            foreach (dynamic b in Buckets)
            {
                b.Height = (double)b.Count / (double)maxCount * (this as FrameworkElement).Height;
            }
        }
    }
}