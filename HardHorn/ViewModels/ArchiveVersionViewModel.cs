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
using System.Globalization;
using System.Web;

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
        public ICollectionView InteractiveTablesView { get; set; }
        public ICollectionView ColumnsView { get { return _columnsView; } set { _columnsView = value; NotifyOfPropertyChange("ColumnsView"); } }
        public ICollectionView KeyTestTablesView { get { return _keyTestTableView; } set { _keyTestTableView = value;  NotifyOfPropertyChange("KeyTestTableView"); } }
        public ICollectionView ReplacementOperationsView { get; set; }
        public TestSelection SelectedTests { get; set; }
        public ILogger MainLogger { get; private set; }

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

        ArchiveVersion _archiveVersion;
        public ArchiveVersion ArchiveVersion
        {
            get
            {
                return _archiveVersion;
            }
            set
            {
                _archiveVersion = value;

                // Setup table view models
                TableViewModels.Clear();
                TableViewModelIndex.Clear();
                foreach (var table in _archiveVersion.Tables)
                {
                    var vm = new TableViewModel(table);
                    TableViewModels.Add(vm);
                    TableViewModelIndex[table.Name] = vm;
                }

                foreach (var tvm in TableViewModels)
                {
                    tvm.InitializeIncomingForeignKeys(this);
                }

                NotifyOfPropertyChange("ArchiveVersion");
            }
        }

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

        public List<TableViewModel> SelectedTableViewModels { get; set; }

        TableViewModel _SelectedTableViewModel = null;
        public TableViewModel SelectedTableViewModel
        {
            get { return _SelectedTableViewModel; }
            set { _SelectedTableViewModel = value; NotifyOfPropertyChange("SelectedTableViewModel"); }
        }

        object _interactiveFilterDataType;
        public object InteractiveFilterDataType
        {
            get
            {
                return _interactiveFilterDataType;
            }
            set
            {
                _interactiveFilterDataType = value;
                InteractiveTablesView.Refresh();
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
        public Dictionary<string, TableViewModel> TableViewModelIndex { get; set; }

        Dictionary<Column, Dictionary<AnalysisTestType, NotificationViewModel>> AnalysisErrorNotificationIndex { get; set; }
        Dictionary<string, NotificationViewModel> ForeignKeyTestErrorNotificationIndex { get; set; }
        Dictionary<string, NotificationViewModel> ForeignKeyTestBlankNotificationIndex { get; set; }
        public ObservableCollection<NotificationViewModel> Notifications { get; set; }
        System.Timers.Timer Notifications_RefreshViewTimer = new System.Timers.Timer(1000);

        public void Notifications_RefreshViews()
        {
            if (!Notifications_RefreshViewTimer.Enabled)
            {
                Notifications_RefreshViewTimer.Start();
            }
        }

        public void Notifications_RefreshViewTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs a)
        {
            Application.Current.Dispatcher.Invoke(() =>
            {
                Notifications_RefreshViewTimer.Stop();
                NotificationsView.Refresh();
                NotificationsCategoryView.Refresh();
                NotificationCount = NotificationsView.Cast<object>().Count();
            });
        }

        int notificationCount = 0;
        public int NotificationCount
        {
            get { return notificationCount; }
            set { notificationCount = value; NotifyOfPropertyChange("NotificationCount"); }
        }
        
        bool notifications_ShowHints = true;
        public bool Notifications_ShowHints
        {
            get { return notifications_ShowHints; }
            set { notifications_ShowHints = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowErrors = true;
        public bool Notifications_ShowErrors
        {
            get { return notifications_ShowErrors; }
            set { notifications_ShowErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowHintsWhereErrors = true;
        public bool Notifications_ShowHintsWhereErrors
        {
            get { return notifications_ShowHintsWhereErrors; }
            set { notifications_ShowHintsWhereErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowOverflow = true;
        public bool Notifications_ShowOverflow
        {
            get { return notifications_ShowOverflow; }
            set { notifications_ShowOverflow = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowUnderflow = true;
        public bool Notifications_ShowUnderflow
        {
            get { return notifications_ShowUnderflow; }
            set { notifications_ShowUnderflow = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowFormat = true;
        public bool Notifications_ShowFormat
        {
            get { return notifications_ShowFormat; }
            set { notifications_ShowFormat = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowBlank = true;
        public bool Notifications_ShowBlank
        {
            get { return notifications_ShowBlank; }
            set { notifications_ShowBlank = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowRegex = true;
        public bool Notifications_ShowRegex
        {
            get { return notifications_ShowRegex; }
            set { notifications_ShowRegex = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowSuspiciousKeywords = true;
        public bool Notifications_ShowSuspiciousKeywords
        {
            get { return notifications_ShowSuspiciousKeywords; }
            set { notifications_ShowSuspiciousKeywords = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowRepeatingChar = true;
        public bool Notifications_ShowRepeatingChar
        {
            get { return notifications_ShowRepeatingChar; }
            set { notifications_ShowRepeatingChar = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowForeignKeyTestErrors = true;
        public bool Notifications_ShowForeignKeyTestErrors
        {
            get { return notifications_ShowForeignKeyTestErrors; }
            set { notifications_ShowForeignKeyTestErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowForeignKeyTestBlanks = true;
        public bool Notifications_ShowForeignKeyTestBlanks
        {
            get { return notifications_ShowForeignKeyTestBlanks; }
            set { notifications_ShowForeignKeyTestBlanks = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowParameterSuggestions = true;
        public bool Notifications_ShowParameterSuggestions
        {
            get { return notifications_ShowParameterSuggestions; }
            set { notifications_ShowParameterSuggestions = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowDatatypeSuggestions = true;
        public bool Notifications_ShowDatatypeSuggestions
        {
            get { return notifications_ShowDatatypeSuggestions; }
            set { notifications_ShowDatatypeSuggestions = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowXmlValidationErrors = true;
        public bool Notifications_ShowXmlValidationErrors
        {
            get { return notifications_ShowXmlValidationErrors; }
            set { notifications_ShowXmlValidationErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowColumnErrors = true;
        public bool Notifications_ShowColumnErrors
        {
            get { return notifications_ShowColumnErrors; }
            set { notifications_ShowColumnErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowColumnTypeErrors = true;
        public bool Notifications_ShowColumnTypeErrors
        {
            get { return notifications_ShowColumnTypeErrors; }
            set { notifications_ShowColumnTypeErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowDataTypeIllegalAliasErrors = true;
        public bool Notifications_ShowDataTypeIllegalAliasErrors
        {
            get { return notifications_ShowDataTypeIllegalAliasErrors; }
            set { notifications_ShowDataTypeIllegalAliasErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowForeignKeyTypeErrors = true;
        public bool Notifications_ShowForeignKeyTypeErrors
        {
            get { return notifications_ShowForeignKeyTypeErrors; }
            set { notifications_ShowForeignKeyTypeErrors = value; Notifications_RefreshViews(); }
        }
        bool notifications_ShowTableRowCountErrors = true;
        public bool Notifications_ShowTableRowCountErrors
        {
            get { return notifications_ShowTableRowCountErrors; }
            set { notifications_ShowTableRowCountErrors = value; Notifications_RefreshViews(); }
        }
        public ICollectionView NotificationsView { get; set; }
        public ICollectionView NotificationsCategoryView { get; set; }
        public int Notifications_SelectedGroupingIndex { get; set; }

        CancellationTokenSource _testCts;
        CancellationTokenSource _keyTestCts;

        int _tabSelectedIndex = 0;
        public int TabSelectedIndex { get { return _tabSelectedIndex; } set { _tabSelectedIndex = value; NotifyOfPropertyChange("TabSelectedIndex"); } }

        enum TabNameEnum
        {
            TAB_ARCHIVEVERSION = 0,
            TAB_METADATA = 1,
            TAB_ERRORS = 2,
            TAB_NOTIFICATIONS = 3,
            TAB_STATISTICS = 4,
            TAB_SPECTABLE = 5,
            TAB_KEYTEST = 6,
            TAB_COMPARE = 7,
            TAB_REPLACE = 8,
            TAB_STATUSLOG = 9
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

        string _tableColumnNameFilter = string.Empty;
        public string TableColumnNameFilter
        {
            get { return _tableColumnNameFilter; }
            set{ _tableColumnNameFilter = value; NotifyOfPropertyChange("TableColumnNameFilter"); InteractiveTablesView.Refresh(); }
        }

        public ObservableCollection<Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>> ForeignKeyTestResults { get; set; }
        public ICollectionView ReplaceTablesView { get; private set; }
        public int TableReplacementTotalReplacements { get; private set; }
        public string TempName { get; private set; }
        public CollectionViewSource InteractiveTablesViewSource { get; private set; }
        #endregion

        public void UpdateSelection(SelectionChangedEventArgs ea)
        {
            var added = ea.AddedItems.Cast<TableViewModel>();
            var removed = ea.RemovedItems.Cast<TableViewModel>(); ;
            SelectedTableViewModels.RemoveAll(tvm => removed.Contains(tvm));
            SelectedTableViewModels.AddRange(added);
        }

        CollectionViewSource NotificationsViewSource;
        CollectionViewSource NotificationsCategoryViewSource;

        class NotificationTypeToStringConverter : IValueConverter
        {
            public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            {
                var notificationType = value as NotificationType?;
                return notificationType?.ToString();
            }

            public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            {
                throw new NotImplementedException();
            }
        }

        #region Constructors
        public ArchiveVersionViewModel(ILogger mainLogger, string tempName)
        {
            TempName = tempName;
            AnalysisErrorNotificationIndex = new Dictionary<Column, Dictionary<AnalysisTestType, NotificationViewModel>>();
            ForeignKeyTestErrorNotificationIndex = new Dictionary<string, NotificationViewModel>();
            ForeignKeyTestBlankNotificationIndex = new Dictionary<string, NotificationViewModel>();
            Notifications = new ObservableCollection<NotificationViewModel>();
            Notifications.CollectionChanged += (o, a) => Notifications_RefreshViews();
            NotificationsViewSource = new CollectionViewSource() { Source = Notifications };
            NotificationsView = NotificationsViewSource.View;
            NotificationsCategoryViewSource = new CollectionViewSource() { Source = Notifications };
            NotificationsCategoryView = NotificationsCategoryViewSource.View;
            NotificationsView.GroupDescriptions.Add(new PropertyGroupDescription("Table"));
            NotificationsView.SortDescriptions.Add(new SortDescription("Table.FolderNumber", ListSortDirection.Ascending));
            NotificationsView.SortDescriptions.Add(new SortDescription("Column.ColumnIdNumber", ListSortDirection.Ascending));
            NotificationsView.SortDescriptions.Add(new SortDescription("Type", ListSortDirection.Ascending));
            NotificationsView.SortDescriptions.Add(new SortDescription("Header", ListSortDirection.Ascending));
            NotificationsView.SortDescriptions.Add(new SortDescription("Message", ListSortDirection.Ascending));
            NotificationsView.Filter += Notifications_Filter;
            NotificationsCategoryView.SortDescriptions.Add(new SortDescription("Type", ListSortDirection.Ascending));
            NotificationsCategoryView.SortDescriptions.Add(new SortDescription("Header", ListSortDirection.Ascending));
            NotificationsCategoryView.SortDescriptions.Add(new SortDescription("Table.FolderNumber", ListSortDirection.Ascending));
            NotificationsCategoryView.SortDescriptions.Add(new SortDescription("Column.ColumnIdNumber", ListSortDirection.Ascending));
            NotificationsCategoryView.SortDescriptions.Add(new SortDescription("Message", ListSortDirection.Ascending));
            NotificationsCategoryView.GroupDescriptions.Add(new PropertyGroupDescription("Type"));
            NotificationsCategoryView.Filter += Notifications_Filter;
            Notifications_RefreshViewTimer.Elapsed += Notifications_RefreshViewTimer_Elapsed;
            TableComparisons = new ObservableCollection<TableComparison>();
            RemovedTableComparisons = new ObservableCollection<TableComparison>();
            AddedTableComparisons = new ObservableCollection<TableComparison>();
            ReplacementOperations = new ObservableCollection<ReplacementOperation>();
            CurrentColumnAnalyses = new ObservableCollection<ColumnAnalysis>();
            ForeignKeyTestResults = new ObservableCollection<Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>>();
            MainLogger = mainLogger;
            SelectedTableViewModels = new List<TableViewModel>();
            LogItems = new ObservableCollection<Tuple<LogLevel, DateTime, string>>();
            TableViewModels = new ObservableCollection<TableViewModel>();
            TableViewModelIndex = new Dictionary<string, TableViewModel>();
            ReplacedDataTable = new System.Data.DataTable();
            PropertyChanged += (sender, arg) =>
            {
                if (arg.PropertyName == "SelectedTableViewModel" && SelectedTableViewModel != null)
                {
                    ColumnsView = CollectionViewSource.GetDefaultView(SelectedTableViewModel.ColumnViewModels);
                    ColumnsView.Filter = FilterColumnViewModels;
                }
            };
            KeyTestTablesView = new CollectionViewSource() { Source = TableViewModels }.View;
            KeyTestTablesView.Filter += o =>
            {
                var vm = o as TableViewModel;
                return vm != null && vm.Table.ForeignKeys.Count > 0;
            };
            ReplacementOperationsView = CollectionViewSource.GetDefaultView(ReplacementOperations);
            ReplacementOperationsView.GroupDescriptions.Add(new PropertyGroupDescription("Table"));
            InteractiveTablesViewSource = new CollectionViewSource() { Source = TableViewModels };
            InteractiveTablesView = InteractiveTablesViewSource.View;
            InteractiveTablesView.Filter += o =>
            {
                var vm = o as TableViewModel;
                var dataTypeFilter = InteractiveFilterDataType as DataType?;
                var includeDataType = !dataTypeFilter.HasValue || vm.Table.Columns.Any(c => c.ParameterizedDataType.DataType == dataTypeFilter.Value);
                var includeNames = vm.Table.Name.ToLower().Contains(TableColumnNameFilter.ToLower()) || vm.Table.Columns.Any(c => c.Name.ToLower().Contains(TableColumnNameFilter.ToLower())) || vm.Table.Folder.ToLower().Contains(TableColumnNameFilter.ToLower());
                return vm != null && includeDataType && includeNames;
            };
            ReplaceTablesView = new CollectionViewSource() { Source = TableViewModels }.View;
        }
        #endregion

        #region Callbacks
        public void HandleNotification(INotification notification)
        {
            NotificationViewModel notificationViewModel = null;
            switch (notification.Type)
            {
                case NotificationType.AnalysisErrorBlank:
                case NotificationType.AnalysisErrorOverflow:
                case NotificationType.AnalysisErrorUnderflow:
                //case NotificationType.AnalysisErrorFormat:
                case NotificationType.AnalysisErrorRegex:
                case NotificationType.AnalysisErrorRepeatingChar:
                case NotificationType.AnalysisErrorUnallowedKeyword:
                    if (!AnalysisErrorNotificationIndex.ContainsKey(notification.Column))
                    {
                        AnalysisErrorNotificationIndex[notification.Column] = new Dictionary<AnalysisTestType, NotificationViewModel>();
                    }

                    var testType = (notification as AnalysisErrorNotification).TestType;
                    
                    // if testType for column already exists
                    if (AnalysisErrorNotificationIndex[notification.Column].ContainsKey(testType))
                    {   // increment no of testType occurence in Index 
                        AnalysisErrorNotificationIndex[notification.Column][testType].Count++;
                        if (notification.Type == NotificationType.AnalysisErrorUnallowedKeyword)
                        {
                            AnalysisErrorNotificationIndex[notification.Column][testType].Message = notification.Message;
                        }
                        if (notification.Type == NotificationType.AnalysisErrorRepeatingChar)
                        {
                            if (! (AnalysisErrorNotificationIndex[notification.Column][testType].Message.Contains(notification.Message)))
                            {
                                AnalysisErrorNotificationIndex[notification.Column][testType].Message += notification.Message;
                            }
                        }
                    }

                    else
                    {
                        notificationViewModel = new NotificationViewModel(notification);
                        AnalysisErrorNotificationIndex[notification.Column][(notification as AnalysisErrorNotification).TestType] = notificationViewModel;
                    }
                    break;
                case NotificationType.ForeignKeyTestError:
                    var foreignKeyTestErrorNotification = notification as ForeignKeyTestErrorNotification;
                    if (ForeignKeyTestErrorNotificationIndex.ContainsKey(foreignKeyTestErrorNotification.ForeignKey.Name))
                    {
                        ForeignKeyTestErrorNotificationIndex[foreignKeyTestErrorNotification.ForeignKey.Name].Count = foreignKeyTestErrorNotification.Count;
                    }
                    else
                    {
                        notificationViewModel = new NotificationViewModel(notification);
                        ForeignKeyTestErrorNotificationIndex[foreignKeyTestErrorNotification.ForeignKey.Name] = notificationViewModel;
                    }
                    break;
                case NotificationType.ForeignKeyTestBlank:
                    var foreignKeyTestBlankNotification = notification as ForeignKeyTestBlankNotification;
                    if (ForeignKeyTestBlankNotificationIndex.ContainsKey(foreignKeyTestBlankNotification.ForeignKey.Name))
                    {
                        ForeignKeyTestBlankNotificationIndex[foreignKeyTestBlankNotification.ForeignKey.Name].Count = foreignKeyTestBlankNotification.Count;
                    }
                    else
                    {
                        notificationViewModel = new NotificationViewModel(notification);
                        ForeignKeyTestBlankNotificationIndex[foreignKeyTestBlankNotification.ForeignKey.Name] = notificationViewModel;
                    }
                    break;
                default:
                    notificationViewModel = new NotificationViewModel(notification);
                    break;
            }

            if (notificationViewModel != null)
            {
                Application.Current.Dispatcher.Invoke(() =>
                {
                    Notifications.Add(notificationViewModel);
                });
            }
        }
        #endregion

        #region Methods
        private bool Notifications_Filter(object obj)
        {
            var nvm = obj as NotificationViewModel; 
            bool includeBySeverity = (nvm.Severity == Severity.Hint && Notifications_ShowHints)
                                  || (nvm.Severity == Severity.Error && Notifications_ShowErrors)
                                  || (nvm.Severity == Severity.Hint && Notifications_ShowHintsWhereErrors && AnalysisErrorNotificationIndex.ContainsKey(nvm.Column));
            bool includeByNotificationType = (nvm.Type == NotificationType.AnalysisErrorOverflow && Notifications_ShowOverflow)
                || (nvm.Type == NotificationType.AnalysisErrorUnderflow && Notifications_ShowUnderflow)
                || (nvm.Type == NotificationType.AnalysisErrorFormat && Notifications_ShowFormat)
                || (nvm.Type == NotificationType.AnalysisErrorBlank && Notifications_ShowBlank)
                || (nvm.Type == NotificationType.AnalysisErrorRegex && Notifications_ShowRegex)
                || (nvm.Type == NotificationType.ForeignKeyTestError && Notifications_ShowForeignKeyTestErrors)
                || (nvm.Type == NotificationType.ForeignKeyTestBlank && Notifications_ShowForeignKeyTestBlanks)
                || (nvm.Type == NotificationType.ParameterSuggestion && Notifications_ShowParameterSuggestions)
                || (nvm.Type == NotificationType.DataTypeSuggestion && Notifications_ShowDatatypeSuggestions)
                || (nvm.Type == NotificationType.XmlError && Notifications_ShowXmlValidationErrors)
                || (nvm.Type == NotificationType.ColumnParsing && Notifications_ShowColumnErrors)
                || (nvm.Type == NotificationType.ColumnTypeError && Notifications_ShowColumnTypeErrors)
                || (nvm.Type == NotificationType.DataTypeIllegalAlias && Notifications_ShowDataTypeIllegalAliasErrors)
                || (nvm.Type == NotificationType.ForeignKeyTypeError && Notifications_ShowForeignKeyTypeErrors)
                || (nvm.Type == NotificationType.TableRowCountError && Notifications_ShowTableRowCountErrors)
                || (nvm.Type == NotificationType.AnalysisErrorRepeatingChar && Notifications_ShowRepeatingChar)
                || (nvm.Type == NotificationType.AnalysisErrorUnallowedKeyword && Notifications_ShowSuspiciousKeywords);

            return includeBySeverity && includeByNotificationType;
        }

        public void RunStatistics()
        {
            _stats = new DataStatistics(ArchiveVersion.Tables.ToArray());
        }

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
            if (ArchiveVersion == null)
            {
                MainLogger.Log("Indlæser " + TempName + " - " + msg, level);
            }
            else
            {
                MainLogger.Log(ArchiveVersion.Id + " - " + msg, level);
            }
        }
        #endregion

        #region Actions
        public void Notifications_ExportHTML()
        {
            using (var dialog = new System.Windows.Forms.SaveFileDialog())
            {
                var now = DateTime.Now;
                dialog.Filter = "Html|*.html|Alle filtyper|*.*";
                dialog.FileName = $"HardHorn_{ArchiveVersion.Id}_{now.ToString("yyyy-MM-dd_HH-mm-ss")}.html";
                if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                {
                    Log($"Eksporterer {NotificationCount} ud af {Notifications.Count} notifikationer til '{dialog.FileName}'.");
                    using (var stream = dialog.OpenFile())
                    {
                        using (var writer = new StreamWriter(stream))
                        {
                            writer.WriteLine("<!doctype html>");
                            writer.WriteLine("<html>");
                            writer.WriteLine("<head>");
                            writer.Write(@"<style>
.sort {display:inline-block; width: 0; height: 0; border-left: 8px solid transparent; border-right: 8px solid transparent; }
.asc {border-bottom: 10px solid black;}
.desc {border-top: 10px solid black;}
</style>
<script>
function sortBy(tableId, sortColumnIndex) {
	var table = document.getElementById(tableId).nextElementSibling;
	var sortTypeElem = table.children[0].children[sortColumnIndex].getElementsByTagName('strong')[0];
    var sortType = '';
    if (sortTypeElem != undefined) {
        sortType = sortTypeElem.innerText;
    }
	var rows = Array.prototype.slice.call(table.children, 1);
	var glyphs = table.getElementsByClassName('sort');
	for (var i = 0; i < glyphs.length; i++) {
		glyphs[i].remove();
	}
	if (table.lastSortType == sortType) {
		table.sortAscending = !table.sortAscending;
		rows.reverse();
	} else {
		table.sortAscending = true;
		table.lastSortType = sortType;
		rows.sort(getSortFunc(sortType, sortColumnIndex)); 
	}
	addGlyph(table, sortColumnIndex);
	readdRows(rows);
}

function getSortFunc(sortType, sortColumnIndex) {
	switch (sortType) {
		case 'Felt':
			return function(row1, row2) {
				var idx = sortColumnIndex;
				var field1 = row1.children[idx].innerText;
				var field2 = row2.children[idx].innerText;
				if (field1 == '-') {
					return -1;
				}
				if (field2 == '-') {
					return 1;
				}
				var f1 = parseInt(field1.substr(2, field1.indexOf(':')-2));
				var f2 = parseInt(field2.substr(2, field2.indexOf(':')-2));
				return f1-f2;
			};
			break;
		case 'Tabel':
			return function(row1, row2) {
				var idx = sortColumnIndex;
				var field1 = row1.children[idx].innerText;
				var field2 = row2.children[idx].innerText;
				if (field1 == '-') {
					return -1;
				}
				if (field2 == '-') {
					return 1;
				}
				var f1 = parseInt(field1.substr(6, field1.indexOf(':')-6));
				var f2 = parseInt(field2.substr(6, field2.indexOf(':')-6));
				return f1-f2;
			};
			break;
        case 'Forekomster':
            return function(row1, row2) {
                var idx = sortColumnIndex;
				var field1 = row1.children[idx].innerText;
				var field2 = row2.children[idx].innerText;
				if (field1 == '-') {
					return -1;
				}
				if (field2 == '-') {
					return 1;
				}
                return parseInt(field1) - parseInt(field2);
            };
            break;
		default:
			return function(row1, row2) {
				var idx = sortColumnIndex;
				var field1 = row1.children[idx].innerText;
				var field2 = row2.children[idx].innerText;
				if (field1 == '-') {
					return -1;
				}
				if (field2 == '-') {
					return 1;
				}
				return field1.localeCompare(field2);
			};
			break;
	}
	
}

function readdRows(rows) {
	for (var i = 0; i < rows.length; i++) {
		var parent = rows[i].parentNode;
		var detached = parent.removeChild(rows[i]);
		parent.appendChild(detached); 
	}
}

function addGlyph(table, columnIndex) {
	var glyph = document.createElement('span');
    glyph.classList.add('sort');
    if (table.sortAscending) {
        glyph.classList.add('asc');
    } else {
        glyph.classList.add('desc');
    }
	table.children[0].children[columnIndex].appendChild(glyph);
}
</script>");
                            writer.WriteLine($"<title>{ArchiveVersion.Id} - HardHorn Log</title>");
                            writer.WriteLine("</head>");
                            writer.WriteLine("<body style=\"font-family: verdana, sans-serif;\">");
                            writer.WriteLine($"<h1>{ArchiveVersion.Id} - HardHorn Log</h1>");
                            writer.WriteLine($"<p><strong>Tidspunkt:</strong> {now}</p>");
                            writer.WriteLine("<h2 id=\"oversigt\">Oversigt</h2>");
                            writer.WriteLine("<ul>");
                            foreach (CollectionViewGroup group in Notifications_SelectedGroupingIndex == 0 ? NotificationsView.Groups : NotificationsCategoryView.Groups)
                            {
                                writer.WriteLine($"<li><a href=\"#{HttpUtility.HtmlEncode(group.Name)}\">{HttpUtility.HtmlEncode(group.Name)} ({group.ItemCount} punkter)</a></li>");
                            }
                            writer.WriteLine("</ul>");
                            writer.WriteLine("<h2>Rapport</h2>");
                            if (Notifications_SelectedGroupingIndex == 0) // Table groups
                            {
                                foreach (CollectionViewGroup group in NotificationsView.Groups)
                                {
                                    writer.WriteLine("<div>");
                                    writer.WriteLine($"<h3 id=\"{HttpUtility.HtmlEncode(group.Name)}\">{HttpUtility.HtmlEncode(group.Name)}&nbsp;<span style=\"font-weight: normal; font-size: 12pt;\"><a href=\"#oversigt\">[til oversigt]</a></span></h3>");
                                    writer.WriteLine("<div style=\"display: table\">");
                                    writer.WriteLine("<div style=\"display: table-row\">");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 0)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 1)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Felt</strong></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 2)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Kategori</strong></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 3)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Forekomster</strong></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 4)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Besked</strong></div>");
                                    writer.WriteLine("</div>");
                                    foreach (INotification notification in group.Items)
                                    {
                                        writer.WriteLine("<div style=\"display: table-row\">");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{(notification.Severity == Severity.Hint ? "<b style=\"background: yellow;\">!</b>" : "<b style=\"background: red; color: white;\">X</b>")}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Column?.ToString() ?? "-")}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(Utilities.NotificationTypeToStringConverter.ConvertNotificationType(notification.Type)?.ToString() ?? "-")}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{notification.Count?.ToString() ?? "-"}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Message?.ToString() ?? "-")}</div>");
                                        writer.WriteLine("</div>");
                                    }
                                    writer.WriteLine("</div>");
                                    writer.WriteLine("</div>");
                                }
                            }
                            else // Category groups
                            {
                                foreach (CollectionViewGroup group in NotificationsCategoryView.Groups)
                                {
                                    writer.WriteLine("<div>");
                                    writer.WriteLine($"<h3 id=\"{HttpUtility.HtmlEncode(group.Name)}\">{HttpUtility.HtmlEncode(group.Name)}&nbsp;<span style=\"font-weight: normal; font-size: 12pt;\"><a href=\"#oversigt\">[til oversigt]</a></span></h3>");
                                    writer.WriteLine("<div style=\"display: table\">");
                                    writer.WriteLine("<div style=\"display: table-row\">");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 0)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 1)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Tabel</strong></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 2)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Felt</strong></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 3)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Forekomster</strong></div>");
                                    writer.WriteLine($"<div onclick=\"sortBy('{HttpUtility.HtmlEncode(group.Name)}', 4)\" style=\"cursor: pointer; display: table-cell; padding: 2pt;\"><strong>Besked</strong></div>");
                                    writer.WriteLine("</div>");
                                    foreach (INotification notification in group.Items)
                                    {
                                        writer.WriteLine("<div style=\"display: table-row\">");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{(notification.Severity == Severity.Hint ? "<b style=\"background: yellow;\">!</b>" : "<b style=\"background: red; color: white;\">X</b>")}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Table?.ToString() ?? "-")}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Column?.ToString() ?? "-")}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{notification.Count?.ToString() ?? "-"}</div>");
                                        writer.WriteLine($"<div style=\"display: table-cell; padding: 2pt;\">{HttpUtility.HtmlEncode(notification.Message?.ToString() ?? "-")}</div>");
                                        writer.WriteLine("</div>");
                                    }
                                    writer.WriteLine("</div>");
                                    writer.WriteLine("</div>");
                                }
                            }
                            writer.WriteLine("</body>");
                            writer.WriteLine("</html>");
                        }
                    }
                }
            }
        }

        public async void Notifications_ExportText()
        {

        }

        public async void ReplaceTableToFile(TableViewModel tableViewModel)
        {
            var table = tableViewModel.Table;
            Stream stream = null;

            try
            {
                using (var dialog = new System.Windows.Forms.SaveFileDialog())
                {
                    dialog.Filter = "Xml|*.xml|Alle filtyper|*.*";
                    if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                    {
                        if (Path.GetFullPath(dialog.FileName).ToLower().StartsWith(Path.GetFullPath(ArchiveVersion.Path).ToLower()))
                        {
                            MessageBox.Show("Du kan ikke gemme erstatningen i arkiveringsversionen.", "Sti valgt i arkiveringsversion", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
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

                IProgress<int> replacements = new Progress<int>(i =>
                {
                    TableReplacementTotalReplacements = i;
                    NotifyOfPropertyChange("TableReplacementTotalReplacements");
                    Log(string.Format("Erstatning fuldført med {0} post-erstatninger.", i), LogLevel.SECTION);
                });
            
                await Task.Run(() =>
                {
                    var replacementOperations = ReplacementOperations.Where(op => op.Table == tableViewModel.Table);
                    var replacer = new TableReplacer(table, replacementOperations, stream);
                    replacer.WriteHeader();
                    var reader = table.GetReader();
                    Post[,] readPosts;
                    int totalRows = 0;
                    int readRows = 0;
                    int totalReplacements = 0;
                    do
                    {
                        readRows = reader.Read(out readPosts);
                        totalRows += readRows;
                        totalReplacements += replacer.Write(readPosts, readRows);
                        progress.Report(totalRows);
                    } while (readRows > 0);
                    replacer.WriteFooter();
                    replacer.Flush();
                    replacements.Report(totalReplacements);
                });
            }
            catch (Exception ex)
            {
                Log(string.Format("En undtagelse af typen '{0}' forekom med beskeden:\n{1}", ex.GetType(), ex.Message, ex.StackTrace), LogLevel.ERROR);
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

            var replacementOperations = ReplacementOperations.Where(op => op.Table == table.Table);

            try {
                using (Stream stream = new MemoryStream())
                {
                    using (var originalTableReader = new TableReader(table.Table))
                    {
                        Post[,] posts;
                        int rowsRead = originalTableReader.Read(out posts, 1000);
                        var replacer = new TableReplacer(table.Table, replacementOperations, stream);
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
                }
            }
            catch (Exception ex)
            {
                Log(string.Format("En undtagelse resulterede i at erstatningsvisningen ikke kunne fuldføres. Undtagelse: {0}, besked: {1}", ex.GetType(), ex.Message), LogLevel.ERROR);
            }
        }

        public void RemoveReplacementOperation(ReplacementOperation replacement)
        {
            ReplacementOperations.Remove(replacement);
        }

        public void AddReplacementOperation(TableViewModel tableViewModel, ColumnViewModel columnViewModel, string pattern, string replacement)
        {
            if (tableViewModel == null || columnViewModel == null)
                return;

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
                ReplacementOperations.Add(new ReplacementOperation(tableViewModel.Table, columnViewModel.Column, regex, replacement));
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
            IEnumerable<TableViewModel> tableViewModelsToTest = SelectedTableViewModels;
            if (tableViewModelsToTest.Count() == 0)
            {
                tableViewModelsToTest = TableViewModels;
            }

            // Create the analyzer
            var startTestViewModel = new StartTestViewModel(this, tableViewModelsToTest, MainLogger);
            var windowSettings = new Dictionary<string, object>();
            windowSettings.Add("Title", string.Format("Start test af {0}", ArchiveVersion.Id));
            bool? success = windowManager.ShowDialog(startTestViewModel, null, windowSettings);
            if (!success.HasValue || !success.Value)
            {
                return;
            }
            Analyzer = startTestViewModel.Analyzer;
            Analyzer.Notify = HandleNotification;

            // Create cancallation token
            _testCts = new CancellationTokenSource();
            var token = _testCts.Token;

            // Connect the column analysis objects to the corresponding column view models
            foreach (var tableViewModel in tableViewModelsToTest)
                foreach (var columnViewModel in tableViewModel.ColumnViewModels)
                    columnViewModel.Analysis = Analyzer.TestHierachy[tableViewModel.Table][columnViewModel.Column];

            Log("Påbegynder dataanalyse.", LogLevel.SECTION);

            // Reset table view model states
            foreach (var tableViewModel in tableViewModelsToTest)
            {
                tableViewModel.AnalysisBusy = false;
                tableViewModel.AnalysisDone = false;
            }

            foreach (var tableViewModel in tableViewModelsToTest)
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
            });

            IProgress<Table> showReportProgress = new Progress<Table>(table =>
            {
                var tableReport = Analyzer.TestHierachy[table];
                Tuple<int, int> errors = tableReport.Values.Aggregate(new Tuple<int, int>(0, 0),
                    (c, r) => new Tuple<int, int>(r.ErrorCount + c.Item1, r.ErrorCount > 0 ? c.Item2 + 1 : c.Item2));
                int suggestions = tableReport.Values.Aggregate(0, (c, r) => r.SuggestedType == null ? c : c + 1);
                Log(string.Format("I alt: {0} fejl i {1} kolonner, {2} forslag.", errors.Item1, errors.Item2, suggestions));
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

                        bool typesSuggested = false;
                        foreach (var report in Analyzer.TestHierachy[Analyzer.CurrentTable].Values)
                        {
                            report.SuggestType(HandleNotification);
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

            var keyTest = new ForeignKeyTest(tables, HandleNotification);

            ForeignKeyTestResults.Clear();

            KeyTestRunning = true;

            var totalRowCountProgress = new Progress<int>(value => { KeyTestTotalRowCount = value; }) as IProgress<int>;
            var totalDoneRowsProgress = new Progress<int>(value => { KeyTestTotalDoneRows = value; }) as IProgress<int>;
            var tableRowCountProgress = new Progress<int>(value => { KeyTestTableRowCount = value; }) as IProgress<int>;
            var tableDoneRowsProgress = new Progress<int>(value => { KeyTestTableDoneRows = value; }) as IProgress<int>;
            var addResultProgress = new Progress<Tuple<Table, Tuple<ForeignKey, int, int, IEnumerable<KeyValuePair<ForeignKeyValue, int>>>>>(tuple =>
            {
                TableViewModelIndex[tuple.Item1.Name].KeyTestErrors |= tuple.Item2.Item2 > 0; // error count greater than 0
                if (tuple.Item2.Item2 > 0)
                {
                    ForeignKeyTestResults.Add(tuple.Item2);
                }
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
            if (ex is ColumnTypeParsingException)
            {
                var avctEx = ex as ColumnTypeParsingException;
                Log(string.Format("En fejl opstod under afkodningen af kolonnen '{0}' i tabellen '{1}': Typen '{2}' er ikke valid.", avctEx.Name, avctEx.Table.Name, avctEx.Type), LogLevel.ERROR);
            }
            else if (ex is ColumnParsingException)
            {
                var avcEx = ex as ColumnParsingException;
                Log(string.Format("En fejl opstod under afkodningen af en kolonne i tabellen '{0}': {1}", avcEx.Table.Name, avcEx.Message), LogLevel.ERROR);
            }
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

        public void GoToUndefinedColumn(ColumnTypeParsingException ex)
        {
            var vm = TableViewModelIndex[ex.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
        }

        public void GoToForeignKeyReferencedColumn(ReferenceViewModel referenceViewModel)
        {
            var vm = TableViewModelIndex[referenceViewModel.Reference.ReferencedColumn.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
            SelectedTableViewModel.SelectedColumnViewModel = SelectedTableViewModel.ColumnViewModels.First(cvm => cvm.Column == referenceViewModel.Reference.ReferencedColumn);
        }

        public void GoToForeignKeyColumn(ReferenceViewModel referenceViewModel)
        {
            var vm = TableViewModelIndex[referenceViewModel.Reference.Column.Table.Name];
            if (vm == null)
                return;

            TabSelectedIndex = (int)TabNameEnum.TAB_ARCHIVEVERSION;
            SelectedTableViewModel = vm;
            SelectedTableViewModel.SelectedColumnViewModel = SelectedTableViewModel.ColumnViewModels.First(cvm => cvm.Column == referenceViewModel.Reference.Column);
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
