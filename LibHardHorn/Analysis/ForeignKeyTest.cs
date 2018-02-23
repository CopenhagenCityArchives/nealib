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

        private IDictionary<ForeignKey, ISet<ForeignKeyValue>> _valueMap;
        private IDictionary<ForeignKey, int> _errorCountMap;
        private IDictionary<ForeignKey, IDictionary<ForeignKeyValue, int>> _errorMap;

        private IEnumerator<Table> _tableEnumerator;
        private IEnumerator<ForeignKey> _foreignKeyEnumerator;
        private TableReader _tableReader;

        private Post[,] _rows;

        public int GetErrorCount(ForeignKey foreignKey)
        {
            return _errorCountMap[foreignKey];
        }

        public int GetErrorTypeCount(ForeignKey foreignKey)
        {
            return _errorMap[foreignKey].Count;
        }

        public IEnumerable<KeyValuePair<ForeignKeyValue, int>> GetOrderedErrorCounts(ForeignKey foreignKey)
        {
            return new List<KeyValuePair<ForeignKeyValue, int>>(_errorMap[foreignKey].OrderBy(kv => kv.Value).Reverse());
        }

        public ForeignKeyTest(IEnumerable<Table> tables)
        {
            Tables = new List<Table>(tables);
            _tableEnumerator = Tables.GetEnumerator();

            _valueMap = new Dictionary<ForeignKey, ISet<ForeignKeyValue>>();
            _errorCountMap = new Dictionary<ForeignKey, int>();
            _errorMap = new Dictionary<ForeignKey, IDictionary<ForeignKeyValue, int>>();
            foreach (var table in Tables)
            {
                foreach (var foreignKey in table.ForeignKeys)
                {
                    _errorCountMap[foreignKey] = 0;
                    _errorMap[foreignKey] = new Dictionary<ForeignKeyValue, int>();
                }
            }

            TotalDoneRows = 0;
            TotalRowCount = 0;
            foreach (var table in Tables)
            {
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
            _valueMap.Clear();
            foreach (var foreignKey in CurrentTable.ForeignKeys)
            {
                _valueMap[foreignKey] = new HashSet<ForeignKeyValue>();
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
                _valueMap[CurrentForeignKey].Add(CurrentForeignKey.GetReferencedValueFromRow(i, _rows));
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
                    if (!_valueMap[foreignKey].Contains(key))
                    {
                        _errorCountMap[foreignKey]++;
                        if (_errorMap[foreignKey].ContainsKey(key))
                        {
                            _errorMap[foreignKey][key]++;
                        }
                        else
                        {
                            _errorMap[foreignKey][key] = 1;
                        }
                    }
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
