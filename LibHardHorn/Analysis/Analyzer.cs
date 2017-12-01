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
        public void AnalyzeRows(Table table, Post[,] rows, int n)
        {
            // analyze the rows
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var post = rows[i,j];
                    TestHierachy[table][table.Columns[j]].UpdateLengthStatistics(post.Data);
                    TestHierachy[table][table.Columns[j]].RunTests(post);
                }

                if (i == 0)
                    foreach (var analysis in TestHierachy[table].Values)
                        analysis.FirstRowAnalyzed = true;
            }

            foreach (var columnAnalysis in TestHierachy[table].Values)
            {
                columnAnalysis.Flush();
            }
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
