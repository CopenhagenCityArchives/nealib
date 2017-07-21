using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Caliburn.Micro;
using System.ComponentModel;
using HardHorn.Analysis;
using HardHorn.Statistics;
using HardHorn.ArchiveVersion;
using System.IO;
using System.Collections.ObjectModel;
using System.Windows.Media;
using System.Collections;
using HardHorn.Logging;
using System.Dynamic;
using System.Text.RegularExpressions;

namespace HardHorn.ViewModels
{
    public enum TestWorkerUpdate
    {
        NEW_TABLE,
        UPDATE_TABLE_STATUS,
        TEST_DONE,
        COLUMN_REPORT,
        TABLE_REPORT
    }

    class ListTable : PropertyChangedBase
    {
        public Table Table { get; set; }

        public bool Errors
        {
            get { return _errors; }
            set { _errors = value; NotifyOfPropertyChange("Errors"); }
        }

        public bool Done
        {
            get { return _done; }
            set { _done = value; NotifyOfPropertyChange("Done"); }
        }

        public bool Busy
        {
            get { return _busy; }

            set { _busy = value; NotifyOfPropertyChange("Busy"); }
        }

        bool _busy = false;
        bool _errors = false;
        private bool _done = false;
    }

    class TestSuiteDataTypeTest : PropertyChangedBase
    {
        public DataType DataType { get; private set; }
        public TestSuiteTestType ParentTest { get; set; }

        bool? _selected = true;
        public bool? Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                if (ParentTest != null)
                {
                    ParentTest.ChildSetTo(value);
                }
                NotifyOfPropertyChange("Selected");
            }
        }

        public TestSuiteDataTypeTest(DataType dataType, TestSuiteTestType parentTest)
        {
            DataType = dataType;
            ParentTest = parentTest;
        }
    }

    class TestSuiteTestType : PropertyChangedBase
    {
        public AnalysisErrorType TestType { get; private set; }
        public IEnumerable<TestSuiteDataTypeTest> DataTypeTests { get; set; }
        public TestSuiteCategory Category { get; private set; }

        bool? _selected = true;
        public bool? Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                if (Category != null)
                {
                    Category.ChildSetTo(value);
                }
                foreach (var test in DataTypeTests)
                {
                    test.Selected = value;
                }
                NotifyOfPropertyChange("Selected");
            }
        }

        public void ChildSetTo(bool? value)
        {
            if (!value.HasValue || (Selected.HasValue && value.HasValue && Selected.Value != value.Value))
            {
                _selected = null;
            }
            else if (!Selected.HasValue && value.HasValue)
            {
                bool? newValue = null;
                bool init = true;
                foreach (var test in DataTypeTests)
                {
                    if (init)
                    {
                        newValue = test.Selected;
                        init = false;
                    }
                    else
                    {
                        if (test.Selected.HasValue && newValue.HasValue && test.Selected.Value == newValue.Value)
                        {
                            newValue = test.Selected.Value;
                        }
                        else
                        {
                            newValue = null;
                            break;
                        }
                    }
                }
                _selected = newValue;
            }
            Category.ChildSetTo(_selected);
            NotifyOfPropertyChange("Selected");
        }

        public TestSuiteTestType(AnalysisErrorType testType, TestSuiteCategory category)
        {
            Category = category;
            TestType = testType;
            DataTypeTests = Enumerable.Empty<TestSuiteDataTypeTest>();
        }
    }

    class TestSuiteCategory : PropertyChangedBase
    {
        public string Name { get; private set; }
        public ObservableCollection<TestSuiteTestType> TestTypes { get; private set; }

        bool? _selected = true;
        public bool? Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                foreach (var testType in TestTypes)
                {
                    testType.Selected = value;
                }
                NotifyOfPropertyChange("Selected");
            }
        }

        public void ChildSetTo(bool? value)
        {
            if (!value.HasValue || (Selected.HasValue && value.HasValue && Selected.Value != value.Value))
            {
                _selected = null;
            }
            else if (!Selected.HasValue && value.HasValue)
            {
                bool? newValue = null;
                bool init = true;
                foreach (var test in TestTypes)
                {
                    if (init)
                    {
                        newValue = test.Selected;
                        init = false;
                    }
                    else
                    {
                        if (test.Selected.HasValue && newValue.HasValue && test.Selected.Value == newValue.Value)
                        {
                            newValue = test.Selected.Value;
                        }
                        else
                        {
                            newValue = null;
                            break;
                        }
                    }
                }
                _selected = newValue;
            }
            NotifyOfPropertyChange("Selected");
        }


        public TestSuiteCategory(string name, DataType[] dataTypes, AnalysisErrorType[] testTypes)
        {
            Name = name;

            TestTypes = new ObservableCollection<TestSuiteTestType>();

            foreach (var testType in testTypes)
            {
                var test = new TestSuiteTestType(testType, this);
                var dataTypeTests = new List<TestSuiteDataTypeTest>();

                foreach (var dataType in dataTypes)
                {
                    var dataTypeTest = new TestSuiteDataTypeTest(dataType, test);
                    dataTypeTests.Add(dataTypeTest);
                }

                test.DataTypeTests = dataTypeTests.Cast<TestSuiteDataTypeTest>();
                TestTypes.Add(test);
            }
        }
    }

    class TestSuite : PropertyChangedBase, IEnumerable<TestSuiteCategory>
    {
        List<TestSuiteCategory> testCategories = new List<TestSuiteCategory>();

        public TestSuite()
        {
            testCategories.Add(new TestSuiteCategory("Strengtyper",
                new DataType[] { DataType.CHARACTER, DataType.CHARACTER_VARYING, DataType.NATIONAL_CHARACTER, DataType.NATIONAL_CHARACTER_VARYING },
                new AnalysisErrorType[] { AnalysisErrorType.OVERFLOW, AnalysisErrorType.UNDERFLOW, AnalysisErrorType.BLANK }));
            testCategories.Add(new TestSuiteCategory("Tidstyper",
                new DataType[] { DataType.TIME, DataType.TIMESTAMP, DataType.INTERVAL },
                new AnalysisErrorType[] { AnalysisErrorType.OVERFLOW, AnalysisErrorType.FORMAT }));
            testCategories.Add(new TestSuiteCategory("Decimaltalstyper",
                new DataType[] { DataType.DECIMAL, DataType.DOUBLE_PRECISION, DataType.FLOAT, DataType.REAL },
                new AnalysisErrorType[] { AnalysisErrorType.OVERFLOW }));
        }

        public Dictionary<DataType, HashSet<AnalysisErrorType>> GetTestDictionary()
        {
            var dict = new Dictionary<DataType, HashSet<AnalysisErrorType>>();

            foreach (var testCategory in testCategories)
            {
                foreach (var testType in testCategory.TestTypes)
                {
                    foreach (var dataType in testType.DataTypeTests)
                    {
                        if (dataType.Selected.HasValue && dataType.Selected.Value)
                        {
                            if (!dict.ContainsKey(dataType.DataType))
                            {
                                dict.Add(dataType.DataType, new HashSet<AnalysisErrorType>(new AnalysisErrorType[] { testType.TestType }));
                            }
                            else
                            {
                                dict[dataType.DataType].Add(testType.TestType);
                            }
                        }
                    }
                }
            }

            return dict;
        }

        public IEnumerator<TestSuiteCategory> GetEnumerator()
        {
            return testCategories.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return testCategories.GetEnumerator();
        }
    }

    class LoadWorkerLogger : ILogger
    {
        private BackgroundWorker _worker;

        public void Log(string message, LogLevel level)
        {
            dynamic state = new ExpandoObject();

            state.Message = message;
            state.LogLevel = level;

            _worker.ReportProgress(0, state);
        }

        public LoadWorkerLogger(BackgroundWorker worker)
        {
            _worker = worker;
        }
    }

    class MainViewModel : PropertyChangedBase, ILogger
    {
        #region Properties
        public object Item { get; set; }
        public ObservableCollection<Tuple<LogLevel, DateTime, string>> LogItems { get; set; }
        public TestSuite TestSuite { get; set; }
        public int[] BarChartValues { get; set; }

        bool _showErrorReports = true;
        public bool ShowErrorReports
        {
            get { return _showErrorReports; }
            set { _showErrorReports = value; UpdateInteractiveReportView(); }
        }

        bool _showSuggestionReports = true;
        public bool ShowSuggestionReports
        {
            get { return _showSuggestionReports; }
            set { _showSuggestionReports = value; UpdateInteractiveReportView(); }
        }

        bool _showEmptyReports = false;
        public bool ShowEmptyReports
        {
            get { return _showEmptyReports; }
            set { _showEmptyReports = value; UpdateInteractiveReportView(); }
        }

        bool _testRunning = false;
        public bool TestRunning
        {
            get { return _testRunning; }
            set { _testRunning = value; NotifyOfPropertyChange("TestRunning"); }
        }

        private bool _testLoaded = false;
        public bool TestLoaded
        {
            get
            {
                return _testLoaded;
            }

            set
            {
                _testLoaded = value; NotifyOfPropertyChange("TestLoaded");
            }
        }

        int _testProgress;
        public int TestProgress
        {
            get { return _testProgress; }
            set { _testProgress = value; NotifyOfPropertyChange("TestProgress"); }
        }

        string _testLocation = "";
        public string TestLocation
        {
            get { return _testLocation; }
            set { _testLocation = value; NotifyOfPropertyChange("TestLocation"); }
        }

        public ObservableCollection<AnalysisErrorType> DataTypeErrors
        {
            get; set;
        }

        ListTable _currentTable = null;
        public ListTable CurrentTable
        {
            get { return _currentTable; }
            set { _currentTable = value;  UpdateInteractiveReportView(); NotifyOfPropertyChange("CurrentTable"); }
        }

        public string SpecTables
        {
            set
            {
                SpecTablesMatching = "";
                SpecTablesMissing = "";
                SpecTablesUndefined = "";

                bool matched;

                foreach (var name in value.Split('\n'))
                {
                    if (name.Trim().Length > 0)
                    {
                        matched = false;
                        foreach (var table in Tables)
                        {
                            if (table.Table.Name.ToLower() == name.Trim().ToLower())
                            {
                                SpecTablesMatching += name.Trim() + "\n";
                                matched = true;
                            }
                        }

                        if (!matched)
                        {
                            SpecTablesMissing += name.Trim() + "\n";
                        }
                    }
                }

                foreach (var table in Tables)
                {
                    matched = false;
                    foreach (var name in value.Split('\n'))
                    {
                        if (name.Trim().Length > 0)
                        {
                            if (table.Table.Name.ToLower() == name.Trim().ToLower())
                            {
                                matched = true;
                            }
                        }
                    }

                    if (!matched)
                    {
                        SpecTablesUndefined += table.Table.Name + "\n";
                    }
                }

                NotifyOfPropertyChange("SpecTablesMatching");
                NotifyOfPropertyChange("SpecTablesMissing");
                NotifyOfPropertyChange("SpecTablesUndefined");
            }
        }

        public string SpecTablesMatching { get; set; }
        public string SpecTablesMissing { get; set; }
        public string SpecTablesUndefined { get; set; }
      

        public void UpdateInteractiveReportView()
        {
            var table = CurrentTable == null ? null : CurrentTable.Table;

            if (table == null)
            {
                TableReports = null;
            }
            else if (_analyzer != null && _analyzer.Report.ContainsKey(table.Name))
            {
                TableReports = new ObservableCollection<AnalysisReport>();
                foreach (var report in _analyzer.Report[table.Name].Values)
                {
                    if ((report.ErrorCount > 0 && ShowErrorReports) ||
                        (report.SuggestedType != null && ShowSuggestionReports) ||
                        (report.ErrorCount == 0 && report.SuggestedType == null && ShowEmptyReports))
                        TableReports.Add(report);
                }
            }
        }

        ObservableCollection<AnalysisReport> _tableReports;
        public ObservableCollection<AnalysisReport> TableReports
        {
            get { return _tableReports; }
            set { _tableReports = value; NotifyOfPropertyChange("TableReports"); }
        }

        Dictionary<string, ListTable> ListTableLookup = new Dictionary<string, ListTable>();
        public ObservableCollection<ListTable> Tables { get; set; }

        BackgroundWorker _loadWorker = new BackgroundWorker();
        BackgroundWorker _testWorker = new BackgroundWorker();

        DataAnalyzer _analyzer;
        DataStatistics _stats;

        public IEnumerable<KeyValuePair<DataType, dynamic>> DataTypeStatistics
        {
            get
            {
                if (_stats != null && _stats.DataTypeStatistics != null)
                {
                    return _stats.DataTypeStatistics.Cast<KeyValuePair<DataType, dynamic>>();
                }
                else
                {
                    return Enumerable.Empty<KeyValuePair<DataType, dynamic>>();
                }
                
            }
        }

        public ObservableCollection<dynamic> Regexes { get; private set; }

        string _statusText = "";
        public string StatusText { get { return _statusText; } set { _statusText = value; NotifyOfPropertyChange("StatusText"); } }

        #endregion

        #region Constructors
        public MainViewModel()
        {
            Tables = new ObservableCollection<ListTable>();
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();
            DataTypeErrors = new ObservableCollection<AnalysisErrorType>();
            Regexes = new ObservableCollection<dynamic>();
            TestSuite = new TestSuite();

            Log("Så er det dælme tid til at teste datatyper!", LogLevel.SECTION);

            // Setup test worker
            _testWorker.DoWork += _testWorker_DoWork;
            _testWorker.RunWorkerCompleted += _testWorker_RunWorkerCompleted;
            _testWorker.ProgressChanged += _testWorker_ProgressChanged;
            _testWorker.WorkerReportsProgress = true;
            _testWorker.WorkerSupportsCancellation = true;

            _loadWorker.DoWork += _loadWorker_DoWork;
            _loadWorker.RunWorkerCompleted += _loadWorker_RunWorkerCompleted;
            _loadWorker.ProgressChanged += _loadWorker_ProgressChanged;
            _loadWorker.WorkerReportsProgress = true;
        }
        #endregion

        #region Methods
        public void Log(string msg, LogLevel level = LogLevel.NORMAL)
        {
            LogItems.Add(new Tuple<LogLevel, DateTime, string>(level, DateTime.Now, msg));

            if (level == LogLevel.SECTION || level == LogLevel.ERROR)
            {
                StatusText = msg;
            }
        }

        public void OpenCurrentTable()
        {
            if (CurrentTable == null)
                return;

            var path = Path.Combine(TestLocation, "tables", CurrentTable.Table.Folder, CurrentTable.Table.Folder + ".xml");
            System.Diagnostics.Process.Start(path);
        }
        #endregion

        #region Background workers
        private void _loadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Tables.Clear();
            ListTableLookup.Clear();

            if (e.Error != null)
            {
                Log("En undtagelse forekom under indlæsningen af arkiveringsversionen, med følgende besked: " + e.Error.Message, LogLevel.ERROR);
                return;
            }

            foreach (var table in _analyzer.Tables)
            {
                var listTable = new ListTable() { Table = table, Errors = false };
                ListTableLookup.Add(table.Name, listTable);
                Tables.Add(listTable);
            }
            TestLoaded = true;

            _stats = new DataStatistics(Tables.Select(lt => lt.Table).ToArray());
            foreach (dynamic stat in _stats.DataTypeStatistics.Values)
            {
                stat.BarCharts = new ObservableCollection<ExpandoObject>();
                foreach (var paramValues in stat.ParamValues)
                {
                    dynamic b = new ExpandoObject();
                    b.Values = paramValues;
                    b.BucketCount = 10;
                    stat.BarCharts.Add(b);
                }
            }
            NotifyOfPropertyChange("DataTypeStatistics");
            Log("Statistik", LogLevel.SECTION);
            foreach (var dataType in _stats.DataTypeStatistics.Keys)
            {
                Log(string.Format("Datatype {0}:", dataType.ToString()));
                Log(string.Format("\tAntal kolonner: {0}", _stats.DataTypeStatistics[dataType].Count));
                if (_stats.DataTypeStatistics[dataType].MinParams != null)
                {
                    Log(string.Format("\tMinimumsparametre: {0}", string.Join(",", _stats.DataTypeStatistics[dataType].MinParams)));
                }
                if (_stats.DataTypeStatistics[dataType].MaxParams != null)
                {
                    Log(string.Format("\tMaksimumsparametre: {0}", string.Join(",", _stats.DataTypeStatistics[dataType].MaxParams)));
                }
            }
            Log("Indlæsning fuldført.", LogLevel.SECTION);

            // Add to recent locations
            if (Properties.Settings.Default.RecentLocations == null)
            {
                Properties.Settings.Default.RecentLocations = new ObservableCollection<string>();
            }
            var location = TestLocation;
            var index = -1;
            for (int i = 0; i < Properties.Settings.Default.RecentLocations.Count; i++)
            {
                var loc = Properties.Settings.Default.RecentLocations[i];
                if (loc.ToLower() == TestLocation.ToLower())
                {
                    index = i;
                    break;
                }
            }
            if (index != -1)
            {
                Properties.Settings.Default.RecentLocations.RemoveAt(index);
            }
            if (Properties.Settings.Default.RecentLocations.Count < 5)
            {
                Properties.Settings.Default.RecentLocations.Add(null);
            }
            TestLocation = location;

            for (int i = Properties.Settings.Default.RecentLocations.Count - 1; i > 0; i--)
            {
                Properties.Settings.Default.RecentLocations[i] = Properties.Settings.Default.RecentLocations[i - 1];
            }
            Properties.Settings.Default.RecentLocations[0] = TestLocation;

            Properties.Settings.Default.Save();
            NotifyOfPropertyChange("RecentLocations");

            SpecTables = "";
        }

        private void _loadWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            dynamic state = e.UserState;
            Log(state.Message, state.LogLevel);
        }

        private void _loadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var loadLogger = new LoadWorkerLogger(sender as BackgroundWorker);
            _analyzer = new DataAnalyzer(e.Argument as string, loadLogger);
        }

        void _testWorker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            TestProgress = e.ProgressPercentage;

            dynamic state = e.UserState;
            TestWorkerUpdate type = state.Type;
            Table table;

            switch (type)
            {
                case TestWorkerUpdate.NEW_TABLE:
                    table = state.Data as Table;
                    if (table == null) return;
                    Log(string.Format("Tester tabellen '{0}' ({1})", table.Name, table.Folder), LogLevel.SECTION);
                    break;
                case TestWorkerUpdate.UPDATE_TABLE_STATUS:
                    table = state.Data as Table;
                    if (table == null) return;
                    var listTable = ListTableLookup[table.Name];
                    if (_analyzer.Report[table.Name].Values.Any(rep => rep.ErrorCount > 0))
                        listTable.Errors = true;
                    break;
                case TestWorkerUpdate.TABLE_REPORT:
                    var tableReport = state.Data as Dictionary<string, AnalysisReport>;
                    Tuple<int, int> errors = tableReport.Values.Aggregate(new Tuple<int, int>(0, 0),
                        (c, r) => new Tuple<int, int>(r.ErrorCount + c.Item1, r.ErrorCount > 0 ? c.Item2 + 1 : c.Item2));
                    int suggestions = tableReport.Values.Aggregate(0, (c, r) => r.SuggestedType == null ? c : c + 1);
                    Log(string.Format("I alt: {0} fejl i {1} kolonner, {2} forslag.", errors.Item1, errors.Item2, suggestions));
                    if (errors.Item1 > 0)
                    {
                        Log("Fejl:");
                    }
                    foreach (var columnReport in tableReport.Values)
                    {
                        var column = columnReport.Column;
                        if (columnReport.ErrorCount > 0)
                        {
                            Log(string.Format("\t- Felt '{0}' af typen '{1} {2}'", column.Name, column.Type, column.Param));
                            foreach (var error in columnReport.Errors.Values)
                            {
                                if (error.Count == 0)
                                    continue;

                                Log(string.Format("\t\t- {0} ({1} forekomster)", error.Type, error.Count));
                                int i = 0;
                                foreach (dynamic instance in error.Instances)
                                {
                                    if (i >= Math.Min(10, error.Count))
                                        break;

                                    string pos = string.Format("({0}, {1})", instance.Line, instance.Pos);
                                    Log(string.Format("\t\t\t- {1} \"{0}\"", string.Join(Environment.NewLine + "\t\t\t" + string.Concat(Enumerable.Repeat(" ", pos.Length + 4)), (instance.Data as string).Split(Environment.NewLine.ToCharArray())), pos));

                                    i++;
                                }
                            }
                        }
                    }
                    if (suggestions > 0)
                    {
                        Log("Forslag:");
                    }
                    foreach (var columnReport in tableReport.Values)
                    {
                        var column = columnReport.Column;
                        var suggestion = columnReport.SuggestedType;
                        if (suggestion == null)
                            continue;

                        Log(string.Format("\t- Felt '{0}' kan ændres: {1} {2} => {3} {4}", column.Name, column.Type, column.Param, suggestion.Item1, suggestion.Item2));
                    }

                    break;
                case TestWorkerUpdate.TEST_DONE:
                    var totalErrors = _analyzer.Report.Values.Aggregate(0, (n, columnReports) => columnReports.Values.Aggregate(0, (m, columnReport) => columnReport.ErrorCount + m) + n);
                    var errorTables = _analyzer.Report.Values.Aggregate(0, (n, columnReports) => columnReports.Values.Any(columnReport => columnReport.ErrorCount > 0) ? n + 1 : n);
                    var totalSuggestions = _analyzer.Report.Values.Aggregate(0, (n, columnReports) => n + columnReports.Values.Aggregate(0, (m, columnReport) => columnReport.SuggestedType != null ? m + 1 : m));
                    var suggestionTables = _analyzer.Report.Values.Aggregate(0, (n, columnReports) => columnReports.Values.Any(columnReport => columnReport.SuggestedType != null) ? n + 1 : n);

                    Log(string.Format("Testen er afsluttet. I alt {0} fejl i {1} tabeller, og {2} foreslag i {3} tabeller.", totalErrors, errorTables, totalSuggestions, suggestionTables), LogLevel.SECTION);
                    break;
            }
        }

        void _testWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TestRunning = false;
            foreach (var listTable in Tables)
            {
                listTable.Busy = false;
            }
        }

        void _testWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            // Count total rows
            int totalRows = _analyzer.Tables.Aggregate(0, (r, t) => r + t.Rows);
            int doneRows = 0;
           

            foreach (var table in _analyzer.Tables)
            {
                worker.ReportProgress(100 * doneRows / totalRows, new { Type = TestWorkerUpdate.NEW_TABLE, Data = table });
                ListTableLookup[table.Name].Busy = true;
                _analyzer.InitializeTableAnalysis(table);
                int readRows = 0;
                do
                {
                    if (worker.CancellationPending)
                        return;
                    readRows = _analyzer.AnalyzeRows(10000);
                    worker.ReportProgress(100 * doneRows / totalRows, new { Type = TestWorkerUpdate.UPDATE_TABLE_STATUS, Data = table });
                    doneRows += readRows;
                } while (readRows > 0);
                _analyzer.DisposeTableAnalysis();

                worker.ReportProgress(100 * doneRows / totalRows, new { Type = TestWorkerUpdate.TABLE_REPORT, Data = _analyzer.Report[table.Name] });

                foreach (var report in _analyzer.Report[table.Name].Values)
                {
                    report.SuggestType();
                    worker.ReportProgress(100 * doneRows / totalRows, new { Type = TestWorkerUpdate.COLUMN_REPORT, Data = report });
                }
                ListTableLookup[table.Name].Busy = false;
                ListTableLookup[table.Name].Done = true;
            }
            worker.ReportProgress(100 * doneRows / totalRows, new { Type = TestWorkerUpdate.TEST_DONE, Data = (object)null });
        }
        #endregion

        #region Actions
        public void AddRegex(string regexText, Column regexColumn)
        {
            if (regexColumn == null)
            {
                return;
            }

            dynamic regex = new ExpandoObject();
            try
            {
                regex.Regex = new Regex(regexText);
            }
            catch (ArgumentException)
            {
                Log(string.Format("Det regulære udtryk \"{0}\" er ikke gyldigt.", regexText), LogLevel.ERROR);
            }
           
            regex.Column = regexColumn;
            Regexes.Add(regex);
        }

        public void RemoveRegex(dynamic regex)
        {
            Regexes.Remove(regex);
        }

        public void ClearLog()
        {
            LogItems.Clear();
        }

        public void SaveLog()
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.OverwritePrompt = true;
                dialog.Filter = "Tekstfil|*.txt|Logfil|*.log|Alle filtyper|*.*";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    using (var stream = dialog.OpenFile())
                    {
                        using (var writer = new StreamWriter(stream))
                        {
                            bool first = true;
                            foreach (var item in LogItems)
                            {
                                if (item.Item1 == LogLevel.SECTION)
                                {
                                    if (first)
                                    {
                                        first = false;
                                    }
                                    else
                                    {
                                        writer.Write(Environment.NewLine);
                                    }
                                    var section = item.Item2.ToLocalTime() + " " + item.Item3;
                                    writer.Write(section + Environment.NewLine + string.Concat(Enumerable.Repeat("-", section.Length)) + Environment.NewLine);
                                }
                                else
                                {
                                    writer.Write(item.Item3 + Environment.NewLine);
                                }
                            }
                        }
                    }
               } 
            }
        }

        public void LoadTables()
        {
            if (_loadWorker.IsBusy)
                return;

            LogItems.Clear();

            if (Directory.Exists(TestLocation) && File.Exists(Path.Combine(TestLocation, "Indices", "tableIndex.xml")))
            {
                TestLoaded = false;
                Log(string.Format("Indlæser tabeller fra '{0}'", TestLocation), LogLevel.SECTION);
                _loadWorker.RunWorkerAsync(TestLocation);
            }
            else
            {
                Log(string.Format("Placeringen '{0}' er ikke en gyldig arkiveringsversion.", TestLocation), LogLevel.ERROR);
            }
        }

        public void ToggleTest()
        {
            if (TestRunning)
            {
                _testWorker.CancelAsync();
            } else
            {
                foreach (var listTable in Tables)
                {
                    listTable.Busy = false;
                    listTable.Done = false;
                }

                Log("Påbegynder dataanalyse med følgende tests", LogLevel.SECTION);
                var regexList = new List<RegexTest>();
                foreach (dynamic regex in Regexes)
                {
                    var dict = new Dictionary<string, HashSet<string>>();
                    var hset = new HashSet<string>();
                    dict.Add(regex.Column.Table.Name, new HashSet<string>() { regex.Column.Name });
                    regexList.Add(new RegexTest(regex.Regex, dict));
                }
                _analyzer.RegexTests = regexList;
                TestProgress = 0;
                _analyzer.TestSelection = TestSuite.GetTestDictionary();

                foreach (var pair in _analyzer.TestSelection)
                {
                    if (pair.Value.Count > 0)
                    {
                        Log(pair.Key.ToString());
                        foreach (var testType in pair.Value)
                        {
                            Log(string.Format("\t- {0}", testType.ToString()));
                        }
                    }
                }

                _analyzer.PrepareReports();
                foreach (var listTable in Tables)
                {
                    listTable.Errors = false;
                }
                _testWorker.RunWorkerAsync();
                TestRunning = true;
            }
        }

        public void SelectLocation(string start)
        {
            start = Directory.Exists(start) ? start : @"E:\";
            using (var dialog = new System.Windows.Forms.FolderBrowserDialog())
            {
                dialog.RootFolder = Environment.SpecialFolder.MyComputer;
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    TestLocation = dialog.SelectedPath;
                }
            }

            LoadTables();
        }
        #endregion
    }
}
