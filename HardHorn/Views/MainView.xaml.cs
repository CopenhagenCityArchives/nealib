using HardHorn.Analysis;
using HardHorn.ArchiveVersion;
using System;
using System.Linq;
using System.Windows;

namespace HardHorn.Views
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainView : Window
    {
        public MainView()
        {
            InitializeComponent();
            //DataTypeListBox.ItemsSource = Enum.GetValues(typeof(DataType)).Cast<DataType>();
            //AnalysisErrorTypeListBox.ItemsSource = Enum.GetValues(typeof(AnalysisErrorType)).Cast<DataType>();
        }
    }
}
