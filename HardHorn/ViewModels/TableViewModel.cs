using Caliburn.Micro;
using HardHorn.Analysis;
using HardHorn.Archiving;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace HardHorn.ViewModels
{
    public class BrowseRow : PropertyChangedBase
    {
        bool _isSelected;
        public bool IsSelected
        {
            get { return _isSelected; }
            set { _isSelected = value; NotifyOfPropertyChange("IsSelected"); }
        }

        public List<Post> Posts { get; private set; }

        public BrowseRow(Post[] posts)
        {
            Posts = new List<Post>(posts);
        }
    }

    public class TableViewModel : PropertyChangedBase
    {
        public Table Table { get; set; }

        public ObservableCollection<ForeignKeyViewModel> ForeignKeyViewModels { get; private set; }

        uint _browseOffset;
        public uint BrowseOffset
        {
            get { return _browseOffset; }
            set
            {
                if (value > Table.Rows) return;
                _browseOffset = value;
                NotifyOfPropertyChange("BrowseOffset");
            }
        }

        uint _browseCount;
        public uint BrowseCount
        {
            get { return _browseCount; }
            set
            {
                _browseCount = value;
                NotifyOfPropertyChange("BrowseCount");
            }
        }

        public string Description
        {
            get { return Table.Description; }
            set { Table.Description = value; NotifyOfPropertyChange("Description"); }
        }

        public ColumnViewModel _selectedColumnViewModel;
        public ColumnViewModel SelectedColumnViewModel
        {
            get { return _selectedColumnViewModel; }
            set { _selectedColumnViewModel = value; NotifyOfPropertyChange("SelectedColumnViewModel"); }
        }
        public ObservableCollection<ColumnViewModel> ColumnViewModels { get; private set; }

        ObservableCollection<BrowseRow> _browseRows;
        public ObservableCollection<BrowseRow> BrowseRows
        {
            get
            {
                if (_browseRows.Count == 0)
                {
                    UpdateBrowseRows();
                }
                return _browseRows;
            }
        }

        bool _keyTestErrors = false;
        public bool KeyTestErrors
        {
            get { return _keyTestErrors; }
            set { _keyTestErrors = value; NotifyOfPropertyChange("KeyTestErrors"); }
        }

        private bool _keyTestDone = false;
        public bool KeyTestDone
        {
            get { return _keyTestDone; }
            set { _keyTestDone = value; NotifyOfPropertyChange("KeyTestDone"); }
        }

        bool _keyTestBusy = false;
        public bool KeyTestBusy
        {
            get { return _keyTestBusy; }
            set { _keyTestBusy = value; NotifyOfPropertyChange("KeyTestBusy"); }
        }

        bool _analysisErrors = false;
        public bool AnalysisErrors
        {
            get { return _analysisErrors; }
            set { _analysisErrors = value; NotifyOfPropertyChange("AnalysisErrors"); }
        }

        private bool _analysisDone = false;
        public bool AnalysisDone
        {
            get { return _analysisDone; }
            set { _analysisDone = value; NotifyOfPropertyChange("AnalysisDone"); }
        }

        bool _analysisBusy = false;
        public bool AnalysisBusy
        {
            get { return _analysisBusy; }
            set { _analysisBusy = value; NotifyOfPropertyChange("AnalysisBusy"); }
        }

        int _browseReadProgress = 0;
        public int BrowseReadProgress {  get { return _browseReadProgress; } set { _browseReadProgress = value;  NotifyOfPropertyChange("BrowseReadProgress"); } }
        bool _BrowseReady = true;
        public bool BrowseReady { get { return _BrowseReady; } set { _BrowseReady = value; NotifyOfPropertyChange("BrowseReady"); } }

        DataTable _rowDataTable;
        public DataTable RowDataTable
        {
            get
            {
                if (_rowDataTable.Rows.Count == 0)
                {
                    UpdateBrowseRows();
                }
                return _rowDataTable;
            }
            set { _rowDataTable = value; NotifyOfPropertyChange("RowDataTable"); }
        }

        public TableViewModel(Table table)
        {
            Table = table;
            ColumnViewModels = new ObservableCollection<ColumnViewModel>(table.Columns.Select(c => new ColumnViewModel(c)));
            ForeignKeyViewModels = new ObservableCollection<ForeignKeyViewModel>(table.ForeignKeys.Select(fkey => new ForeignKeyViewModel(this, fkey)));
            AnalysisErrors = ColumnViewModels.Any(cvm => cvm.Column.ParameterizedDataType.DataType == DataType.UNDEFINED);
            BrowseOffset = 0;
            BrowseCount = 20;
            RowDataTable = new DataTable();
            _browseRows = new ObservableCollection<BrowseRow>();
            foreach (var column in table.Columns)
            {
                var dataColumn = new DataColumn(string.Format("<{0}: {1}>", column.ColumnId, column.Name.Replace("_", "__")), typeof(Post));
                dataColumn.Caption = column.ColumnIdNumber.ToString();
                _rowDataTable.Columns.Add(dataColumn);
            }
        }

        public async void UpdateBrowseRows()
        {
            if (!BrowseReady || Table == null || BrowseOffset < 0 || BrowseCount < 0)
                return;

            BrowseReady = false;
            BrowseReadProgress = 0;
            var browseReadProgress = new Progress<int>(p => { BrowseReadProgress = p; }) as IProgress<int>;

            IEnumerable<BrowseRow> browseRows = Enumerable.Empty<BrowseRow>();
            try
            {
                browseRows = await Task.Run(() =>
                {
                    // Local copies
                    uint browseOffset = BrowseOffset;
                    uint browseCount = BrowseCount;
                    var result = new List<BrowseRow>();

                    int currentOffset = 0;
                    int rowsRead = 0;
                    Post[,] posts;
                    using (var reader = Table.GetReader())
                    {
                        if (browseOffset > 0)
                        {
                            uint chunkSize = 50000;
                            uint chunks = browseOffset / chunkSize;
                            uint chunkExtra = browseOffset % chunkSize;

                            for (int c = 0; c < chunks; c++)
                            {
                                rowsRead = reader.Read(out posts, (int)chunkSize);
                                currentOffset += rowsRead;
                                browseReadProgress.Report((int)((currentOffset * 100) / browseOffset));
                            }
                            rowsRead = reader.Read(out posts, (int)chunkExtra);
                            currentOffset += rowsRead;
                            browseReadProgress.Report((int)((currentOffset * 100) / browseOffset));
                        }
                        rowsRead = reader.Read(out posts, (int)browseCount);
                        currentOffset += rowsRead;
                    }

                    for (int i = 0; i < rowsRead; i++)
                    {
                        var rowPosts = new Post[Table.Columns.Count];
                        for (int j = 0; j < Table.Columns.Count; j++)
                        {
                            rowPosts[j] = posts[i, j];
                        }
                        result.Add(new BrowseRow(rowPosts));
                    }

                    return result;
                });
            }
            catch (Exception)
            {
                // ignore failure
            }
            finally
            {
                RowDataTable.Rows.Clear();
                foreach (var row in browseRows)
                {
                    try
                    {
                        RowDataTable.Rows.Add(row.Posts.ToArray());
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine(ex.Message, ex.StackTrace);
                    }
                }

                BrowseReady = true;
                _browseRows.Clear();
                foreach (var row in browseRows) _browseRows.Add(row);
            }
        }
    }
}
