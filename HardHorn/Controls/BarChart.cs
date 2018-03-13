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
        public class Bucket
        {
            public int IntervalStart { get; set; }
            public int IntervalEnd { get; set; }
            public int Count { get; set; }
            public double Height { get; set; }

            public Bucket(int intervalStart, int intervalEnd, int count, double height = 0d)
            {
                IntervalStart = intervalStart;
                IntervalEnd = intervalEnd;
                Count = count;
                Height = height;
            }
        }

        public static readonly DependencyProperty ValuesProperty =
            DependencyProperty.Register("Values", typeof(IEnumerable<uint>), typeof(BarChart),
            new FrameworkPropertyMetadata(Enumerable.Empty<uint>(), FrameworkPropertyMetadataOptions.None, new PropertyChangedCallback(ValuesPropertyChangedCallback)));

        public static void ValuesPropertyChangedCallback(DependencyObject d, DependencyPropertyChangedEventArgs args)
        {
            var barChart = d as BarChart;
            if (barChart != null)
                barChart.ConfigureBarChart();
        }

        public IEnumerable<uint> Values
        {
            get { return (IEnumerable<uint>)GetValue(ValuesProperty); }
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

        public ObservableCollection<Bucket> Buckets { get; set; }

        static BarChart()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(BarChart), new FrameworkPropertyMetadata(typeof(BarChart)));
        }

        public BarChart()
        {
            Buckets = new ObservableCollection<Bucket>();
            ConfigureBarChart();
        }

        public void ConfigureBarChart()
        {
            if (Values == null || Values.Count() == 0 || !BucketCount.HasValue)
            {
                return;
            }

            var min = Values.Min();
            var max = Values.Max();
            int bucketCount = Math.Min(BucketCount.Value, Enumerable.Distinct(Values).Count());
            float interval = (float)max / bucketCount;

            for (int i = 0; i < bucketCount; i++)
            {
                Buckets.Add(new Bucket((int)Math.Ceiling(i * interval), (int)Math.Ceiling((i + 1) * interval), 0));
            }

            foreach (var val in Values)
            {
                int i = (int)((val / interval) - 1.0f);
                if (val == 0)
                    i = 0;
                Bucket b = Buckets[i];
                b.Count++;
            }

            int maxCount = 0;
            foreach (var b in Buckets)
            {
                maxCount = Math.Max(b.Count, maxCount);
            }

            foreach (var b in Buckets)
            {
                b.Height = (double)b.Count / (double)maxCount * (this as FrameworkElement).Height;
            }
        }
    }
}