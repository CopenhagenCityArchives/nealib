using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using HardHorn.Archiving;
using HardHorn.Utility;

namespace HardHorn.Analysis
{
    public class ForeignKeyTest : IDisposable
    {
        public IEnumerable<Table> Tables { get; private set; }
        public Table CurrentTable { get { return _tableEnumerator == null ? null : _tableEnumerator.Current; } }
        public ForeignKey CurrentForeignKey { get { return _foreignKeyEnumerator == null ? null : _foreignKeyEnumerator.Current; } }
        public int TableDoneRows { get; private set; }
        public int TableRowCount { get; private set; }
        public int TotalDoneRows { get; private set; }
        public int TotalRowCount { get; private set; }

        private int _readRows = 0;

        private IDictionary<ForeignKey, ISet<ForeignKeyValue>> valueMap;
        private IDictionary<ForeignKey, int> errorCountMap;
        private IDictionary<ForeignKey, int> blankCountMap;
        private IDictionary<ForeignKey, IDictionary<ForeignKeyValue, int>> errorMap;

        private IEnumerator<Table> _tableEnumerator;
        private IEnumerator<ForeignKey> _foreignKeyEnumerator;
        private TableReader _tableReader;

        private NotificationCallback Notify { get;set;}

        private Post[,] _rows;

        public int GetErrorCount(ForeignKey foreignKey)
        {
            return errorCountMap[foreignKey];
        }

        public int GetErrorTypeCount(ForeignKey foreignKey)
        {
            return errorMap[foreignKey].Count;
        }

        public IEnumerable<KeyValuePair<ForeignKeyValue, int>> GetOrderedErrorCounts(ForeignKey foreignKey)
        {
            return new List<KeyValuePair<ForeignKeyValue, int>>(errorMap[foreignKey].OrderBy(kv => kv.Value).Reverse());
        }

        public ForeignKeyTest(IEnumerable<Table> tables, NotificationCallback notify)
        {
            Tables = new List<Table>(tables);
            _tableEnumerator = Tables.GetEnumerator();

            Notify = notify;

            valueMap = new Dictionary<ForeignKey, ISet<ForeignKeyValue>>();
            errorCountMap = new Dictionary<ForeignKey, int>();
            blankCountMap = new Dictionary<ForeignKey, int>();
            errorMap = new Dictionary<ForeignKey, IDictionary<ForeignKeyValue, int>>();
            foreach (var table in Tables)
            {
                foreach (var foreignKey in table.ForeignKeys)
                {
                    blankCountMap[foreignKey] = 0;
                    errorCountMap[foreignKey] = 0;
                    errorMap[foreignKey] = new Dictionary<ForeignKeyValue, int>();
                }
            }

            TotalDoneRows = 0;
            TotalRowCount = 0;
            foreach (var table in Tables)
            {
                if (table.ForeignKeys.Count == 0)
                    continue;

                TotalRowCount += table.Rows;
                foreach (var foreignTable in table.ForeignKeys.Select(fkey => fkey.ReferencedTable))
                {
                    TotalRowCount += foreignTable.Rows;
                }
            }
        }

        public void InitializeTable()
        {
            TableDoneRows = 0;
            TableRowCount = CurrentTable.Rows;
            foreach (var foreignTable in CurrentTable.ForeignKeys.Select(fkey => fkey.ReferencedTable))
            {
                TableRowCount += foreignTable.Rows;
            }
        }

        public bool MoveNextTable()
        {
            if (_tableEnumerator.MoveNext())
            {
                InitializeTable();
                return true;
            }

            if (_tableReader != null)
            {
                _tableReader.Dispose();
            }

            return false;
        }

        public void InitializeReferencedValueLoading()
        {
            valueMap.Clear();
            foreach (var foreignKey in CurrentTable.ForeignKeys)
            {
                valueMap[foreignKey] = new HashSet<ForeignKeyValue>();
            }
            if (_foreignKeyEnumerator != null)
            {
                _foreignKeyEnumerator.Dispose();
                _foreignKeyEnumerator = null;
            }
            _foreignKeyEnumerator = CurrentTable.ForeignKeys.GetEnumerator();
        }

        public bool MoveNextForeignKey()
        {
            if (_foreignKeyEnumerator.MoveNext())
            {
                if (_tableReader != null)
                {
                    _tableReader.Dispose();
                }
                _tableReader = CurrentForeignKey.ReferencedTable.GetReader();
                return true;
            }

            if (_tableReader != null)
            {
                _tableReader.Dispose();
                _tableReader = null;
            }

            return false;
        }

        public bool ReadReferencedForeignKeyValue(int chunk = 10000)
        {
            _readRows = _tableReader.Read(out _rows, chunk);

            for (int i = 0; i < _readRows; i++)
            {
                valueMap[CurrentForeignKey].Add(CurrentForeignKey.GetReferencedValueFromRow(i, _rows));
            }

            TableDoneRows += _readRows;
            TotalDoneRows += _readRows;

            return _readRows == chunk;
        }

        public void InitializeTableTest()
        {
            if (_tableReader != null)
            {
                _tableReader.Dispose();
            }

            _tableReader = CurrentTable.GetReader();
        }

        public bool ReadForeignKeyValue(int chunk = 10000)
        {
            _readRows = _tableReader.Read(out _rows, chunk);

            for (int i = 0; i < _readRows; i++)
            {
                foreach (var foreignKey in CurrentTable.ForeignKeys)
                {
                    var key = foreignKey.GetValueFromRow(i, _rows);
                    if (key.Values.Any(post => post.IsNull))
                    {
                        blankCountMap[foreignKey]++;
                    }
                    else if (!valueMap[foreignKey].Contains(key))
                    {
                        errorCountMap[foreignKey]++;
                        if (errorMap[foreignKey].ContainsKey(key))
                        {
                            errorMap[foreignKey][key]++;
                        }
                        else
                        {
                            errorMap[foreignKey][key] = 1;
                        }
                    }
                }
            }

            foreach (var foreignKey in errorCountMap.Keys)
            {
                if (errorCountMap[foreignKey] == 0)
                    continue;

                if (errorCountMap[foreignKey] > 0)
                {
                    Notify(new ForeignKeyTestErrorNotification(foreignKey, errorCountMap[foreignKey], errorMap[foreignKey]));
                }

                if (blankCountMap[foreignKey] > 0)
                {
                    Notify(new ForeignKeyTestBlankNotification(foreignKey, blankCountMap[foreignKey]));
                }
            }

            TableDoneRows += _readRows;
            TotalDoneRows += _readRows;

            return _readRows == chunk;
        }

        public void Dispose()
        {
            if (_tableReader != null)
            {
                _tableReader.Dispose();
            }
            if (_tableEnumerator != null)
            {
                _tableEnumerator.Dispose();
            }
            if (_foreignKeyEnumerator != null)
            {
                _foreignKeyEnumerator.Dispose();
            }
        }
    }
}
