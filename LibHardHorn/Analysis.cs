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

namespace HardHorn.Analysis
{
    public enum AnalysisErrorType
    {
        OVERFLOW,
        UNDERFLOW,
        FORMAT,
        NULL,
        BLANK,
        REGEX
    }

    public class AnalysisError
    {
        int _count = 0;
        public int Count { get { return _count; } }
        List<Post> _instances = new List<Post>();

        public IEnumerable<Post> Posts { get { return _instances; } }
        public AnalysisErrorType Type { get; private set; }
        public RegexTest Regex { get; private set; }

        public AnalysisError(AnalysisErrorType type, RegexTest regex = null)
        {
            Type = type;
            Regex = regex;
        }

        public void Add(Post post)
        {
            if (_count < AnalysisReport.MAX_ERROR_INSTANCES)
            {
                _instances.Add(post);
            }
            _count++;
        }

        public override string ToString()
        {
            string repr = string.Format("{{{0}, count={1}}}", Type, Count);
            foreach (dynamic instance in _instances)
            {
                repr += Environment.NewLine + string.Format("[{0},{1}]({2})", instance.Line, instance.Pos, instance.Data);
            }
            return repr;
        }
    }

    public class AnalysisReport
    {
        public int ErrorCount { get; private set; }
        public Column Column { get; set; }
        public Dictionary<AnalysisErrorType, AnalysisError> Errors { get; set; }
        public int[] MinLengths { get; set; }
        public int[] MaxLengths { get; set; }
        public static readonly int MAX_ERROR_INSTANCES = 10;
        public Tuple<DataType, DataTypeParam> SuggestedType { get; set; }
        public bool AnalysisFirstRow { get; set; }

        public AnalysisReport(Column column)
        {
            AnalysisFirstRow = false;
            Column = column;
            ErrorCount = 0;
            MinLengths = new int[column.Param != null ? column.Param.Length : 1];
            MaxLengths = new int[column.Param != null ? column.Param.Length : 1];
            Errors = new Dictionary<AnalysisErrorType, AnalysisError>();
        }

        public void ReportError(Post post, AnalysisErrorType errorType, RegexTest regexTest = null)
        {
            ErrorCount++;
            if (!Errors.ContainsKey(errorType))
            {
                Errors.Add(errorType, new AnalysisError(errorType, regexTest));
            }

            Errors[errorType].Add(post);
        }

        public void SuggestType()
        {
            switch (Column.Type)
            {
                case DataType.CHARACTER:
                    if (MinLengths[0] == MaxLengths[0] && MaxLengths[0] > Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MinLengths[0] != MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER:
                    if (MinLengths[0] == MaxLengths[0] && MaxLengths[0] > Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MinLengths[0] != MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.CHARACTER_VARYING:
                    if (MinLengths[0] == MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MaxLengths[0] != Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (MinLengths[0] == MaxLengths[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER, new DataTypeParam(MaxLengths[0]));
                    }
                    else if (MaxLengths[0] != Column.Param[0])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.NATIONAL_CHARACTER_VARYING, new DataTypeParam(MaxLengths[0]));
                    }
                    break;
                case DataType.DECIMAL:
                    if (MaxLengths[0] + MaxLengths[1] != Column.Param[0] || MaxLengths[1] != Column.Param[1])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.DECIMAL, new DataTypeParam(new int[] { MaxLengths[0] + MaxLengths[1], MaxLengths[1] }));
                    }
                    break;
            }
        }

        public override string ToString()
        {
            string repr = string.Format("[{0}, {1}{2}{3}]",
                Column.Name,
                Column.Type.ToString(),
                Column.Param != null && Column.Param.Length > 0 ? "(" + string.Join(",", Column.Param) + ")" : string.Empty,
                Column.Nullable ? ", nullable" : string.Empty,
                ErrorCount);

            foreach (var errorType in Errors.Keys)
            {
                var errors = Errors[errorType];
                if (errors.Count > 0)
                {
                    repr += Environment.NewLine + string.Join(Environment.NewLine, errors);
                }
            }
            return repr;
        }
    }

    public class DataAnalyzer
    {
        ArchiveVersion _archiveVersion;
        public ArchiveVersion ArchiveVersion { get { return _archiveVersion; } }
        public Dictionary<DataType, HashSet<AnalysisErrorType>> TestSelection { get; set; }
        public IEnumerable<RegexTest> RegexTests { get; set; }
        ILogger _log;

        Regex date_regex = new Regex(@"(\d\d\d\d)-(\d\d)-(\d\d)$");
        Regex timestamp_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?$");
        Regex time_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?$");
        Regex timestamp_timezone_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?(\+|-)(\d\d:\d\d)$");
        Regex time_timezone_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?(\+|-)(\d\d:\d\d)$");
        static int[] months = new int[] { 31, 29, 31, 30, 31, 30, 31, 33, 30, 31, 30, 31 };

        public Dictionary<string, Dictionary<string, AnalysisReport>> Report { get; private set; }

        public DataAnalyzer(ArchiveVersion archiveVersion, ILogger log)
        {
            _log = log;
            _archiveVersion = archiveVersion;
            TestSelection = new Dictionary<DataType, HashSet<AnalysisErrorType>>();
            Report = new Dictionary<string, Dictionary<string, AnalysisReport>>();
            PrepareReports();
        }

        public void PrepareReports()
        {
            Report.Clear();
            foreach (var table in ArchiveVersion.Tables)
            {
                var columnReports = new Dictionary<string, AnalysisReport>();
                foreach (var column in table.Columns)
                {
                    columnReports.Add(column.Name, new AnalysisReport(column));
                }
                Report.Add(table.Name, columnReports);
            }
        }

        /// <summary>
        /// Analyze rows of the archive version.
        /// </summary>
        /// <param name="n">The number of rows to analyze.</param>
        /// <returns>The number of rows analyzed.</returns>
        public void AnalyzeRows(Table table, Post[,] rows, int n)
        {
            // analyize the rows
            for (int i = 0; i < n; i++)
            {
                for (int j = 0; j < table.Columns.Count; j++)
                {
                    var post = rows[i,j];
                    AnalyzeLengths(Report[table.Name][table.Columns[j].Name], post.Data);
                    AnalyzePost(table.Columns[j], post, Report[table.Name][table.Columns[j].Name]);
                }
            }
        }

        /// <summary>
        /// Analyze the lengths of the columns of the archive version.
        /// </summary>
        /// <param name="report"></param>
        /// <param name="data"></param>
        void AnalyzeLengths(AnalysisReport report, string data)
        {
            switch (report.Column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (report.AnalysisFirstRow)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);
                    }
                    else
                    {
                        report.MinLengths[0] = data.Length;
                        report.MaxLengths[0] = data.Length;
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (report.AnalysisFirstRow)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], data.Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], data.Length);
                    }
                    else
                    {
                        report.MinLengths[0] = data.Length;
                        report.MaxLengths[0] = data.Length;
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    if (report.AnalysisFirstRow)
                    {
                        report.MinLengths[0] = Math.Min(report.MinLengths[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        report.MaxLengths[0] = Math.Max(report.MaxLengths[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        report.MinLengths[1] = Math.Min(report.MinLengths[1], components.Length == 1 ? 0 : components[1].Length);
                        report.MaxLengths[1] = Math.Max(report.MaxLengths[1], components.Length == 1 ? 0 : components[1].Length);
                    }
                    else
                    {
                        report.MinLengths[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        report.MaxLengths[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        report.MinLengths[1] = components.Length == 1 ? 0 : components[1].Length;
                        report.MaxLengths[1] = components.Length == 1 ? 0 : components[1].Length;
                    }
                    break;
                case DataType.TIME:
                case DataType.DATE:
                case DataType.TIMESTAMP:
                    break;
            }
        }

        /// <summary>
        /// Analyze a post.
        /// </summary>
        /// <param name="line"></param>
        /// <param name="pos"></param>
        /// <param name="column"></param>
        /// <param name="data"></param>
        /// <param name="isNull"></param>
        /// <param name="report"></param>
        void AnalyzePost(Column column, Post post, AnalysisReport report)
        {
            if (RegexTests != null)
            {
                foreach (var regexTest in RegexTests)
                {
                    if (regexTest.ShouldPerformMatch(column) && !post.IsNull && !regexTest.MatchData(post.Data))
                    {
                        report.ReportError(post, AnalysisErrorType.REGEX, regexTest);
                    }
                }
            }

            Match match;
            if (!TestSelection.ContainsKey(column.Type))
            {
                return;
            }
            var currentTests = TestSelection[column.Type];
            if (currentTests.Count == 0)
            {
                return;
            }

            switch (column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (currentTests.Contains(AnalysisErrorType.UNDERFLOW) && post.Data.Length < column.Param[0])
                    {
                        report.ReportError(post, AnalysisErrorType.UNDERFLOW);
                    }
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && post.Data.Length > column.Param[0])
                    {
                        report.ReportError(post, AnalysisErrorType.OVERFLOW);
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    // Data too long
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && post.Data.Length > column.Param[0])
                    {
                        report.ReportError(post, AnalysisErrorType.OVERFLOW);
                    }
                    break;
                case DataType.DECIMAL:
                    var components = post.Data.Split('.');
                    // No separator
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && components.Length == 1)
                    {
                        if (components[0].Length > column.Param[0])
                        {
                            report.ReportError(post, AnalysisErrorType.OVERFLOW);
                        }
                    }
                    // With separator
                    if (currentTests.Contains(AnalysisErrorType.OVERFLOW) && components.Length == 2)
                    {
                        if (components[0].Length + components[1].Length > column.Param[0] || components[1].Length > column.Param[1])
                        {
                            report.ReportError(post, AnalysisErrorType.OVERFLOW);
                        }
                    }
                    break;
                case DataType.DATE:
                    match = date_regex.Match(post.Data);
                    if (match.Success)
                    {
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        if (currentTests.Contains(AnalysisErrorType.FORMAT) && invalidDate(year, month, day))
                        {
                            report.ReportError(post, AnalysisErrorType.FORMAT);
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(post.Data.Length == 0 && column.Nullable && post.IsNull))
                    {
                        report.ReportError(post, AnalysisErrorType.FORMAT);
                    }
                    break;
                case DataType.TIME:
                    match = time_regex.Match(post.Data);
                    if (match.Success)
                    {
                        if (currentTests.Contains(AnalysisErrorType.OVERFLOW) &&
                            column.Param != null && match.Groups.Count == 8 && match.Groups[4].Length > column.Param[0])
                        {
                            report.ReportError(post, AnalysisErrorType.OVERFLOW);
                        }
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        if (currentTests.Contains(AnalysisErrorType.FORMAT) && invalidDate(year, month, day))
                        {
                            report.ReportError(post, AnalysisErrorType.FORMAT);
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(post.Data.Length == 0 && column.Nullable && post.IsNull))
                    {
                        report.ReportError(post, AnalysisErrorType.FORMAT);
                    }
                    break;
                case DataType.TIME_WITH_TIME_ZONE:
                    match = time_timezone_regex.Match(post.Data);
                    if (match.Success)
                    {
                        if (currentTests.Contains(AnalysisErrorType.OVERFLOW) &&
                            column.Param != null && match.Groups.Count == 8 && match.Groups[4].Length > column.Param[0])
                        {
                            report.ReportError(post, AnalysisErrorType.OVERFLOW);
                        }
                        int year = int.Parse(match.Groups[1].Value);
                        int month = int.Parse(match.Groups[2].Value);
                        int day = int.Parse(match.Groups[3].Value);
                        if (currentTests.Contains(AnalysisErrorType.FORMAT) && invalidDate(year, month, day))
                        {
                            report.ReportError(post, AnalysisErrorType.FORMAT);
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(post.Data.Length == 0 && column.Nullable && post.IsNull))
                    {
                        report.ReportError(post, AnalysisErrorType.FORMAT);
                    }
                    break;
                case DataType.TIMESTAMP:
                    match = timestamp_regex.Match(post.Data);
                    if (match.Success)
                    {
                        if (currentTests.Contains(AnalysisErrorType.OVERFLOW) &&
                            column.Param != null && match.Groups.Count == 8 && match.Groups[7].Length > column.Param[0])
                        {
                            report.ReportError(post, AnalysisErrorType.OVERFLOW);
                        }

                        if (currentTests.Contains(AnalysisErrorType.FORMAT))
                        {
                            // validate datetime
                            int year = int.Parse(match.Groups[1].Value);
                            int month = int.Parse(match.Groups[2].Value);
                            int day = int.Parse(match.Groups[3].Value);
                            int hour = int.Parse(match.Groups[4].Value);
                            int minute = int.Parse(match.Groups[5].Value);
                            int second = int.Parse(match.Groups[6].Value);

                            if (invalidDate(year, month, day) || invalidTime(hour, minute, second))
                            {
                                report.ReportError(post, AnalysisErrorType.FORMAT);
                            }
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(post.Data.Length == 0 && column.Nullable && post.IsNull))
                    {
                        report.ReportError(post, AnalysisErrorType.FORMAT);
                    }
                    break;
                case DataType.TIMESTAMP_WITH_TIME_ZONE:
                    match = timestamp_timezone_regex.Match(post.Data);
                    if (match.Success)
                    {
                        if (currentTests.Contains(AnalysisErrorType.OVERFLOW) &&
                            column.Param != null && match.Groups.Count == 8 && match.Groups[7].Length > column.Param[0])
                        {
                            report.ReportError(post, AnalysisErrorType.OVERFLOW);
                        }

                        if (currentTests.Contains(AnalysisErrorType.FORMAT))
                        {
                            // validate datetime
                            int year = int.Parse(match.Groups[1].Value);
                            int month = int.Parse(match.Groups[2].Value);
                            int day = int.Parse(match.Groups[3].Value);
                            int hour = int.Parse(match.Groups[4].Value);
                            int minute = int.Parse(match.Groups[5].Value);
                            int second = int.Parse(match.Groups[6].Value);

                            if (invalidDate(year, month, day) || invalidTime(hour, minute, second))
                            {
                                report.ReportError(post, AnalysisErrorType.FORMAT);
                            }
                        }
                    }
                    else if (currentTests.Contains(AnalysisErrorType.FORMAT) && !(post.Data.Length == 0 && column.Nullable && post.IsNull))
                    {
                        report.ReportError(post, AnalysisErrorType.FORMAT);
                    }
                    break;
            }

            if (currentTests.Contains(AnalysisErrorType.BLANK) && post.Data.Length > 0 &&
                (post.Data[0] == ' ' ||
                 post.Data[0] == '\n' ||
                 post.Data[0] == '\t' ||
                 post.Data[post.Data.Length - 1] == ' ' ||
                 post.Data[post.Data.Length - 1] == '\n' ||
                 post.Data[post.Data.Length - 1] == '\t'))
            {
                report.ReportError(post, AnalysisErrorType.BLANK);
            }

            report.AnalysisFirstRow = true;
        }

        bool invalidTime(int hour, int minute, int second)
        {
            return hour > 23 ||
                   hour < 0 ||
                   minute > 59 ||
                   minute < 0 ||
                   second > 60 ||
                   second < 0;
        }

        bool invalidDate(int year, int month, int day)
        {
            bool leap = (year % 4 == 0 && year % 100 != 0) || (year % 400 == 0);
            return year <= 0 ||
                   month > 12 && month < 1 ||
                   day < 1 ||
                   month == 2 && leap && day > months[2] - 1 ||
                   day > months[month - 1];
        }
    }

    public class RegexTest
    {
        public Regex Regex { get; private set; }
        public Dictionary<string, HashSet<string>> Columns { get; private set; }

        public RegexTest(Regex regex, Dictionary<string, HashSet<string>> columns)
        {
            Regex = regex;
            Columns = columns;
        }

        public bool ShouldPerformMatch(Column column)
        {
            return Columns.ContainsKey(column.Table.Name) && Columns[column.Table.Name].Contains(column.Name);
        }

        public bool MatchData(string data)
        {
            var match = Regex.Match(data);
            return match.Success;
        }
    }
}
