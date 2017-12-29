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

        bool _loadingTableIndex = false;
        public bool LoadingTableIndex
        {
            get { return _loadingTableIndex; }
            set { _loadingTableIndex = value; NotifyOfPropertyChange("LoadingTableIndex"); }
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

        TableViewModel _SelectedTableViewModel = null;
        public TableViewModel SelectedTableViewModel
        {
            get { return _SelectedTableViewModel; }
            set { _SelectedTableViewModel = value; UpdateInteractiveReportView(); NotifyOfPropertyChange("SelectedTableViewModel"); }
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

        Dictionary<string, TableViewModel> ListTableLookup = new Dictionary<string, TableViewModel>();
        public ObservableCollection<TableViewModel> TableViewModels { get; set; }
        public ObservableCollection<TableViewModel> FilteredTableViewModels { get; set; }
        public ObservableCollection<ErrorViewModelBase> ErrorViewModels { get; set; }
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
            TAB_COMPARE = 4,
            TAB_STATUSLOG = 5
        }

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
            ErrorViewModels = new ObservableCollection<ErrorViewModelBase>();
            TestErrorViewModelIndex = new Dictionary<AnalysisTestType, ErrorViewModelBase>();
            LoadingErrorViewModelIndex = new Dictionary<Type, ErrorViewModelBase>();
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
                //SelectedTests = GetDefaultSelectedTests();
                SelectedTests = TestSelection.GetFullSelection();
                SetDefaultSelectedTests();
            }

            Log("Så er det dælme tid til at teste datatyper!", LogLevel.SECTION);
        }
        #endregion

        #region Methods
        public void UpdateInteractiveReportView()
        {
            var table = SelectedTableViewModel == null ? null : SelectedTableViewModel.Table;

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
                if (SelectedTableViewModel != null && CurrentColumnAnalyses.Count > 0)
                {
                    SelectedTableViewModel.SelectedColumnAnalysis = CurrentColumnAnalyses[0];
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

        public void OpenSelectedTableViewModel()
        {
            if (SelectedTableViewModel == null)
                return;

            var path = Path.Combine(TestLocation, "tables", SelectedTableViewModel.Table.Folder, SelectedTableViewModel.Table.Folder + ".xml");
            System.Diagnostics.Process.Start(path);
        }

        #endregion

        #region Actions
        public void AddParameter()
        {
            if (SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.Parameter == null)
            {
                SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.Parameter = new Archiving.Parameter(false, new int[0]);
            }
            SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.AddParameterItem(0);
        }

        public void RemoveParameter()
        {
            if (SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.Parameter == null)
                return;
            if (SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.Parameter.Count == 1)
            {
                SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.Parameter = null;
                return;
            }
            if (SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.Parameter.Count > 1)
                SelectedTableViewModel.SelectedColumnAnalysis.Column.ParameterizedDataType.RemoveParameterItem(0);
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

        public void GoToTable(Table table)
        {

        }

        public void GoToUndefinedColumn(ArchiveVersionColumnTypeParsingException ex)
        {
            var vm = ListTableLookup[ex.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
            vm.SelectedColumnAnalysis = CurrentColumnAnalyses.First(ca => ca.Column.ColumnId == ex.Id);
        }

        public void GoToColumn(ColumnCount c)
        {
            var vm = ListTableLookup[c.Column.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
            vm.SelectedColumnAnalysis = CurrentColumnAnalyses.First(ca => ca.Column == c.Column);
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
                        SelectedTableViewModel = loaded.SelectedTableViewModel;
                        _stats = new DataStatistics(loaded.ArchiveVersion.Tables.ToArray());
                        DoneRows = loaded.DoneRows;
                        _analyzer = loaded.Analyzer;
                    }
                }
            }
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
            if (SelectedTableViewModel != null && _SelectedTableViewModel.SelectedColumnAnalysis != null)
            {
                SelectedTableViewModel.SelectedColumnAnalysis.ApplySuggestion();
            }
        }

        public void ApplyAllSuggestions()
        {
            foreach (var columnAnalysis in _analyzer.TestHierachy.Values.SelectMany(d => d.Values))
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

        public async void LoadTables()
        {
            if (Directory.Exists(TestLocation) && File.Exists(Path.Combine(TestLocation, "Indices", "tableIndex.xml")))
            {
                TableViewModels.Clear();
                ListTableLookup.Clear();

                ErrorViewModels.Clear();
                TestErrorViewModelIndex.Clear();
                LoadingErrorViewModelIndex.Clear();
                Regexes.Clear();

                LoadingTableIndex = true;
                TestLoaded = false;
                Log(string.Format("Indlæser tabeller fra '{0}'", TestLocation), LogLevel.SECTION);
                try
                {
                    var logger = new ProgressLogger(this);
                    await Task.Run(() =>
                    {
                        _archiveVersion = ArchiveVersion.Load(TestLocation, logger, OnArchiveVersionException);
                        _analyzer = new Analyzer(_archiveVersion, logger);
                    });
                }
                catch (Exception ex)
                {
                    Log("En undtagelse forekom under indlæsningen af arkiveringsversionen, med følgende besked: " + ex.Message, LogLevel.ERROR);
                    return;
                }
                finally
                {
                    LoadingTableIndex = false;
                }

                WindowTitle = string.Format("{0} - HardHorn", ArchiveVersion.Id);

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
            else
            {
                Log(string.Format("Placeringen '{0}' er ikke en gyldig arkiveringsversion.", TestLocation), LogLevel.ERROR);
            }
        }

        public async void ToggleTest()
        {
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

                foreach (var table in _analyzer.TestHierachy.Values)
                    foreach (var columnAnalysis in table.Values)
                    {
                        columnAnalysis.Clear();
                        columnAnalysis.AnalysisErrorsOccured += OnTestError;
                    }

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
                                        case AnalysisTestType.BLANK:
                                            test = new Test.Blank();
                                            break;
                                        case AnalysisTestType.OVERFLOW:
                                            test = new Test.Overflow();
                                            break;
                                        case AnalysisTestType.UNDERFLOW:
                                            test = new Test.Underflow();
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
                                    _analyzer.AddTest(column, test);
                                }
                            }
                        }

                foreach (var tableViewModel in TableViewModels)
                {
                    tableViewModel.Errors = tableViewModel.Table.Columns.Any(c => c.ParameterizedDataType.DataType == DataType.UNDEFINED);
                }

                IProgress<Table> updateTableProgress = new Progress<Table>(table =>
                {
                    if (table == null) return;
                        var tableViewModel = ListTableLookup[table.Name];
                        if (_analyzer.TestHierachy[table].Values.Any(rep => rep.ErrorCount > 0))
                            tableViewModel.Errors = true;
                });

                IProgress<Table> showReportProgress = new Progress<Table>(table =>
                {

                    var tableReport = _analyzer.TestHierachy[table];
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

                            ListTableLookup[table.Name].Busy = true;
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

                                        _analyzer.AnalyzeRows(table, rows, readRows);

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

                            foreach (var report in _analyzer.TestHierachy[table].Values)
                            {
                                report.SuggestType();
                            }

                            showReportProgress.Report(table);

                            ListTableLookup[table.Name].Busy = false;
                            ListTableLookup[table.Name].Done = true;
                        }
                    });
                }
                catch (Exception ex)
                {
                    Log(string.Format("En fejl af typen '{0}' opstod med beskeden: '{1}'. Testen afbrydes.", ex.GetType(), ex.Message), LogLevel.ERROR);
                }
                finally
                {
                    var totalErrors = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Aggregate(0, (m, columnAnalysis) => columnAnalysis.ErrorCount + m) + n);
                    var errorTables = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Any(columnAnalysis => columnAnalysis.ErrorCount > 0) ? n + 1 : n);
                    var totalSuggestions = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => n + columnAnalyses.Values.Aggregate(0, (m, columnAnalysis) => columnAnalysis.SuggestedType != null ? m + 1 : m));
                    var suggestionTables = _analyzer.TestHierachy.Values.Aggregate(0, (n, columnAnalyses) => columnAnalyses.Values.Any(columnAnalysis => columnAnalysis.SuggestedType != null) ? n + 1 : n);

                    Log(string.Format("Testen er afsluttet. I alt {0} fejl i {1} tabeller, og {2} foreslag i {3} tabeller.", totalErrors, errorTables, totalSuggestions, suggestionTables), LogLevel.SECTION);

                    TestRunning = false;
                    foreach (var tableViewModel in TableViewModels)
                    {
                        tableViewModel.Busy = false;
                    }
                }
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
        #endregion
    }
}
