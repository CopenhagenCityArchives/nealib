﻿using System;
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

    public enum LogLevel
    {
        NORMAL,
        ERROR,
        SUGGEST,
        SECTION
    }

    class ListTable : PropertyChangedBase
    {
        public Table Table { get; set; }

        public bool Errors
        {
            get { return _errors; }
            set { _errors = value; NotifyOfPropertyChange("Errors"); }
        }

        public bool Busy
        {
            get { return _busy; }

            set { _busy = value; NotifyOfPropertyChange("Busy"); }
        }

        bool _busy;
        bool _errors;
    }

    class TestSuiteTest : PropertyChangedBase
    {
        public AnalysisErrorType TestType { get; private set; }
        public IEnumerable<TestSuiteTest> ChildTests { get; private set; }

        bool _selected = false;
        public bool Selected
        {
            get { return _selected; }
            set
            {
                _selected = value;
                foreach (var child in ChildTests)
                {
                    child.Selected = value;
                }
                NotifyOfPropertyChange("Selected");
            }
        }

        public TestSuiteTest(AnalysisErrorType testType, IEnumerable<TestSuiteTest> children)
        {
            TestType = testType;
            ChildTests = children;
        }

        public TestSuiteTest(AnalysisErrorType testType)
        {
            TestType = testType;
            ChildTests = Enumerable.Empty<TestSuiteTest>();
        }
    }

    class TestSuiteDataTypeTest : PropertyChangedBase
    {
        public DataType DataType { get; private set; }
        public ObservableCollection<TestSuiteTest> Tests { get; private set; }

        public TestSuiteDataTypeTest(DataType dataType)
        {
            DataType = dataType;
            Tests = new ObservableCollection<TestSuiteTest>();
        }
    }

    class TestSuiteCategory : PropertyChangedBase
    {
        public string Name { get; private set; }
        public ObservableCollection<TestSuiteDataTypeTest> Children { get; private set; }
        public ObservableCollection<TestSuiteTest> Tests { get; private set; }

        public TestSuiteCategory(string name, DataType[] dataTypes, AnalysisErrorType[] testTypes)
        {
            Name = name;
            Children = new ObservableCollection<TestSuiteDataTypeTest>();

            foreach (var dataType in dataTypes)
            {
                Children.Add(new TestSuiteDataTypeTest(dataType));
            }

            Tests = new ObservableCollection<TestSuiteTest>();

            foreach (var testType in testTypes)
            {
                var childTests = new List<TestSuiteTest>();
                
                foreach (var child in Children)
                {
                    var childTest = new TestSuiteTest(testType);
                    child.Tests.Add(childTest);
                    childTests.Add(childTest);
                }

                var test = new TestSuiteTest(testType, childTests.Cast<TestSuiteTest>());
                Tests.Add(test);
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
                new AnalysisErrorType[] { AnalysisErrorType.OVERFLOW, AnalysisErrorType.UNDERFLOW }));
            testCategories.Add(new TestSuiteCategory("Tidstyper",
                new DataType[] { DataType.TIME, DataType.TIMESTAMP, DataType.INTERVAL },
                new AnalysisErrorType[] { AnalysisErrorType.OVERFLOW, AnalysisErrorType.FORMAT }));
            testCategories.Add(new TestSuiteCategory("Decimaltalstyper",
                new DataType[] { DataType.DECIMAL, DataType.DOUBLE_PRECISION, DataType.FLOAT, DataType.REAL },
                new AnalysisErrorType[] { AnalysisErrorType.OVERFLOW, AnalysisErrorType.MISMATCH }));
        }

        public Dictionary<DataType, HashSet<AnalysisErrorType>> GetTestDictionary()
        {
            var dict = new Dictionary<DataType, HashSet<AnalysisErrorType>>();

            foreach (var testCategory in testCategories)
            {
                foreach (var categoryChild in testCategory.Children)
                {
                    if (!dict.ContainsKey(categoryChild.DataType))
                    {
                        var testTypes = new HashSet<AnalysisErrorType>();
                        foreach (var test in categoryChild.Tests)
                        {
                            if (test.Selected)
                                testTypes.Add(test.TestType);
                        }
                        dict.Add(categoryChild.DataType, testTypes);
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

    class MainViewModel : PropertyChangedBase
    {
        #region Properties
        public ObservableCollection<Tuple<LogLevel, DateTime, string>> LogItems { get; set; }
        public TestSuite TestSuite { get; set; }

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
            set { _currentTable = value;  UpdateInteractiveReportView(); }
        }

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

        #endregion

        #region Constructors
        public MainViewModel()
        {
            Tables = new ObservableCollection<ListTable>();
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();
            DataTypeErrors = new ObservableCollection<AnalysisErrorType>();
            TestSuite = new TestSuite();
            Log("Så er det dælme tid til at teste datatyper!");

            // Setup test worker
            _testWorker.DoWork += _testWorker_DoWork;
            _testWorker.RunWorkerCompleted += _testWorker_RunWorkerCompleted;
            _testWorker.ProgressChanged += _testWorker_ProgressChanged;
            _testWorker.WorkerReportsProgress = true;
            _testWorker.WorkerSupportsCancellation = true;

            _loadWorker.DoWork += _loadWorker_DoWork;
            _loadWorker.RunWorkerCompleted += _loadWorker_RunWorkerCompleted;
        }
        #endregion

        #region Methods
        void Log(string msg, LogLevel level = LogLevel.NORMAL)
        {
            LogItems.Add(new Tuple<LogLevel, DateTime, string>(level, DateTime.Now, msg));
        }
        #endregion

        #region Background workers
        private void _loadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Tables.Clear();
            ListTableLookup.Clear();
            foreach (var table in _analyzer.Tables)
            {
                var listTable = new ListTable() { Table = table, Errors = false };
                ListTableLookup.Add(table.Name, listTable);
                Tables.Add(listTable);
            }
            TestLoaded = true;

            _stats = new DataStatistics(Tables.Select(lt => lt.Table).ToArray());
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
        }

        private void _loadWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            _analyzer = new DataAnalyzer(e.Argument as string);
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
                    Log("Testen er afsluttet.");
                    break;
            }
        }

        void _testWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TestRunning = false;
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
                    readRows = _analyzer.AnalyzeRows(50000);
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
            }
            worker.ReportProgress(100 * doneRows / totalRows, new { Type = TestWorkerUpdate.TEST_DONE, Data = (object)null });
        }
        #endregion

        #region Actions
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
                Log(string.Format("Lokationen '{0}' er ikke en gyldig arkiveringsversion.", TestLocation), LogLevel.ERROR);
            }
        }

        public void ToggleTest()
        {
            if (TestRunning)
            {
                _testWorker.CancelAsync();
            } else
            {
                Log("Påbegynder dataanalyse med følgende tests", LogLevel.SECTION);
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
        }
        #endregion
    }
}