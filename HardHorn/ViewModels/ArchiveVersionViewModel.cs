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
using System.Windows;
using System.Threading.Tasks;
using System.Threading;
using HardHorn.Utility;
using System.Collections;

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

    public class ArchiveVersionViewModel : Screen, ILogger
    {
        public IWindowManager windowManager = new WindowManager();

        #region Properties
        public ObservableCollection<TableComparison> TableComparisons { get; set; }
        public ObservableCollection<TableComparison> RemovedTableComparisons { get; set; }
        public ObservableCollection<TableComparison> AddedTableComparisons { get; set; }
        public ObservableCollection<ReplacementOperation> ReplacementOperations { get; set; }
        public ObservableCollection<Tuple<LogLevel, DateTime, string>> LogItems { get; set; }

        int _tableReplacementProgressValue = 0;
        public int TableReplacementProgressValue { get { return _tableReplacementProgressValue; } set { _tableReplacementProgressValue = value; NotifyOfPropertyChange("TableReplacementProgressValue"); } }

        System.Data.DataTable _replacedDataTable;
        public System.Data.DataTable ReplacedDataTable
        { get { return _replacedDataTable; } set { _replacedDataTable = value; NotifyOfPropertyChange("ReplacedDataTable"); } }

        ICollectionView _columnsView;
        ICollectionView _keyTestTableView;
        public ICollectionView ColumnsView { get { return _columnsView; } set { _columnsView = value; NotifyOfPropertyChange("ColumnsView"); } }
        public ICollectionView KeyTestTableView { get { return _keyTestTableView; } set { _keyTestTableView = value;  NotifyOfPropertyChange("KeyTestTableView"); } }
        public TestSelection SelectedTests { get; set; }
        public ILogger MainLogger { get; private set; }

        public int[] BarChartValues { get; set; }

        public int TableTestProgress
        {
            get { return AnalysisTableRowCount == 0 ? 0 : (int)(((long)AnalysisTableDoneRows) * 100 / AnalysisTableRowCount); }
        }

        public int TestProgress
        {
            get { return AnalysisTotalRowCount == 0 ? 0 : (int)(((long)AnalysisTotalDoneRows) * 100 / AnalysisTotalRowCount); }
        }

        int _analysisTotalRowCount;
        public int AnalysisTotalRowCount
        {
            get { return _analysisTotalRowCount; }
            set { _analysisTotalRowCount = value; NotifyOfPropertyChange("AnalysisTotalRowCount"); NotifyOfPropertyChange("TestProgress"); }
        }

        int _analysisTotalDoneRows;
        public int AnalysisTotalDoneRows
        {
            get { return _analysisTotalDoneRows; }
            set { _analysisTotalDoneRows = value; NotifyOfPropertyChange("AnalysisTotalDoneRows"); NotifyOfPropertyChange("TestProgress"); }
        }

        int _analysisTableRowCount;
        public int AnalysisTableRowCount
        {
            get { return _analysisTableRowCount; }
            set { _analysisTableRowCount = value; NotifyOfPropertyChange("AnalysisTableRowCount"); NotifyOfPropertyChange("TableTestProgress"); }
        }

        int _analysisTableDoneRows;
        public int AnalysisTableDoneRows
        {
            get { return _analysisTableDoneRows; }
            set { _analysisTableDoneRows = value; NotifyOfPropertyChange("AnalysisTableDoneRows"); NotifyOfPropertyChange("TableTestProgress"); }
        }

        public ArchiveVersion ArchiveVersion { get; private set; }

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
        public TableRowCountErrorViewModel TableRowCountErrorViewModel { get; set; }
        Dictionary<string, TableViewModel> TableViewModelIndex { get; set; }
        Dictionary<AnalysisTestType, ErrorViewModelBase> TestErrorViewModelIndex { get; set; }
        Dictionary<AnalysisTestType, ErrorViewModelBase> TestFailureViewModelIndex { get; set; }
        Dictionary<Type, ErrorViewModelBase> LoadingErrorViewModelIndex { get; set; }

        CancellationTokenSource _testCts;
        CancellationTokenSource _keyTestCts;

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

        public IEnumerable<DataTypeStatistic> DataTypeStatistics
        {
            get
            {
                if (_stats != null && _stats.DataTypeStatistics != null)
                {
                    return _stats.DataTypeStatistics.Values;
                }
                else
                {
                    return Enumerable.Empty<DataTypeStatistic>();
                }

            }
        }

        string _compareLocation;
        public string CompareLocation { get { return _compareLocation; } set { _compareLocation = value; NotifyOfPropertyChange("CompareLocation"); } }

        int _keyTestTotalRowCount;
        public int KeyTestTotalRowCount
        {
            get { return _keyTestTotalRowCount; }
            set { _keyTestTotalRowCount = value; NotifyOfPropertyChange("KeyTestTotalRowCount"); NotifyOfPropertyChange("KeyTestTotalProgress"); }
        }

        int _keyTestTotalDoneRows;
        public int KeyTestTotalDoneRows
        {
            get { return _keyTestTotalDoneRows; }
            set { _keyTestTotalDoneRows = value; NotifyOfPropertyChange("KeyTestTotalDoneRows"); NotifyOfPropertyChange("KeyTestTotalProgress"); }
        }

        int _keyTestTableRowCount;
        public int KeyTestTableRowCount
        {
            get { return _keyTestTableRowCount; }
            set { _keyTestTableRowCount = value; NotifyOfPropertyChange("KeyTestTableRowCount"); NotifyOfPropertyChange("KeyTestTableProgress"); }
        }

        int _keyTestTableDoneRows;
        public int KeyTestTableDoneRows
        {
            get { return _keyTestTableDoneRows; }
            set { _keyTestTableDoneRows = value; NotifyOfPropertyChange("KeyTestTableDoneRows"); NotifyOfPropertyChange("KeyTestTableProgress"); }
        }

        public int KeyTestTableProgress
        {
            get { return KeyTestTableRowCount == 0 ? 0 : (int)(((long)KeyTestTableDoneRows) * 100 / KeyTestTableRowCount); }
        }

        public int KeyTestTotalProgress
        {
            get { return KeyTestTotalRowCount == 0 ? 0 : (int)(((long)KeyTestTotalDoneRows) * 100 / KeyTestTotalRowCount); }
        }

        bool _keyTestRunning = false;
        public bool KeyTestRunning
        {
            get { return _keyTestRunning; }
            set { _keyTestRunning = value; NotifyOfPropertyChange("KeyTestRunning"); }
        }

        public ObservableCollection<Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>> ForeignKeyTestResults { get; set; }
        #endregion

        #region Constructors
        public ArchiveVersionViewModel(ILogger mainLogger, string location)
        {
            LoadingErrorViewModelIndex = new Dictionary<Type, ErrorViewModelBase>();
            ErrorViewModels = new ObservableCollection<ErrorViewModelBase>();
            TestErrorViewModelIndex = new Dictionary<AnalysisTestType, ErrorViewModelBase>();
            TestFailureViewModelIndex = new Dictionary<AnalysisTestType, ErrorViewModelBase>();
            LoadingErrorViewModelIndex = new Dictionary<Type, ErrorViewModelBase>();
            FilteredTableViewModels = new ObservableCollection<TableViewModel>();
            TableComparisons = new ObservableCollection<TableComparison>();
            RemovedTableComparisons = new ObservableCollection<TableComparison>();
            AddedTableComparisons = new ObservableCollection<TableComparison>();
            ReplacementOperations = new ObservableCollection<ReplacementOperation>();
            CurrentColumnAnalyses = new ObservableCollection<ColumnAnalysis>();
            ForeignKeyTestResults = new ObservableCollection<Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>>();
            MainLogger = mainLogger;
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();
            ArchiveVersion = ArchiveVersion.Load(location, mainLogger, OnArchiveVersionException);
            TableViewModels = new ObservableCollection<TableViewModel>(ArchiveVersion.Tables.Select(t => new TableViewModel(t)));
            TableViewModelIndex = new Dictionary<string, TableViewModel>();
            ReplacedDataTable = new System.Data.DataTable();
            foreach (var tableViewModel in TableViewModels) TableViewModelIndex[tableViewModel.Table.Name] = tableViewModel;
            PropertyChanged += (sender, arg) =>
            {
                if (arg.PropertyName == "SelectedTableViewModel" && SelectedTableViewModel != null)
                {
                    ColumnsView = CollectionViewSource.GetDefaultView(SelectedTableViewModel.ColumnViewModels);
                    ColumnsView.Filter = FilterColumnViewModels;
                }
            };
            KeyTestTableView = CollectionViewSource.GetDefaultView(TableViewModels);
            KeyTestTableView.Filter += o =>
            {
                var vm = o as TableViewModel;
                return vm != null && vm.Table.ForeignKeys.Count > 0;
            };
            Log("Indlæsningen er fuldført.", LogLevel.SECTION);
            _stats = new DataStatistics(ArchiveVersion.Tables.ToArray());
        }
        #endregion

        #region Methods
        public bool FilterColumnViewModels(object obj)
        {
            var columnViewModel = obj as ColumnViewModel;

            bool include = false;

            if (ShowErrorReports)
            {
                include = include || (columnViewModel.Column.ParameterizedDataType.DataType == DataType.UNDEFINED) || (columnViewModel.Analysis != null && columnViewModel.Analysis.ErrorCount > 0);
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
            MainLogger.Log(ArchiveVersion.Id + " - " + msg, level);
        }
        #endregion

        #region Actions
        public async void ReplaceTableToFile(TableViewModel tableViewModel)
        {
            var table = tableViewModel.Table;
            Stream stream = null;

            try // ^(\d\d\d\d)(\d\d)(\d\d)$
            {
                using (var dialog = new System.Windows.Forms.SaveFileDialog())
                {
                    dialog.Filter = "Xml|*.xml|Alle filtyper|*.*";
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        stream = dialog.OpenFile();
                    }
                    else
                    {
                        return;
                    }
                }

                IProgress<int> progress = new Progress<int>(i =>
                {
                    TableReplacementProgressValue = (i * 100) / table.Rows;
                });
            
                await Task.Run(() =>
                {
                    var replacer = new TableReplacer(table, ReplacementOperations, stream);
                    replacer.WriteHeader();
                    var reader = new TableReader(table);
                    Post[,] readPosts;
                    int totalRows = 0;
                    int readRows = 0;
                    do
                    {
                        readRows = reader.Read(out readPosts);
                        totalRows += readRows;
                        replacer.Write(readPosts, readRows);
                        progress.Report(totalRows);
                    } while (readRows > 0);
                    replacer.WriteFooter();
                });
            }
            catch (Exception ex)
            {
                MessageBox.Show(string.Format("En undtagelse af typen '{0}' forekom med beskeden:\n{1}\nStak:\n{2}", ex.GetType(), ex.Message, ex.StackTrace));
            }
            finally
            {
                if (stream != null)
                {
                    stream.Close();
                }
            }

        }

        public void ShowReplaceTable(TableViewModel table)
        {
            var dataTable = new System.Data.DataTable();
            foreach (var column in table.Table.Columns)
            {
                var dataColumn = new System.Data.DataColumn(string.Format("<{0}: {1}>", column.ColumnId, column.Name.Replace("_", "__")), typeof(Post));
                dataColumn.Caption = column.ColumnIdNumber.ToString();
                dataTable.Columns.Add(dataColumn);
            }
            ReplacedDataTable = dataTable;

            var stream = new MemoryStream();
            var originalTableReader = new TableReader(table.Table);
            Post[,] posts;
            int rowsRead = originalTableReader.Read(out posts, 1000);
            var replacer = new TableReplacer(table.Table, ReplacementOperations, stream);
            replacer.WriteHeader();
            replacer.Write(posts, rowsRead);
            replacer.WriteFooter();
            replacer.Flush();
            stream.Seek(0, SeekOrigin.Begin);
            var replacedTableReader = new TableReader(table.Table, stream);
            Post[,] replacedPosts;
            replacedTableReader.Read(out replacedPosts, rowsRead);

            ReplacedDataTable.Rows.Clear();
            for (int row = 0; row < rowsRead; row++)
            {
                var rowPosts = new Post[table.Table.Columns.Count];
                for (int col = 0; col < table.Table.Columns.Count; col++)
                {
                    rowPosts[col] = replacedPosts[row, col];
                }
                ReplacedDataTable.Rows.Add(rowPosts);
            }
        }

        public void RemoveReplacementOperation(ReplacementOperation replacement)
        {
            ReplacementOperations.Remove(replacement);
        }

        public void AddReplacementOperation(TableViewModel tableViewModel, ColumnViewModel columnViewModel, string pattern, string replacement)
        {
            Regex regex = null;
            try
            {
                regex = new Regex(pattern, RegexOptions.Compiled);
            }
            catch (ArgumentException)
            {
                MessageBox.Show(string.Format("Matchudtrykket '{0}' er ikke et gyldigt regulært udtryk. Erstatningen kunne ikke tilføjes.", pattern), "Fejl i udtryk!");
                return;
            }

            if (ReplacementOperations.Any(op => op.Column == columnViewModel.Column))
            {
                MessageBox.Show(string.Format("Der findes allerede en erstatning for kolonnen '{0}'.", columnViewModel.Column.Name), "Kolonne allerede erstattet!");
            }
            else
            {
                ReplacementOperations.Add(new ReplacementOperation(columnViewModel.Column, regex, replacement));
            }
        }

        public void OpenSelectedTableViewModel()
        {
            if (SelectedTableViewModel == null)
                return;

            var path = Path.Combine(ArchiveVersion.Path, "tables", SelectedTableViewModel.Table.Folder, SelectedTableViewModel.Table.Folder + ".xml");
            System.Diagnostics.Process.Start(path);
        }

        public void StopTest()
        {
            _testCts.Cancel();
        }

        public async void StartTest()
        {
            // Create the analyzer
            var startTestViewModel = new StartTestViewModel(this, MainLogger);
            var windowSettings = new Dictionary<string, object>();
            windowSettings.Add("Title", string.Format("Start test af {0}", ArchiveVersion.Id));
            bool? success = windowManager.ShowDialog(startTestViewModel, null, windowSettings);
            if (!success.HasValue || !success.Value)
            {
                return;
            }
            Analyzer = startTestViewModel.Analyzer;

            // Create cancallation token
            _testCts = new CancellationTokenSource();
            var token = _testCts.Token;

            // Connect the column analysis objects to the corresponding column view models
            foreach (var tableViewModel in TableViewModels)
                foreach (var columnViewModel in tableViewModel.ColumnViewModels)
                    columnViewModel.Analysis = Analyzer.TestHierachy[tableViewModel.Table][columnViewModel.Column];

            Log("Påbegynder dataanalyse.", LogLevel.SECTION);

            // Reset table view model states
            foreach (var tableViewModel in TableViewModels)
            {
                tableViewModel.AnalysisBusy = false;
                tableViewModel.AnalysisDone = false;
            }

            // Clear test error view models from list and index
            TestErrorViewModelIndex.Clear();
            TestFailureViewModelIndex.Clear();
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
            foreach (var tableViewModel in TableViewModels)
            {
                tableViewModel.AnalysisErrors = tableViewModel.Table.Columns.Any(c => c.ParameterizedDataType.DataType == DataType.UNDEFINED);
            }

            // Progress handlers
            IProgress<Table> updateTableProgress = new Progress<Table>(table =>
            {
                // Update table view model error flag
                var tableViewModel = TableViewModelIndex[table.Name];
                if (Analyzer.TestHierachy[table].Values.Any(rep => rep.ErrorCount > 0))
                    tableViewModel.AnalysisErrors = true;

                // Update error information
                if (ColumnsView != null)
                {
                    ColumnsView.Refresh();
                }

                // Update error view
                foreach (var analysis in Analyzer.TestHierachy[table].Values)
                {
                    foreach (var test in analysis.Tests)
                    {
                        if (test.ErrorCount > 0)
                        {
                            ErrorViewModelBase testErrorViewModel;
                            if (!TestErrorViewModelIndex.TryGetValue(test.Type, out testErrorViewModel))
                            {
                                testErrorViewModel = new TestErrorViewModel(test.Type);
                                TestErrorViewModelIndex[test.Type] = testErrorViewModel;
                                ErrorViewModels.Add(testErrorViewModel);
                            }

                            testErrorViewModel.Add(new ColumnCount() { Column = analysis.Column, Count = test.ErrorCount });
                            testErrorViewModel.NotifyOfPropertyChange("ErrorCount");
                        }
                    }

                    if (analysis.TestFailures.Count > 0)
                    {
                        foreach (var test in analysis.TestFailures.Keys)
                        {
                            ErrorViewModelBase testFailureViewModel;
                            if (!TestFailureViewModelIndex.TryGetValue(test.Type, out testFailureViewModel))
                            {
                                testFailureViewModel = new TestFailureViewModel(test.Type);
                                TestFailureViewModelIndex[test.Type] = testFailureViewModel;
                                ErrorViewModels.Add(testFailureViewModel);
                            }

                            foreach (var tuple in analysis.TestFailures[test])
                            {
                                testFailureViewModel.Add(tuple);
                            }
                        }
                    }
                }
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
                        Log("\t- " + column.ToString());
                        foreach (var test in columnAnalysis.Tests)
                        {
                            if (test.ErrorCount == 0)
                                continue;

                            Log(string.Format("\t\t* {0}{1} ({2} forekomster)", test.Type.ToString(), test.Type == AnalysisTestType.REGEX ? " (" + (test as Test.Pattern).Regex.ToString() + ")" : "", test.ErrorCount));
                            int i = 0;
                            foreach (var post in test.ErrorPosts)
                            {
                                if (i >= Math.Min(10, test.ErrorCount))
                                    break;

                                string pos = string.Format("({0}, {1})", post.Line, post.Position);
                                Log(string.Format("\t\t\t> {1} \"{0}\"", string.Join(Environment.NewLine + "\t\t\t" + string.Concat(Enumerable.Repeat(" ", pos.Length + 4)), (post.Data as string).Split(Environment.NewLine.ToCharArray())), pos));

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

                    Log(string.Format("\t- {0} => {1}", column.ToString(), suggestion.ToString()));
                }
            });

            var totalRowCountProgress = new Progress<int>(value => { AnalysisTotalRowCount = value; }) as IProgress<int>;
            var totalDoneRowsProgress = new Progress<int>(value => { AnalysisTotalDoneRows = value; }) as IProgress<int>;
            var tableRowCountProgress = new Progress<int>(value => { AnalysisTableRowCount = value; }) as IProgress<int>;
            var tableDoneRowsProgress = new Progress<int>(value => { AnalysisTableDoneRows = value; }) as IProgress<int>;

            var logger = new ProgressLogger(this);

            // Run test worker
            try
            {
                TestRunning = true;

                await Task.Run(() =>
                {
                    // Count total rows
                    AnalysisTotalRowCount = ArchiveVersion.Tables.Aggregate(0, (r, t) => r + t.Rows);
                    AnalysisTotalDoneRows = 0;
                    int chunk = 10000;

                    totalRowCountProgress.Report(Analyzer.TotalRowCount);

                    bool readNext = false;
                    while (Analyzer.MoveNextTable())
                    {
                        TableViewModelIndex[Analyzer.CurrentTable.Name].AnalysisBusy = true;

                        // Print information of chosen tests for this table
                        logger.Log(string.Format("Tester {0}.", Analyzer.CurrentTable.ToString()), LogLevel.SECTION);
                        foreach (var columnAnalysis in Analyzer.TestHierachy[Analyzer.CurrentTable].Values)
                        {
                            if (columnAnalysis.Tests.Count == 0)
                                continue;

                            logger.Log("\t- " + columnAnalysis.Column.ToString(), LogLevel.NORMAL);
                            foreach (var test in columnAnalysis.Tests)
                            {
                                if (test.Type == AnalysisTestType.REGEX)
                                {
                                    logger.Log(string.Format("\t\t* {0} ({1})", test.Type.ToString(), (test as Test.Pattern).Regex.ToString()), LogLevel.NORMAL);
                                }
                                else
                                {
                                    logger.Log(string.Format("\t\t* {0}", test.Type.ToString()), LogLevel.NORMAL);
                                }
                            }
                        }

                        // Initialize reader for table, handle missing file.
                        try
                        {
                            Analyzer.InitializeTable();
                            tableRowCountProgress.Report(Analyzer.TableRowCount);
                        }
                        catch (FileNotFoundException)
                        {
                            logger.Log(string.Format("Tabelfilen for '{0}' ({1}) findes ikke.", Analyzer.CurrentTable.Name, Analyzer.CurrentTable.Folder), LogLevel.ERROR);
                            continue;
                        }

                        // Perform analysis
                        do
                        {
                            readNext = Analyzer.AnalyzeRows(chunk);
                            updateTableProgress.Report(Analyzer.CurrentTable);
                            tableDoneRowsProgress.Report(Analyzer.TableDoneRows);
                            totalDoneRowsProgress.Report(Analyzer.TotalDoneRows);

                            if (_testCts.IsCancellationRequested)
                                return;
                        } while (readNext);

                        // Check if number of rows in table adds up
                        if (Analyzer.TableDoneRows != Analyzer.TableRowCount)
                        {
                            if (TableRowCountErrorViewModel == null)
                            {
                                TableRowCountErrorViewModel = new TableRowCountErrorViewModel();
                                TableRowCountErrorViewModel.Count = 1;
                                Application.Current.Dispatcher.Invoke(() => ErrorViewModels.Add(TableRowCountErrorViewModel));
                            }
                            else
                            {
                                TableRowCountErrorViewModel.Count++;
                            }

                            Application.Current.Dispatcher.Invoke(() =>
                            {
                                Log(string.Format("{0} har defineret {1} rækker, men {2} blev læst.", Analyzer.CurrentTable.ToString(), Analyzer.CurrentTable.Rows, AnalysisTableDoneRows), LogLevel.ERROR);
                                TableRowCountErrorViewModel.Add(new Tuple<Table, int>(Analyzer.CurrentTable, AnalysisTableDoneRows));
                            });
                        }

                        bool typesSuggested = false;
                        foreach (var report in Analyzer.TestHierachy[Analyzer.CurrentTable].Values)
                        {
                            report.SuggestType();
                            if (report.SuggestedType != null)
                                typesSuggested = true;
                        }

                        if (typesSuggested)
                            Application.Current.Dispatcher.Invoke(() => { if (ColumnsView != null) ColumnsView.Refresh(); });

                        showReportProgress.Report(Analyzer.CurrentTable);

                        TableViewModelIndex[Analyzer.CurrentTable.Name].AnalysisBusy = false;
                        TableViewModelIndex[Analyzer.CurrentTable.Name].AnalysisDone = true;
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
                    tableViewModel.AnalysisBusy = false;
                }
            }
        }

        public void StopKeyTest()
        {
            if (KeyTestRunning && _keyTestCts != null)
            {
                _keyTestCts.Cancel();
            }
        }

        public async void StartKeyTest(IList tableViewModels)
        {
            if (KeyTestRunning)
                return;

            var tables = new List<TableViewModel>(tableViewModels.Cast<TableViewModel>()).Select(tvm => tvm.Table);

            var keyTest = new ForeignKeyTest(tables);

            ForeignKeyTestResults.Clear();

            KeyTestRunning = true;

            var totalRowCountProgress = new Progress<int>(value => { KeyTestTotalRowCount = value; }) as IProgress<int>;
            var totalDoneRowsProgress = new Progress<int>(value => { KeyTestTotalDoneRows = value; }) as IProgress<int>;
            var tableRowCountProgress = new Progress<int>(value => { KeyTestTableRowCount = value; }) as IProgress<int>;
            var tableDoneRowsProgress = new Progress<int>(value => { KeyTestTableDoneRows = value; }) as IProgress<int>;
            var addResultProgress = new Progress<Tuple<Table, Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>>>(tuple =>
            {
                TableViewModelIndex[tuple.Item1.Name].KeyTestErrors |= tuple.Item2.Item2 > 0; // error count greater than 0
                ForeignKeyTestResults.Add(tuple.Item2);
            }) as IProgress<Tuple<Table, Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>>>;
            var tableBusyProgress = new Progress<Table>(table => TableViewModelIndex[table.Name].KeyTestBusy = true) as IProgress<Table>;
            var tableDoneProgress = new Progress<Table>(table =>
            {
                TableViewModelIndex[table.Name].KeyTestDone = true;
                TableViewModelIndex[table.Name].KeyTestBusy = false;
            }) as IProgress<Table>;

            totalRowCountProgress.Report(keyTest.TotalRowCount);

            _keyTestCts = new CancellationTokenSource();

            foreach (var table in tables)
            {
                TableViewModelIndex[table.Name].KeyTestErrors = false;
                TableViewModelIndex[table.Name].KeyTestDone = false;
                TableViewModelIndex[table.Name].KeyTestBusy = false;
            }

            try
            {
                await Task.Run(() => {
                    bool readNext = false;

                    while (keyTest.MoveNextTable())
                    {
                        tableBusyProgress.Report(keyTest.CurrentTable);

                        tableRowCountProgress.Report(keyTest.TableRowCount);
                        keyTest.InitializeReferencedValueLoading();
                        while (keyTest.MoveNextForeignKey())
                        {
                            do
                            {
                                readNext = keyTest.ReadReferencedForeignKeyValue();
                                tableDoneRowsProgress.Report(keyTest.TableDoneRows);
                                totalDoneRowsProgress.Report(keyTest.TotalDoneRows);
                                if (_keyTestCts.IsCancellationRequested)
                                {
                                    return;
                                }
                            } while (readNext);
                        } 

                        keyTest.InitializeTableTest();
                        do
                        {
                            readNext = keyTest.ReadForeignKeyValue();
                            tableDoneRowsProgress.Report(keyTest.TableDoneRows);
                            totalDoneRowsProgress.Report(keyTest.TotalDoneRows);
                            if (_keyTestCts.IsCancellationRequested)
                            {
                                return;
                            }
                        } while (readNext);

                        foreach (var foreignKey in keyTest.CurrentTable.ForeignKeys)
                        {
                            addResultProgress.Report(new Tuple<Table, Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>>(keyTest.CurrentTable, new Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>(foreignKey, keyTest.GetErrorCount(foreignKey), keyTest.GetErrorTypeCount(foreignKey), keyTest.GetOrderedErrorCounts(foreignKey))));
                        }

                        tableDoneProgress.Report(keyTest.CurrentTable);
                    }
                });
            }
            catch (Exception)
            {
                Log("En undtagelse resulterede i at nøgletesten afsluttedes.", LogLevel.ERROR);
            }
            finally
            {
                foreach (var tableViewModel in TableViewModels)
                {
                    tableViewModel.KeyTestBusy = false;
                }
                KeyTestRunning = false;
                keyTest.Dispose();
            }
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
                    errorViewModel = new XmlValidationErrorViewModel();
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

        public void GoToTable(TableRowCountViewModel viewModel)
        {
            var vm = TableViewModelIndex[viewModel.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
        }

        public void GoToUndefinedColumn(ArchiveVersionColumnTypeParsingException ex)
        {
            var vm = TableViewModelIndex[ex.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
        }

        public void GoToReferencedColumn(ReferenceViewModel referenceViewModel)
        {
            var vm = TableViewModelIndex[referenceViewModel.Reference.ReferencedColumn.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
            SelectedTableViewModel.SelectedColumnViewModel = SelectedTableViewModel.ColumnViewModels.First(cvm => cvm.Column == referenceViewModel.Reference.ReferencedColumn);
        }

        public void GoToColumn(ColumnCount c)
        {
            var vm = TableViewModelIndex[c.Column.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
            try
            {
                SelectedTableViewModel.SelectedColumnViewModel = SelectedTableViewModel.ColumnViewModels.First(cvm => cvm.Column == c.Column);
            }
            catch (Exception) { }
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

        public void SaveTableIndex(bool overwriteUnchangedDataTypes = false)
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                dialog.Filter = "Xml|*.xml|Alle filtyper|*.*";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    ArchiveVersion.TableIndex.ToXml(overwriteUnchangedDataTypes).Save(dialog.FileName);
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
                        var tableIndex = TableIndex.ParseFile(location, logger, null, false);
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
