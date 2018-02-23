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

        public static Regex date_regex = new Regex(@"(\d\d\d\d)-(\d\d)-(\d\d)$");
        public static Regex timestamp_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?$");
        public static Regex time_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?$");
        public static Regex timestamp_timezone_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?((?:\+|-)(\d\d):(\d\d)|Z)$");
        public static Regex time_timezone_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?((?:\+|-)(\d\d):(\d\d)|Z)$");
        public static int[] months = new int[] { 31, 29, 31, 30, 31, 30, 31, 33, 30, 31, 30, 31 };

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
            if (TestHierachy[column.Table][column].Tests.Count == 1)
            {
                TestHierachy[column.Table][column].SelectedTest = test;
            }
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

            foreach (var columnAnalysis in TestHierachy[CurrentTable].Values)
            {
                columnAnalysis.Flush();
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

        public static bool invalidTime(int hour, int minute, int second)
        {
            return hour > 23 ||
                   hour < 0 ||
                   minute > 59 ||
                   minute < 0 ||
                   second > 59 ||
                   second < 0;
        }

        public static bool invalidDate(int year, int month, int day)
        {
            bool leap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
            return year <= 0 ||
                   month > 12 ||
                   month < 1 ||
                   day < 1 ||
                   month == 2 && leap && day > months[2] - 1 ||
                   day > months[month - 1];
        }
    }
}
