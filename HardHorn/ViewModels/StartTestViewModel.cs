using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using HardHorn.Logging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace HardHorn.ViewModels
{
    class StartTestViewModel : Screen
    {
        public ArchiveVersion ArchiveVersion { get; private set; }
        public IEnumerable<TableViewModel> SelectedTableViewModels { get; private set; }
        public TestSelection SelectedTests { get; private set; }
        public ObservableCollection<RegexTestViewModel> Regexes { get; private set; }
        public Analyzer Analyzer { get; private set; }
        public ILogger Logger { get; private set; }

        public StartTestViewModel(ArchiveVersionViewModel avvm, IEnumerable<TableViewModel> selectedTableViewModels, ILogger logger)
        {
            SelectedTableViewModels = selectedTableViewModels;
            ArchiveVersion = avvm.ArchiveVersion;
            Regexes = new ObservableCollection<RegexTestViewModel>();
            Logger = logger;
            Analyzer = new Analyzer(ArchiveVersion, selectedTableViewModels.Select(tvm => tvm.Table), Logger);

            if (Properties.Settings.Default.SelectedTestsBase64 == null)
            {
                SelectedTests = TestSelection.GetFullSelection();
                SetDefaultSelectedTests();
            }
            else
            {
                //SelectedTests = GetDefaultSelectedTests();
                SelectedTests = TestSelection.GetFullSelection();
                SetDefaultSelectedTests();
            }
        }

        public void Cancel()
        {
            TryClose(false);
        }

        public void StartTest()
        {
            InitializeAnalyzer();
            TryClose(true);
        }

        public void InitializeAnalyzer()
        {
            foreach (var regex in Regexes)
            {
                if (SelectedTableViewModels.Select(tvm => tvm.Table).Contains(regex.Column.Table))
                {
                    Analyzer.AddTest(regex.Column, regex.RegexTest);
                }
            }

            foreach (var testSelectionCategory in SelectedTests)
                foreach (var testTypeSelection in testSelectionCategory)
                    foreach (var dataTypeSelection in testTypeSelection)
                    {
                        if (!dataTypeSelection.Selected.HasValue || !dataTypeSelection.Selected.Value)
                            continue;

                        foreach (var table in SelectedTableViewModels.Select(tvm => tvm.Table))
                        foreach (var column in table.Columns)
                        {
                            if (dataTypeSelection.DataType == column.ParameterizedDataType.DataType)
                            {
                                Test test;
                                switch (testTypeSelection.TestType)
                                {
                                    case AnalysisTestType.BLANK:
                                        test = new Test.Blank();
                                        break;
                                    case AnalysisTestType.OVERFLOW:
                                        test = new Test.Overflow();
                                        break;
                                    case AnalysisTestType.UNDERFLOW:
                                        test = new Test.Underflow();
                                        break;
                                    case AnalysisTestType.REPEATING_CHAR:
                                        test = new Test.RepeatingChar();
                                        break;
                                    case AnalysisTestType.UNALLOWED_KEYWORD:
                                        test = new Test.SuspiciousKeyword();
                                        break;
                                        case AnalysisTestType.FORMAT:
                                        switch (dataTypeSelection.DataType)
                                        {
                                            case DataType.TIMESTAMP:
                                                test = Test.TimestampFormatTest();
                                                break;
                                            case DataType.TIMESTAMP_WITH_TIME_ZONE:
                                                test = Test.TimestampWithTimeZoneFormatTest();
                                                break;
                                            case DataType.DATE:
                                                test = Test.DateFormatTest();
                                                break;
                                            case DataType.TIME:
                                                test = Test.TimeFormatTest();
                                                break;
                                            case DataType.TIME_WITH_TIME_ZONE:
                                                test = Test.TimeWithTimeZoneTest();
                                                break;
                                            default:
                                                continue;
                                        }
                                        break;
                                    default:
                                        continue;
                                }
                                Analyzer.AddTest(column, test);
                                Analyzer.AddTest(column, new Test.HtmlEntity());
                                Analyzer.AddTest(column, new Test.RepeatingChar());
                                Analyzer.AddTest(column, new Test.SuspiciousKeyword());
                                Analyzer.AddTest(column, new Test.EntityCharRef());
                            }
                        }
                    }
        }

        public void AddRegex(string pattern, Column column)
        {
            if (pattern == null || pattern.Length == 0 || column == null)
            {
                return;
            }

            try
            {
                var regex = new Regex(pattern);
                Regexes.Add(new RegexTestViewModel(new Test.Pattern(regex), column));
            }
            catch (ArgumentException)
            {
               Logger.Log(string.Format("Det regulære udtryk \"{0}\" er ikke gyldigt.", pattern), LogLevel.ERROR);
            }
        }

        public void RemoveRegex(RegexTestViewModel regex)
        {
            Regexes.Remove(regex);
        }

        public TestSelection GetDefaultSelectedTests()
        {
            TestSelection selection = null;
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                using (var writer = new BinaryWriter(stream))
                {
                    writer.Write(Convert.FromBase64String(Properties.Settings.Default.SelectedTestsBase64));
                    writer.Flush();
                    stream.Position = 0;

                    try
                    {
                        selection = formatter.Deserialize(stream) as TestSelection;
                    }
                    catch (Exception)
                    {
                        selection = null;
                    }
                }
            }

            if (selection != null)
                foreach (var category in selection)
                    category.HookupEvents();

            return selection;
        }

        public void SetDefaultSelectedTests()
        {
            var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
            using (var stream = new MemoryStream())
            {
                try
                {
                    formatter.Serialize(stream, SelectedTests);
                    Properties.Settings.Default.SelectedTestsBase64 = Convert.ToBase64String(stream.ToArray());
                    Properties.Settings.Default.Save();
                }
                catch (Exception) { }
            }
        }
    }
}
