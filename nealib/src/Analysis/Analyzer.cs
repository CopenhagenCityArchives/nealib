﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Xml;
using System.Xml.Linq;
using NEA.Archiving;
using System.Text.RegularExpressions;
using System.Dynamic;
using NEA.Logging;
using System.ComponentModel;
using System.Collections.ObjectModel;
using NEA.Utility;

namespace NEA.Analysis
{
    public class AnalysisTestException : Exception
    {
        public Post Post { get; private set; }
        public Test Test { get; private set; }

        public AnalysisTestException(Exception exception, Post post, Test test) : base(string.Format("An exception of type {0} occured, when testing {1} for {2}", exception.GetType(), post, test), exception)
        {
            Post = post;
            Test = test;
        }
    }

    /// <summary>
    /// Encapsulates the analysis of the table data.
    /// </summary>
    public class Analyzer
    {
        public static int SampleSize = 50;

        /// <summary>
        /// The archive version whose data will be analyzed.
        /// </summary>
        public ArchiveVersion ArchiveVersion { get; private set; }

        /// <summary>
        /// The number of rows analyzed of the current table.
        /// </summary>
        public int TableDoneRows { get; private set; }

        /// <summary>
        /// The row count of the current table.
        /// </summary>
        public int TableRowCount { get; private set; }

        /// <summary>
        /// The number of rows analyzed in total.
        /// </summary>
        public int TotalDoneRows { get; private set; }

        /// <summary>
        /// The total row count of all tables to be analyzed.
        /// </summary>
        public int TotalRowCount { get; private set; }

        /// <summary>
        /// The currently selected table.
        /// </summary>
        public Table CurrentTable { get { return _tableEnumerator == null ? null : _tableEnumerator.Current; } }

        /// <summary>
        /// The hierachy of tests.
        /// </summary>
        public Dictionary<Table, Dictionary<Column, ColumnAnalysis>> TestHierachy { get; private set; }

        private IEnumerator<Table> _tableEnumerator;
        ILogger _log;
        private TableReader _tableReader;
        private int _readRows = 0;

        public NotificationCallback Notify { get; set; }

        /// <summary>
        /// Construct an analyzer object.
        /// </summary>
        /// <param name="archiveVersion">The archive version whose data will be analyzed.</param>
        /// <param name="log">The logger, which will receive logging calls from the analyzer.</param>
        public Analyzer(ArchiveVersion archiveVersion, IEnumerable<Table> selectedTables, ILogger log)
        {
            _log = log;
            ArchiveVersion = archiveVersion;

            TestHierachy = new Dictionary<Table, Dictionary<Column, ColumnAnalysis>>();
            foreach (var table in selectedTables)
            {
                TestHierachy.Add(table, new Dictionary<Column, ColumnAnalysis>());
                foreach (var column in table.Columns)
                {
                    TestHierachy[table].Add(column, new ColumnAnalysis(column));
                }
            }

            TotalDoneRows = 0;
            TotalRowCount = selectedTables.Aggregate(0, (n, t) => n + t.Rows);
            _tableEnumerator = selectedTables.GetEnumerator();
        }

        /// <summary>
        /// Add a test to a column.
        /// </summary>
        /// <param name="column">The column that will be tested.</param>
        /// <param name="test">The test that will be performed.</param>
        public void AddTest(Column column, Test test)
        {
            TestHierachy[column.Table][column].Tests.Add(test);
        }

        /// <summary>
        /// Analyze rows of the archive version.
        /// </summary>
        /// <param name="n">The number of rows to analyze.</param>
        /// <returns>The number of rows analyzed.</returns>
        /// <exception cref="System.InvalidOperationException">Thrown when called and the current table is not initialized.</exception>
        public bool AnalyzeRows(int n = 10000)
        {
            if (_tableReader == null)
            {
                throw new InvalidOperationException("A table must be initialized before rows can be analyzed.");
            }

            Post[,] rows;
            _readRows = _tableReader.ReadN(out rows, n, TableDoneRows);

            // analyze the rows
            for (int i = 0; i < _readRows; i++)
            {
                for (int j = 0; j < CurrentTable.Columns.Count; j++)
                {
                    var post = rows[i, j];
                    TestHierachy[CurrentTable][CurrentTable.Columns[j]].UpdateColumnStatistics(post);
                    TestHierachy[CurrentTable][CurrentTable.Columns[j]].RunTests(post, Notify);
                }

                if (i == 0)
                    foreach (var analysis in TestHierachy[CurrentTable].Values)
                        analysis.FirstRowAnalyzed = true;
            }

            TableDoneRows += _readRows;
            TotalDoneRows += _readRows;

            return _readRows == n;
        }

        /// <summary>
        /// Initialize the analyzer for the current table.
        /// </summary>
        /// <exception cref="System.InvalidOperationException">Thrown when a table is not selected.</exception>
        public void InitializeTable()
        {
            if (CurrentTable == null)
            {
                throw new InvalidOperationException("A table must be selected, before the analyzer can initialize it.");
            }

            TableDoneRows = 0;
            TableRowCount = CurrentTable.Rows;

            if (_tableReader != null)
            {
                _tableReader.Dispose();
            }

            _tableReader = CurrentTable.GetReader();
        }

        /// <summary>
        /// Advances the table enumerator of the analyzer to the next table.
        /// </summary>
        /// <returns></returns>
        public bool MoveNextTable()
        {
            if (_tableEnumerator.Current != null && _tableEnumerator.Current.Rows != TableDoneRows)
            {
                Notify?.Invoke(new TableRowCountNotification(_tableEnumerator.Current, TableDoneRows));
            }

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

        /// <summary>
        /// Creates a new table index based on the old, with suggestions added.
        /// </summary>
        /// <returns></returns>
        public TableIndex CreateTableIndexFromSuggestions(bool applyOnlyWhereError)
        {
            var newTables = new List<Table>();
            foreach (var table in ArchiveVersion.Tables)
            {
                var newColumns = new List<Column>();
                foreach (var column in table.Columns)
                {
                    var suggestion = TestHierachy[table][column].SuggestedType;
                    bool include = !applyOnlyWhereError;
                    if (applyOnlyWhereError)
                    {
                        include = TestHierachy[table][column].Tests.Any(t => t.Type == AnalysisTestType.FORMAT || t.Type == AnalysisTestType.OVERFLOW && t.ErrorCount > 0);
                    }

                    if (include && suggestion != null)
                    {
                        newColumns.Add(new Column(table,
                            column.Name,
                            suggestion,
                            column.DataTypeOriginal,
                            column.Nullable,
                            column.Description,
                            column.ColumnId,
                            column.ColumnIdNumber,
                            column.DefaultValue,
                            column.FunctionalDescription));
                    }
                    else
                    {
                        newColumns.Add(column);
                    }
                }
                newTables.Add(new Table(table.Name, table.Folder, table.Rows, table.Description, newColumns, table.PrimaryKey, table.ForeignKeys));
            }
            var tableIndex = ArchiveVersion.TableIndex;
            return new TableIndex(tableIndex.Version, tableIndex.DBName, tableIndex.DatabaseProduct, newTables, tableIndex.Views);
        }
    }
}
