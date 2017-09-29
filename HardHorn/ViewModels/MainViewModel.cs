using System;
using System.Collections.Generic;
using System.Linq;
using Caliburn.Micro;
using System.ComponentModel;
using HardHorn.Analysis;
using HardHorn.Statistics;
using HardHorn.Archiving;
using System.IO;
using System.Collections.ObjectModel;
using HardHorn.Logging;
using System.Dynamic;
using System.Text.RegularExpressions;
using System.Windows.Controls;
using System.Windows.Data;

namespace HardHorn.ViewModels
{
    public enum TestWorkerUpdate
    {
        NEW_TABLE,
        UPDATE_TABLE_STATUS,
        TEST_DONE,
        COLUMN_REPORT,
        TABLE_REPORT,
        TABLE_NOT_FOUND
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
        public ObservableCollection<TableComparison> TableComparisons { get; set; }
        public ObservableCollection<TableComparison> RemovedTableComparisons { get; set; }
        public ObservableCollection<TableComparison> AddedTableComparisons { get; set; }
        public ObservableCollection<Tuple<LogLevel, DateTime, string>> LogItems { get; set; }
        public TestSelection SelectedTests { get; set; }

        string _windowTitle = "HardHorn";
        public string WindowTitle { get { return _windowTitle; } set { _windowTitle = value; NotifyOfPropertyChange("WindowTitle"); } }

        public int[] BarChartValues { get; set; }

        int _totalRows;
        public int TotalRows { get { return _totalRows; } set { _totalRows = value; NotifyOfPropertyChange("TotalRows"); } }

        int _doneRows;
        public int DoneRows { get { return _doneRows; } set { _doneRows = value; NotifyOfPropertyChange("DoneRows"); } }

        ArchiveVersion _archiveVersion;
        public ArchiveVersion ArchiveVersion { get { return _archiveVersion; } set { _archiveVersion = value; NotifyOfPropertyChange("ArchiveVersion"); } }

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

        TableViewModel _currentTable = null;
        public TableViewModel CurrentTable
        {
            get { return _currentTable; }
            set { _currentTable = value; UpdateInteractiveReportView(); NotifyOfPropertyChange("CurrentTable"); }
        }

        object _selectedTableViewModelDataType;
        public object SelectedTableViewModelDataType
        {
            set
            {
                _selectedTableViewModelDataType = value;

                var dataType = value as DataType?;
                if (dataType != null)
                {
                    FilteredTableViewModels.Clear();
                    foreach (var tableViewModel in TableViewModels)
                    {
                        if (tableViewModel.Table.Columns.Any(c => c.ParameterizedDataType.DataType == dataType))
                            FilteredTableViewModels.Add(tableViewModel);
                    }
                }
                else
                {
                    FilteredTableViewModels.Clear();
                    foreach (var tableViewModel in TableViewModels)
                        FilteredTableViewModels.Add(tableViewModel);
                }

            }

            get
            {
                return _selectedTableViewModelDataType;
            }
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
                        foreach (var table in TableViewModels)
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

                foreach (var table in TableViewModels)
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

        public ObservableCollection<ColumnAnalysis> CurrentColumnAnalyses { get; set; }

        Dictionary<string, TableViewModel> ListTableLookup = new Dictionary<string, TableViewModel>();
        public ObservableCollection<TableViewModel> TableViewModels { get; set; }
        public ObservableCollection<TableViewModel> FilteredTableViewModels { get; set; }

        BackgroundWorker _loadWorker = new BackgroundWorker();
        BackgroundWorker _testWorker = new BackgroundWorker();
        BackgroundWorker _compareWorker = new BackgroundWorker();

        Analyzer _analyzer;
        public Analyzer Analyzer { get { return _analyzer; } }
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

        public ObservableCollection<RegexTestViewModel> Regexes { get; private set; }

        string _statusText = "";
        public string StatusText { get { return _statusText; } set { _statusText = value; NotifyOfPropertyChange("StatusText"); } }

        string _compareLocation;
        public string CompareLocation { get { return _compareLocation; } set { _compareLocation = value; NotifyOfPropertyChange("CompareLocation"); } }

        #endregion

        #region Constructors
        public MainViewModel()
        {
            TableViewModels = new ObservableCollection<TableViewModel>();
            FilteredTableViewModels = new ObservableCollection<TableViewModel>();
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();
            TableComparisons = new ObservableCollection<TableComparison>();
            RemovedTableComparisons = new ObservableCollection<TableComparison>();
            AddedTableComparisons = new ObservableCollection<TableComparison>();
            CurrentColumnAnalyses = new ObservableCollection<ColumnAnalysis>();
            Regexes = new ObservableCollection<RegexTestViewModel>();
           
            if (Properties.Settings.Default.SelectedTestsBase64 == null)
            {
                SelectedTests = TestSelection.GetFullSelection();
                SetDefaultSelectedTests();
            }
            else
            {
                SelectedTests = GetDefaultSelectedTests();
                if (SelectedTests == null)
                {
                    SelectedTests = TestSelection.GetFullSelection();
                    SetDefaultSelectedTests();
                }
            }

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

            _compareWorker.DoWork += _compareWorker_DoWork;
            _compareWorker.RunWorkerCompleted += _compareWorker_RunWorkerCompleted;
            _compareWorker.ProgressChanged += _loadWorker_ProgressChanged;
            _compareWorker.WorkerReportsProgress = true;
        }
        #endregion

        #region Methods
        public void UpdateInteractiveReportView()
        {
            var table = CurrentTable == null ? null : CurrentTable.Table;

            CurrentColumnAnalyses.Clear();
            if (table != null && _analyzer != null && _analyzer.TestHierachy.ContainsKey(table))
            {

                foreach (var report in _analyzer.TestHierachy[table].Values)
                {
                    if (((report.ErrorCount > 0 || report.Column.ParameterizedDataType.DataType == DataType.UNDEFINED) && ShowErrorReports) ||
                        (report.SuggestedType != null && ShowSuggestionReports) ||
                        (report.ErrorCount == 0 && report.SuggestedType == null && ShowEmptyReports))
                        CurrentColumnAnalyses.Add(report);
                }
                if (CurrentTable != null && CurrentColumnAnalyses.Count > 0)
                {
                    CurrentTable.SelectedColumnAnalysis = CurrentColumnAnalyses[0];
                }
            }
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
        private void _compareWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            if (e.Error != null)
            {
                Log(string.Format("Sammenligningen med '{0}' resulterede i en undtagelse af typen {1}, med beskeden '{2}'.", CompareLocation, e.Error.GetType().ToString(), e.Error.Message), LogLevel.ERROR);
                return;
            }

            TableComparisons.Clear();
            AddedTableComparisons.Clear();
            RemovedTableComparisons.Clear();

            foreach (var tc in e.Result as IEnumerable<TableComparison>)
            {
                TableComparisons.Add(tc);

                if (tc.Removed)
                    RemovedTableComparisons.Add(tc);

                if (tc.Added)
                    AddedTableComparisons.Add(tc);
            }
        }

        private void _compareWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            e.Result = ArchiveVersion.CompareWithTables(ArchiveVersion.LoadTableIndex(ArchiveVersion, e.Argument as string, new LoadWorkerLogger(sender as BackgroundWorker)).ToList());
        }


        private void _loadWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TableViewModels.Clear();
            ListTableLookup.Clear();
            Regexes.Clear();

            WindowTitle = string.Format("{0} - HardHorn", ArchiveVersion.Id);

            if (e.Error != null)
            {
                Log("En undtagelse forekom under indlæsningen af arkiveringsversionen, med følgende besked: " + e.Error.Message, LogLevel.ERROR);
                return;
            }

            foreach (var table in ArchiveVersion.Tables)
            {
                var tableViewModel = new TableViewModel(table) { Errors = table.Columns.Any(c => c.ParameterizedDataType.DataType == DataType.UNDEFINED) };
                ListTableLookup.Add(table.Name, tableViewModel);
                TableViewModels.Add(tableViewModel);
            }
            SelectedTableViewModelDataType = SelectedTableViewModelDataType;
            TestLoaded = true;

            _stats = new DataStatistics(TableViewModels.Select(lt => lt.Table).ToArray());
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
            _archiveVersion = ArchiveVersion.Load(e.Argument as string, loadLogger);
            _analyzer = new Analyzer(_archiveVersion, loadLogger);
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
                    var tableViewModel = ListTableLookup[table.Name];
                    if (_analyzer.TestHierachy[table].Values.Any(rep => rep.ErrorCount > 0))
                        tableViewModel.Errors = true;
                    break;
                case TestWorkerUpdate.TABLE_REPORT:
                    var tableReport = state.Data as Dictionary<Column, ColumnAnalysis>;
                    Tuple<int, int> errors = tableReport.Values.Aggregate(new Tuple<int, int>(0, 0),
                        (c, r) => new Tuple<int, int>(r.ErrorCount + c.Item1, r.ErrorCount > 0 ? c.Item2 + 1 : c.Item2));
                    int suggestions = tableReport.Values.Aggregate(0, (c, r) => r.SuggestedType == null ? c : c + 1);
                    Log(string.Format("I alt: {0} fejl i {1} kolonner, {2} forslag.", errors.Item1, errors.Item2, suggestions));
                    if (errors.Item1 > 0)
                    {
                        Log("Fejl:");
                    }
                    foreach (var columnAnalysis in tableReport.Values)
                    {
                        var column = columnAnalysis.Column;
                        if (columnAnalysis.ErrorCount > 0)
                        {
                            Log(string.Format("\t- Felt '{0}' af typen '{1} {2}'", column.Name, column.ParameterizedDataType.DataType, column.ParameterizedDataType.Parameter));
                            foreach (var test in columnAnalysis.Tests)
                            {
                                if (test.ErrorCount == 0)
                                    continue;

                                Log(string.Format("\t\t- {0} ({1} forekomster)", test.GetType(), test.ErrorCount));
                                int i = 0;
                                foreach (var post in test.ErrorPosts)
                                {
                                    if (i >= Math.Min(10, test.ErrorCount))
                                        break;

                                    string pos = string.Format("({0}, {1})", post.Line, post.Position);
                                    Log(string.Format("\t\t\t- {1} \"{0}\"", string.Join(Environment.NewLine + "\t\t\t" + string.Concat(Enumerable.Repeat(" ", pos.Length + 4)), (post.Data as string).Split(Environment.NewLine.ToCharArray())), pos));

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

                        Log(string.Format("\t- Felt '{0}' kan ændres: {1} {2} => {3} {4}", column.Name, column.ParameterizedDataType.DataType, column.ParameterizedDataType.Parameter, suggestion.DataType, suggestion.Parameter));
                    }

                    break;
                case TestWorkerUpdate.TEST_DONE:
                    var totalErrors = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Aggregate(0, (m, columnAnalysis) => columnAnalysis.ErrorCount + m) + n);
                    var errorTables = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Any(columnAnalysis => columnAnalysis.ErrorCount > 0) ? n + 1 : n);
                    var totalSuggestions = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => n + columnAnalyses.Values.Aggregate(0, (m, columnAnalysis) => columnAnalysis.SuggestedType != null ? m + 1 : m));
                    var suggestionTables = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Any(columnAnalysis => columnAnalysis.SuggestedType != null) ? n + 1 : n);

                    Log(string.Format("Testen er afsluttet. I alt {0} fejl i {1} tabeller, og {2} foreslag i {3} tabeller.", totalErrors, errorTables, totalSuggestions, suggestionTables), LogLevel.SECTION);
                    break;
                case TestWorkerUpdate.TABLE_NOT_FOUND:
                    table = state.Data;
                    Log(string.Format("Tabelfilen for '{0}' ({1}) findes ikke.", table.Name, table.Folder), LogLevel.ERROR);
                    break;
            }
        }

        void _testWorker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            TestRunning = false;
            foreach (var tableViewModel in TableViewModels)
            {
                tableViewModel.Busy = false;
            }
        }

        void _testWorker_DoWork(object sender, DoWorkEventArgs e)
        {
            var worker = sender as BackgroundWorker;

            // Count total rows
            TotalRows = ArchiveVersion.Tables.Aggregate(0, (r, t) => r + t.Rows);
            DoneRows = 0;
            int chunk = 10000;

            foreach (var table in ArchiveVersion.Tables)
            {
                worker.ReportProgress(100 * DoneRows / TotalRows, new { Type = TestWorkerUpdate.NEW_TABLE, Data = table });
                ListTableLookup[table.Name].Busy = true;
                try
                {
                    using (var reader = table.GetReader())
                    {
                        int readRows = 0;
                        do
                        {
                            if (worker.CancellationPending)
                                return;
                            Post[,] rows;
                            readRows = reader.Read(out rows, chunk);
                            if (worker.CancellationPending)
                                return;
                            _analyzer.AnalyzeRows(table, rows, readRows);
                            worker.ReportProgress(100 * DoneRows / TotalRows, new { Type = TestWorkerUpdate.UPDATE_TABLE_STATUS, Data = table });
                            DoneRows += readRows;
                        } while (readRows == chunk);
                    }
                }
                catch (FileNotFoundException)
                {
                    worker.ReportProgress(100 * DoneRows / TotalRows, new { Type = TestWorkerUpdate.TABLE_NOT_FOUND, Data = table });
                }
                

                worker.ReportProgress(100 * DoneRows / TotalRows, new { Type = TestWorkerUpdate.TABLE_REPORT, Data = _analyzer.TestHierachy[table] });

                foreach (var report in _analyzer.TestHierachy[table].Values)
                {
                    report.SuggestType();
                    worker.ReportProgress(100 * DoneRows / TotalRows, new { Type = TestWorkerUpdate.COLUMN_REPORT, Data = report });
                }
                ListTableLookup[table.Name].Busy = false;
                ListTableLookup[table.Name].Done = true;
            }
            worker.ReportProgress(100 * DoneRows / TotalRows, new { Type = TestWorkerUpdate.TEST_DONE, Data = (object)null });
        }
        #endregion

        #region Actions
        public void BrowseNext()
        {
            if (CurrentTable == null)
                return;

            CurrentTable.BrowseOffset = CurrentTable.BrowseOffset + CurrentTable.BrowseCount > CurrentTable.Table.Rows ? CurrentTable.BrowseOffset : CurrentTable.BrowseOffset + CurrentTable.BrowseCount;
            CurrentTable.UpdateBrowseRows();
        }

        public void BrowsePrevious()
        {
            if (CurrentTable == null)
                return;

            CurrentTable.BrowseOffset = CurrentTable.BrowseCount > CurrentTable.BrowseOffset ? 0 : CurrentTable.BrowseOffset - CurrentTable.BrowseCount;
            CurrentTable.UpdateBrowseRows();
        }

        public void BrowseUpdate()
        {
            if (CurrentTable == null)
                return;

            CurrentTable.UpdateBrowseRows();
        }

        public void Merge(TableComparison added, TableComparison removed)
        {
            if (added == null || removed == null)
                return;

            var tableComparison = added.NewTable.CompareTo(removed.OldTable);
            tableComparison.Name = added.Name + " / " + removed.Name;
            TableComparisons.Add(tableComparison);
            TableComparisons.Remove(added);
            TableComparisons.Remove(removed);
            AddedTableComparisons.Remove(added);
            RemovedTableComparisons.Remove(removed);
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
                Log(string.Format("Det regulære udtryk \"{0}\" er ikke gyldigt.", pattern), LogLevel.ERROR);
            }
        }

        public void RemoveRegex(RegexTestViewModel regex)
        {
            Regexes.Remove(regex);
        }

        public void ClearLog()
        {
            LogItems.Clear();
        }

        public void SaveState()
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    using (var stream = dialog.OpenFile())
                    {
                        try
                        {
                            formatter.Serialize(stream, this);
                        }
                        catch (Exception) { }
                    }
                }
            }
        }

        public void LoadState()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    MainViewModel loaded = null;
                    var formatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                    using (var stream = dialog.OpenFile())
                    {
                        try
                        {
                            loaded = formatter.Deserialize(stream) as MainViewModel;
                        }
                        catch (Exception)
                        {
                            loaded = null;
                        }
                    }

                    if (loaded != null)
                    {
                        TableViewModels = loaded.TableViewModels;
                        ListTableLookup = loaded.ListTableLookup;
                        SelectedTests = loaded.SelectedTests;
                        AddedTableComparisons = loaded.AddedTableComparisons;
                        RemovedTableComparisons = loaded.RemovedTableComparisons;
                        TableComparisons = loaded.TableComparisons;
                        TestLoaded = loaded.TestLoaded;
                        SpecTablesMatching = loaded.SpecTablesMatching;
                        SpecTablesMissing = loaded.SpecTablesMissing;
                        SpecTablesUndefined = loaded.SpecTablesUndefined;
                        StatusText = loaded.StatusText;
                        LogItems = loaded.LogItems;
                        TestLocation = loaded.TestLocation;
                        TestRunning = loaded.TestRunning;
                        TotalRows = loaded.TotalRows;
                        Regexes = loaded.Regexes;
                        ArchiveVersion = loaded.ArchiveVersion;
                        BarChartValues = loaded.BarChartValues;
                        CompareLocation = loaded.CompareLocation;
                        CurrentColumnAnalyses = loaded.CurrentColumnAnalyses;
                        CurrentTable = loaded.CurrentTable;
                        _stats = new DataStatistics(loaded.ArchiveVersion.Tables.ToArray());
                        DoneRows = loaded.DoneRows;
                        _analyzer = loaded.Analyzer;
                    }
                }
            }
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
                foreach (var tableViewModel in TableViewModels)
                {
                    tableViewModel.Busy = false;
                    tableViewModel.Done = false;
                }

                foreach (var table in _analyzer.TestHierachy.Values)
                    foreach (var columnAnalysis in table.Values)
                        columnAnalysis.Clear();

                Log("Påbegynder dataanalyse med følgende tests", LogLevel.SECTION);
                var regexList = new List<RegexTestViewModel>();
                foreach (var regex in Regexes)
                {
                    _analyzer.AddTest(regex.Column, regex.RegexTest);
                }

                TestProgress = 0;

                SetDefaultSelectedTests();

                foreach (var testSelectionCategory in SelectedTests)
                    foreach (var testTypeSelection in testSelectionCategory)
                        foreach (var dataTypeSelection in testTypeSelection)
                        {
                            if (!dataTypeSelection.Selected.HasValue || !dataTypeSelection.Selected.Value)
                                continue;

                            foreach (var column in ArchiveVersion.Columns)
                            {
                                if (dataTypeSelection.DataType == column.ParameterizedDataType.DataType)
                                {
                                    Test test;
                                    switch (testTypeSelection.TestType)
                                    {
                                        case TestSelectionType.BLANK:
                                            test = new Test.Blank();
                                            break;
                                        case TestSelectionType.OVERFLOW:
                                            test = new Test.Overflow();
                                            break;
                                        case TestSelectionType.UNDERFLOW:
                                            test = new Test.Underflow();
                                            break;
                                        case TestSelectionType.FORMAT:
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
                                    _analyzer.AddTest(column, test);
                                }
                            }
                        }

                foreach (var tableViewModel in TableViewModels)
                {
                    tableViewModel.Errors = tableViewModel.Table.Columns.Any(c => c.ParameterizedDataType.DataType == DataType.UNDEFINED);
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

                    LoadTables();
                }
            }
        }

        public void SelectTableIndex()
        {
            using (var dialog = new System.Windows.Forms.OpenFileDialog())
            {
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    CompareLocation = dialog.FileName;

                    Compare(CompareLocation);
                }
            }
        }

        public void Compare(string location)
        {
            if (File.Exists(location))
            {
                Log(string.Format("Sammenligner '{0}' med {1}.", location, ArchiveVersion.Id), LogLevel.SECTION);
                TableComparisons.Clear();
                _compareWorker.RunWorkerAsync(location);
            }
            else
            {
                Log(string.Format("Placeringen '{0}' er ikke en fil.", location), LogLevel.ERROR);
            }
        }
        #endregion
    }
}
