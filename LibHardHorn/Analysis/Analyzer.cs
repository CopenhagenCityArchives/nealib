using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using HardHorn.Archiving;
using System.Text.RegularExpressions;
using System.Dynamic;
using HardHorn.Logging;
using System.ComponentModel;
using System.Collections.ObjectModel;
using HardHorn.Utility;

namespace HardHorn.Analysis
{
    public class Analyzer
    {
        public ArchiveVersion ArchiveVersion { get; private set; }
        ILogger _log;

        public int TableDoneRows { get; private set; }
        public int TableRowCount { get; private set; }
        public int TotalDoneRows { get; private set; }
        public int TotalRowCount { get; private set; }

        private IEnumerator<Table> _tableEnumerator;
        public Table CurrentTable { get { return _tableEnumerator == null ? null : _tableEnumerator.Current; } }
        private TableReader _tableReader;

        private int _readRows = 0;

        public Dictionary<Table, Dictionary<Column, ColumnAnalysis>> TestHierachy { get; private set; }

        public Analyzer(ArchiveVersion archiveVersion, ILogger log)
        {
            _log = log;
            ArchiveVersion = archiveVersion;

            TestHierachy = new Dictionary<Table, Dictionary<Column, ColumnAnalysis>>();
            foreach (var table in archiveVersion.Tables)
            {
                TestHierachy.Add(table, new Dictionary<Column, ColumnAnalysis>());
                foreach (var column in table.Columns)
                {
                    TestHierachy[table].Add(column, new ColumnAnalysis(column));
                }
            }

            TotalDoneRows = 0;
            TotalRowCount = ArchiveVersion.Tables.Aggregate(0, (n, t) => n + t.Rows);
            _tableEnumerator = ArchiveVersion.Tables.GetEnumerator();
        }

        public void AddTest(Column column, Test test)
        {
            TestHierachy[column.Table][column].Tests.Add(test);
        }

        /// <summary>
        /// Analyze rows of the archive version.
        /// </summary>
        /// <param name="n">The number of rows to analyze.</param>
        /// <returns>The number of rows analyzed.</returns>
        public bool AnalyzeRows(int n = 10000)
        {
            Post[,] rows;
            _readRows = _tableReader.Read(out rows, n);

            // analyze the rows
            for (int i = 0; i < _readRows; i++)
            {
                for (int j = 0; j < CurrentTable.Columns.Count; j++)
                {
                    var post = rows[i,j];
                    TestHierachy[CurrentTable][CurrentTable.Columns[j]].UpdateLengthStatistics(post.Data);
                    TestHierachy[CurrentTable][CurrentTable.Columns[j]].RunTests(post);
                }

                if (i == 0)
                    foreach (var analysis in TestHierachy[CurrentTable].Values)
                        analysis.FirstRowAnalyzed = true;
            }

            TableDoneRows += _readRows;
            TotalDoneRows += _readRows;

            return _readRows == n;
        }

        public void InitializeTable()
        {
            TableDoneRows = 0;
            TableRowCount = CurrentTable.Rows;
            
            if (_tableReader != null)
            {
                _tableReader.Dispose();
            }

            _tableReader = CurrentTable.GetReader();
        }

        public bool MoveNextTable()
        {
            if (_tableEnumerator.MoveNext())
            {
                return true;
            }

            if (_tableReader != null)
            {
                _tableReader.Dispose();
            }

            return false;
        }
    }
}
