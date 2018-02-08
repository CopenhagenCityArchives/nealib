﻿using System;
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
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using HardHorn.Utility;

namespace HardHorn.ViewModels
{
    class ProgressLogger : ILogger
    {
        private IProgress<Tuple<string, LogLevel>> _progress;

        public void Log(string message, LogLevel level)
        {
            _progress.Report(new Tuple<string, LogLevel>(message, level));
        }

        public ProgressLogger(ILogger logger)
        {
            _progress = new Progress<Tuple<string, LogLevel>>(logItem => logger.Log(logItem.Item1, logItem.Item2));
        }
    }

    class ArchiveVersionViewModel : Screen, ILogger
    {
        public IWindowManager windowManager = new WindowManager();

        #region Properties
        public ObservableCollection<TableComparison> TableComparisons { get; set; }
        public ObservableCollection<TableComparison> RemovedTableComparisons { get; set; }
        public ObservableCollection<TableComparison> AddedTableComparisons { get; set; }
        public ObservableCollection<Tuple<LogLevel, DateTime, string>> LogItems { get; set; }

        ICollectionView _columnsView;
        public ICollectionView ColumnsView { get { return _columnsView; } set { _columnsView = value; NotifyOfPropertyChange("ColumnsView"); } }
        public TestSelection SelectedTests { get; set; }
        public ILogger MainLogger { get; private set; }

        public int[] BarChartValues { get; set; }

        int _totalRows;
        public int TotalRows { get { return _totalRows; } set { _totalRows = value; NotifyOfPropertyChange("TotalRows"); } }

        int _doneRows;
        public int DoneRows { get { return _doneRows; } set { _doneRows = value; NotifyOfPropertyChange("DoneRows"); } }

        ArchiveVersion _archiveVersion;
        public ArchiveVersion ArchiveVersion { get { return _archiveVersion; } set { _archiveVersion = value; NotifyOfPropertyChange("ArchiveVersion"); } }

        public ObservableCollection<string> RecentLocations { get { return Properties.Settings.Default.RecentLocations; } }

        bool _showErrorReports = true;
        public bool ShowErrorReports
        {
            get { return _showErrorReports; }
            set { _showErrorReports = value; ColumnsView.Refresh(); }
        }

        bool _showSuggestionReports = true;
        public bool ShowSuggestionReports
        {
            get { return _showSuggestionReports; }
            set { _showSuggestionReports = value; ColumnsView.Refresh(); }
        }

        bool _showEmptyReports = false;
        public bool ShowEmptyReports
        {
            get { return _showEmptyReports; }
            set { _showEmptyReports = value; ColumnsView.Refresh(); }
        }

        bool _testRunning = false;
        public bool TestRunning
        {
            get { return _testRunning; }
            set { _testRunning = value; NotifyOfPropertyChange("TestRunning"); }
        }

        bool _loadingTableIndex = false;
        public bool LoadingTableIndex
        {
            get { return _loadingTableIndex; }
            set { _loadingTableIndex = value; NotifyOfPropertyChange("LoadingTableIndex"); }
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

        TableViewModel _SelectedTableViewModel = null;
        public TableViewModel SelectedTableViewModel
        {
            get { return _SelectedTableViewModel; }
            set { _SelectedTableViewModel = value; NotifyOfPropertyChange("SelectedTableViewModel"); }
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

                if (ArchiveVersion == null)
                    return;

                foreach (var spec in ArchiveVersion.CheckTableSpec(value.Split(Environment.NewLine.ToCharArray()).Select(s => s.Trim())))
                {
                    switch (spec.Item1)
                    {
                        case ArchiveVersion.TableSpecStatus.SPEC_MATCHING:
                            SpecTablesMatching += spec.Item2 + Environment.NewLine;
                            break;
                        case ArchiveVersion.TableSpecStatus.SPEC_MISSING:
                            SpecTablesMissing += spec.Item2 + Environment.NewLine;
                            break;
                        case ArchiveVersion.TableSpecStatus.SPEC_UNDEFINED:
                            SpecTablesUndefined += spec.Item2 + Environment.NewLine;
                            break;
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

        public ObservableCollection<TableViewModel> TableViewModels { get; set; }
        public ObservableCollection<TableViewModel> FilteredTableViewModels { get; set; }
        public ObservableCollection<ErrorViewModelBase> ErrorViewModels { get; set; }
        Dictionary<string, TableViewModel> TableViewModelIndex { get; set; }
        Dictionary<AnalysisTestType, ErrorViewModelBase> TestErrorViewModelIndex { get; set; }
        Dictionary<Type, ErrorViewModelBase> LoadingErrorViewModelIndex { get; set; }

        CancellationTokenSource _testCts;

        int _tabSelectedIndex = 0;
        public int TabSelectedIndex { get { return _tabSelectedIndex; } set { _tabSelectedIndex = value; NotifyOfPropertyChange("TabSelectedIndex"); } }

        enum TabNameEnum
        {
            TAB_ARCHIVEVERSION = 0,
            TAB_ERRORS = 1,
            TAB_STATISTICS = 2,
            TAB_SPECTABLE = 3,
            TAB_KEYTEST = 4,
            TAB_COMPARE = 5,
            TAB_STATUSLOG = 6
        }

        public Analyzer Analyzer { get; private set; }
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

        string _compareLocation;
        public string CompareLocation { get { return _compareLocation; } set { _compareLocation = value; NotifyOfPropertyChange("CompareLocation"); } }

        int _foreignKeyTestProgress = 0;
        public int ForeignKeyTestProgress
        {
            get { return _foreignKeyTestProgress; }
            private set { _foreignKeyTestProgress = value; NotifyOfPropertyChange("ForeignKeyTestProgress"); }
        }

        int _foreignKeyTestErrorCount = 0;
        public int ForeignKeyTestErrorCount
        {
            get { return _foreignKeyTestErrorCount; }
            private set { _foreignKeyTestErrorCount = value; NotifyOfPropertyChange("ForeignKeyTestErrorCount"); }
        }

        int _foreignKeyTestErrorTypeCount;
        public int ForeignKeyTestErrorTypeCount
        {
            get { return _foreignKeyTestErrorTypeCount; }
            private set { _foreignKeyTestErrorTypeCount = value; NotifyOfPropertyChange("ForeignKeyTestErrorTypeCount"); }
        }

        IEnumerable<KeyValuePair<ForeignKeyValue, int>> _foreignKeyOrderedErrorsSample;
        public IEnumerable<KeyValuePair<ForeignKeyValue, int>> ForeignKeyOrderedErrorsSample
        {
            get { return _foreignKeyOrderedErrorsSample; }
            private set { _foreignKeyOrderedErrorsSample = value; NotifyOfPropertyChange("ForeignKeyOrderedErrorsSample"); }
        }

        #endregion

        #region Constructors
        public ArchiveVersionViewModel(ArchiveVersion av, ILogger mainLogger)
        {
            ArchiveVersion = av;
            MainLogger = mainLogger;
            TableViewModels = new ObservableCollection<TableViewModel>(av.Tables.Select(t => new TableViewModel(t)));
            TableViewModelIndex = new Dictionary<string, TableViewModel>();
            foreach (var tableViewModel in TableViewModels) TableViewModelIndex[tableViewModel.Table.Name] = tableViewModel;
            ErrorViewModels = new ObservableCollection<ErrorViewModelBase>();
            TestErrorViewModelIndex = new Dictionary<AnalysisTestType, ErrorViewModelBase>();
            LoadingErrorViewModelIndex = new Dictionary<Type, ErrorViewModelBase>();
            FilteredTableViewModels = new ObservableCollection<TableViewModel>();
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();
            TableComparisons = new ObservableCollection<TableComparison>();
            RemovedTableComparisons = new ObservableCollection<TableComparison>();
            AddedTableComparisons = new ObservableCollection<TableComparison>();
            CurrentColumnAnalyses = new ObservableCollection<ColumnAnalysis>();
            PropertyChanged += (sender, arg) =>
            {
                if (arg.PropertyName == "SelectedTableViewModel" && SelectedTableViewModel != null)
                {
                    ColumnsView = CollectionViewSource.GetDefaultView(SelectedTableViewModel.ColumnViewModels);
                    ColumnsView.Filter = FilterColumnViewModels;
                }
            };
            Log("Så er det dælme tid til at teste datatyper!", LogLevel.SECTION);
        }
        #endregion

        #region Methods
        public bool FilterColumnViewModels(object obj)
        {
            var columnViewModel = obj as ColumnViewModel;

            bool include = false;

            if (ShowErrorReports)
            {
                include = include || (columnViewModel.Analysis != null && columnViewModel.Analysis.ErrorCount > 0);
            }

            if (ShowSuggestionReports)
            {
                include = include || (columnViewModel.Analysis != null && columnViewModel.Analysis.SuggestedType != null);
            }

            if (ShowEmptyReports)
            {
                include = include || columnViewModel.Analysis == null || (columnViewModel.Analysis.SuggestedType == null && columnViewModel.Analysis.ErrorCount == 0);
            }

            return include;
        }

        public void Log(string msg, LogLevel level = LogLevel.NORMAL)
        {
            LogItems.Add(new Tuple<LogLevel, DateTime, string>(level, DateTime.Now, msg));

            if (level == LogLevel.SECTION || level == LogLevel.ERROR)
            {
                
            }
        }

        public void OpenSelectedTableViewModel()
        {
            if (SelectedTableViewModel == null)
                return;

            var path = Path.Combine(TestLocation, "tables", SelectedTableViewModel.Table.Folder, SelectedTableViewModel.Table.Folder + ".xml");
            System.Diagnostics.Process.Start(path);
        }

        public void StopTest()
        {
            _testCts.Cancel();
        }

        public async void StartTest()
        {
            var startTestViewModel = new StartTestViewModel(this, MainLogger);
            var windowSettings = new Dictionary<string, object>();
            windowSettings.Add("Title", string.Format("Start test af {0}", ArchiveVersion.Id));
            bool? success = windowManager.ShowDialog(startTestViewModel, null, windowSettings);
            if (!success.HasValue || !success.Value)
            {
                return;
            }

            Analyzer = startTestViewModel.Analyzer;

            foreach (var tableViewModel in TableViewModels)
                foreach (var columnViewModel in tableViewModel.ColumnViewModels)
                    columnViewModel.Analysis = Analyzer.TestHierachy[tableViewModel.Table][columnViewModel.Column];

            if (TestRunning)
            {
                _testCts.Cancel();
            }
            else
            {
                _testCts = new CancellationTokenSource();
                var token = _testCts.Token;

                foreach (var tableViewModel in TableViewModels)
                {
                    tableViewModel.Busy = false;
                    tableViewModel.Done = false;
                }

                // Clear test error view models from list and index
                TestErrorViewModelIndex.Clear();
                var filteredErrorViewModels = new List<ErrorViewModelBase>();
                foreach (var errorViewModel in ErrorViewModels)
                {
                    if (errorViewModel is ColumnParsingErrorViewModel
                        || errorViewModel is ColumnTypeParsingErrorViewModel)
                    {
                        filteredErrorViewModels.Add(errorViewModel);
                    }
                }
                ErrorViewModels.Clear();
                foreach (var errorViewModel in filteredErrorViewModels)
                {
                    ErrorViewModels.Add(errorViewModel);
                }

                foreach (var table in Analyzer.TestHierachy.Values)
                    foreach (var columnAnalysis in table.Values)
                    {
                        columnAnalysis.AnalysisErrorsOccured += OnTestError;
                    }

                Log("Påbegynder dataanalyse med følgende tests", LogLevel.SECTION);

                TestProgress = 0;

                foreach (var tableViewModel in TableViewModels)
                {
                    tableViewModel.Errors = tableViewModel.Table.Columns.Any(c => c.ParameterizedDataType.DataType == DataType.UNDEFINED);
                }

                IProgress<Table> updateTableProgress = new Progress<Table>(table =>
                {
                    if (table == null) return;
                    var tableViewModel = TableViewModelIndex[table.Name];
                    if (Analyzer.TestHierachy[table].Values.Any(rep => rep.ErrorCount > 0))
                        tableViewModel.Errors = true;
                });

                IProgress<Table> showReportProgress = new Progress<Table>(table =>
                {

                    var tableReport = Analyzer.TestHierachy[table];
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
                });

                var logger = new ProgressLogger(this);

                // Run test worker
                try
                {
                    TestRunning = true;

                    await Task.Run(() =>
                    {
                        // Count total rows
                        TotalRows = ArchiveVersion.Tables.Aggregate(0, (r, t) => r + t.Rows);
                        DoneRows = 0;
                        int chunk = 10000;

                        foreach (var table in ArchiveVersion.Tables)
                        {
                            logger.Log(string.Format("Tester tabellen '{0}' ({1})", table.Name, table.Folder), LogLevel.SECTION);

                            TableViewModelIndex[table.Name].Busy = true;
                            try
                            {
                                using (var reader = table.GetReader())
                                {
                                    int readRows = 0;
                                    do
                                    {
                                        if (token.IsCancellationRequested)
                                            return;

                                        Post[,] rows;
                                        readRows = reader.Read(out rows, chunk);

                                        if (token.IsCancellationRequested)
                                            return;

                                        Analyzer.AnalyzeRows(table, rows, readRows);

                                        updateTableProgress.Report(table);

                                        DoneRows += readRows;

                                        TestProgress = (DoneRows * 100) / TotalRows;
                                    } while (readRows == chunk);

                                    // TODO: Check if number of rows in table adds up
                                }
                            }
                            catch (FileNotFoundException)
                            {
                                logger.Log(string.Format("Tabelfilen for '{0}' ({1}) findes ikke.", table.Name, table.Folder), LogLevel.ERROR);
                            }

                            bool typesSuggested = false;
                            foreach (var report in Analyzer.TestHierachy[table].Values)
                            {
                                report.SuggestType();
                                if (report.SuggestedType != null)
                                    typesSuggested = true;
                            }

                            if (typesSuggested)
                                Application.Current.Dispatcher.Invoke(() => ColumnsView.Refresh());

                            showReportProgress.Report(table);

                            TableViewModelIndex[table.Name].Busy = false;
                            TableViewModelIndex[table.Name].Done = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log(string.Format("En fejl af typen '{0}' opstod med beskeden: '{1}'. Testen afbrydes.", ex.GetType(), ex.Message), LogLevel.ERROR);
                }
                finally
                {
                    var totalErrors = Analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Aggregate(0, (m, columnAnalysis) => columnAnalysis.ErrorCount + m) + n);
                    var errorTables = Analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Any(columnAnalysis => columnAnalysis.ErrorCount > 0) ? n + 1 : n);
                    var totalSuggestions = Analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => n + columnAnalyses.Values.Aggregate(0, (m, columnAnalysis) => columnAnalysis.SuggestedType != null ? m + 1 : m));
                    var suggestionTables = Analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Any(columnAnalysis => columnAnalysis.SuggestedType != null) ? n + 1 : n);

                    Log(string.Format("Testen er afsluttet. I alt {0} fejl i {1} tabeller, og {2} foreslag i {3} tabeller.", totalErrors, errorTables, totalSuggestions, suggestionTables), LogLevel.SECTION);

                    TestRunning = false;
                    foreach (var tableViewModel in TableViewModels)
                    {
                        tableViewModel.Busy = false;
                    }
                }
            }
        }
        #endregion

        #region Actions
        public async void TestForeignKey(ForeignKey fkey)
        {
            var progressHandler = new Progress<int>(value =>
            {
                ForeignKeyTestProgress = value;
            });

            var progress = progressHandler as IProgress<int>;
            var fkeyResult = await Task.Run(() =>
            {
                int readRows;
                int doneRows = 0;
                int chunk = 10000;

                var keyValues = new HashSet<ForeignKeyValue>();

                //logger.Log("Indlæser nøgleværdier...", LogLevel.NORMAL);
                using (var reader = fkey.ReferencedTable.GetReader())
                {
                    do
                    {
                        Post[,] rows;
                        readRows = reader.Read(out rows, chunk);

                        for (int i = 0; i < readRows; i++)
                        {
                            keyValues.Add(fkey.GetReferencedValueFromRow(i, rows));
                        }

                        doneRows += readRows;
                    } while (readRows == chunk);
                }
                //logger.Log("Nøgleværdier indlæst.", LogLevel.NORMAL);

                int lastPercentage = 0;
                int thisPercentage = 0;
                var errorKeys = new Dictionary<ForeignKeyValue, int>();
                //logger.Log("Tester nøglen...", LogLevel.NORMAL);

                doneRows = 0;
                int errors = 0;
                using (var reader = fkey.Table.GetReader())
                {
                    do
                    {
                        Post[,] rows;
                        readRows = reader.Read(out rows, chunk);

                        for (int i = 0; i < readRows; i++)
                        {
                            var key = fkey.GetValueFromRow(i, rows);
                            if (!keyValues.Contains(key))
                            {
                                errors++;
                                try
                                {
                                    errorKeys[key]++;
                                }
                                catch (KeyNotFoundException)
                                {
                                    errorKeys[key] = 1;
                                }
                            }
                        }

                        doneRows += readRows;

                        thisPercentage = (doneRows * 100) / fkey.Table.Rows;
                        if (thisPercentage > lastPercentage)
                        {
                            lastPercentage = thisPercentage;
                            progress.Report(thisPercentage);
                            //logger.Log(string.Format("Progress: {0} Fejl: {1}", thisPercentage, errors), LogLevel.NORMAL);
                        }
                    } while (readRows == chunk);
                }

                //Log(string.Format("Errors: {0}", errors), LogLevel.NORMAL);
                var ordered = errorKeys.OrderBy(x => x.Value).Reverse().Take(1000);
                //foreach (var pair in ordered)
                //{
                //    Console.WriteLine(string.Format("{0}/{1}/{2}: {3}", pair.Key.Item1, pair.Key.Item2, pair.Key.Item3, pair.Value));
                //}

                return new Tuple<int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>(errors, errorKeys.Count, ordered);
            });

            ForeignKeyTestErrorCount = fkeyResult.Item1;
            ForeignKeyTestErrorTypeCount = fkeyResult.Item2;
            ForeignKeyOrderedErrorsSample = fkeyResult.Item3;
        }

        public void AddParameter()
        {
            if (SelectedTableViewModel == null || SelectedTableViewModel.SelectedColumnViewModel == null)
                return;

            if (SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.Parameter == null)
            {
                SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.Parameter = new Archiving.Parameter(new int[0]);
            }
            SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.AddParameterItem(0);
        }

        public void RemoveParameter()
        {
            if (SelectedTableViewModel == null || SelectedTableViewModel.SelectedColumnViewModel == null)
                return;

            if (SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.Parameter == null)
                return;
            if (SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.Parameter.Count == 1)
            {
                SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.Parameter = null;
                return;
            }
            if (SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.Parameter.Count > 1)
                SelectedTableViewModel.SelectedColumnViewModel.Column.ParameterizedDataType.RemoveParameterItem(0);
        }

        public void OnArchiveVersionException(Exception ex)
        {
            ErrorViewModelBase errorViewModel;

            if (!LoadingErrorViewModelIndex.ContainsKey(ex.GetType()))
            {
                if (ex is ArchiveVersionColumnParsingException)
                {
                    errorViewModel = new ColumnParsingErrorViewModel();
                }
                else if (ex is ArchiveVersionColumnTypeParsingException)
                {
                    errorViewModel = new ColumnTypeParsingErrorViewModel();
                }
                else
                {
                    return;
                }

                LoadingErrorViewModelIndex[ex.GetType()] = errorViewModel;
                Application.Current.Dispatcher.Invoke(() => ErrorViewModels.Add(errorViewModel));

            }
            else
            {
                errorViewModel = LoadingErrorViewModelIndex[ex.GetType()];
            }

            Application.Current.Dispatcher.Invoke(() => errorViewModel.Add(ex));
        }

        public void OnTestError(object sender, AnalysisErrorsOccuredArgs e)
        {
            var columnAnalysis = sender as ColumnAnalysis;

            if (SelectedTableViewModel != null && columnAnalysis.Column.Table == SelectedTableViewModel.Table)
            {
                Application.Current.Dispatcher.Invoke(() => ColumnsView.Refresh());
            }

            ErrorViewModelBase errorViewModel;

            if (!TestErrorViewModelIndex.ContainsKey(e.Test.Type))
            {
                errorViewModel = new TestErrorViewModel(e.Test.Type);
                TestErrorViewModelIndex[e.Test.Type] = errorViewModel;
                Application.Current.Dispatcher.Invoke(() => ErrorViewModels.Add(errorViewModel));
            }
            else
            {
                errorViewModel = TestErrorViewModelIndex[e.Test.Type];
            }

            Application.Current.Dispatcher.Invoke(() => errorViewModel.Add(e));
        }

        public void BrowseNext()
        {
            if (SelectedTableViewModel == null)
                return;

            SelectedTableViewModel.BrowseOffset = SelectedTableViewModel.BrowseOffset + SelectedTableViewModel.BrowseCount > SelectedTableViewModel.Table.Rows ? SelectedTableViewModel.BrowseOffset : SelectedTableViewModel.BrowseOffset + SelectedTableViewModel.BrowseCount;
            SelectedTableViewModel.UpdateBrowseRows();
        }

        public void BrowsePrevious()
        {
            if (SelectedTableViewModel == null)
                return;

            SelectedTableViewModel.BrowseOffset = SelectedTableViewModel.BrowseCount > SelectedTableViewModel.BrowseOffset ? 0 : SelectedTableViewModel.BrowseOffset - SelectedTableViewModel.BrowseCount;
            SelectedTableViewModel.UpdateBrowseRows();
        }

        public void BrowseUpdate()
        {
            if (SelectedTableViewModel == null)
                return;

            SelectedTableViewModel.UpdateBrowseRows();
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

        public void ClearLog()
        {
            LogItems.Clear();
        }

        public void GoToTable(Table table)
        {

        }

        public void GoToUndefinedColumn(ArchiveVersionColumnTypeParsingException ex)
        {
            var vm = TableViewModelIndex[ex.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
        }

        public void GoToColumn(ColumnCount c)
        {
            var vm = TableViewModelIndex[c.Column.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
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

        public void ApplySuggestion()
        {
            if (SelectedTableViewModel != null
                && SelectedTableViewModel.SelectedColumnViewModel != null
                && SelectedTableViewModel.SelectedColumnViewModel.Analysis != null
                && SelectedTableViewModel.SelectedColumnViewModel.Analysis.SuggestedType != null)
            {
                SelectedTableViewModel.SelectedColumnViewModel.Analysis.ApplySuggestion();
            }
        }

        public void ApplyAllSuggestions()
        {
            foreach (var columnAnalysis in Analyzer.TestHierachy.Values.SelectMany(d => d.Values))
            {
                columnAnalysis.ApplySuggestion();
            }
        }

        public void SaveTableIndex()
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Filter = "Xml|*.xml|Alle filtyper|*.*";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ArchiveVersion.TableIndex.ToXml().Save(dialog.FileName);
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

        public async void Compare(string location)
        {
            if (File.Exists(location))
            {
                Log(string.Format("Sammenligner '{0}' med {1}.", location, ArchiveVersion.Id), LogLevel.SECTION);
                TableComparisons.Clear();

                try
                {
                    var logger = new ProgressLogger(this);
                    IEnumerable<TableComparison> comparisons = await Task.Run(() =>
                    {
                        var tableIndex = TableIndex.ParseFile(location, logger);
                        return ArchiveVersion.CompareWithTables(tableIndex.Tables);
                    });

                    TableComparisons.Clear();
                    AddedTableComparisons.Clear();
                    RemovedTableComparisons.Clear();

                    foreach (var tc in comparisons)
                    {
                        TableComparisons.Add(tc);

                        if (tc.Removed)
                            RemovedTableComparisons.Add(tc);

                        if (tc.Added)
                            AddedTableComparisons.Add(tc);
                    }
                }
                catch (Exception ex)
                {
                    Log(string.Format("Sammenligningen med '{0}' resulterede i en undtagelse af typen {1}, med beskeden '{2}'.", CompareLocation, ex.GetType().ToString(), ex.Message), LogLevel.ERROR);
                }
            }
            else
            {
                Log(string.Format("Placeringen '{0}' er ikke en fil.", location), LogLevel.ERROR);
            }
        }

        public void Exit()
        {
            System.Windows.Application.Current.Shutdown();
        }
        #endregion
    }
}