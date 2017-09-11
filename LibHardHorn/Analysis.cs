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

    public abstract class Test
    {
        public static readonly int MAX_ERROR_POSTS = 10;

        public enum Result
        {
            ERROR,
            OKAY
        }

        public int ErrorCount { get; private set; }

        List<Post> _posts = new List<Post>();
        public IEnumerable<Post> ErrorPosts { get { return _posts; } }

        public void Add(Post post)
        {
            if (ErrorCount < MAX_ERROR_POSTS)
            {
                _posts.Add(post);
            }
            ErrorCount++;
        }

        public Result Run(Post post, Column column)
        {
            var result = GetResult(post, column);
            if (result == Result.ERROR)
            {
                Add(post);
            }
            return result;
        }

        public abstract Result GetResult(Post post, Column column);

        public class Overflow : Test
        {
            public override Result GetResult(Post post, Column column)
            {
                Match match;
                bool overflow = false;

                switch (column.Type)
                {
                    case DataType.NATIONAL_CHARACTER:
                    case DataType.CHARACTER:
                    case DataType.NATIONAL_CHARACTER_VARYING:
                    case DataType.CHARACTER_VARYING:
                        overflow = post.Data.Length > column.Param[0];
                        break;
                    case DataType.DECIMAL:
                        var components = post.Data.Split('.');
                        // No separator
                        if (components.Length == 1)
                            overflow = components[0].Length > column.Param[0];
                        // With separator
                        if (components.Length == 2)
                            overflow = components[0].Length + components[1].Length > column.Param[0] || components[1].Length > column.Param[1];
                        break;
                    case DataType.TIME:
                        match = Analyzer.time_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.Param != null && match.Groups.Count == 8 && match.Groups[4].Length > column.Param[0];
                        }
                        break;
                    case DataType.TIME_WITH_TIME_ZONE:
                        match = Analyzer.time_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.Param != null && match.Groups.Count == 8 && match.Groups[4].Length > column.Param[0];
                        }
                        break;
                    case DataType.TIMESTAMP:
                        match = Analyzer.timestamp_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.Param != null && match.Groups.Count == 8 && match.Groups[7].Length > column.Param[0];
                        }
                        break;
                    case DataType.TIMESTAMP_WITH_TIME_ZONE:
                        match = Analyzer.timestamp_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.Param != null && match.Groups.Count == 8 && match.Groups[7].Length > column.Param[0];
                        }
                        break;
                }

                return overflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Underflow : Test
        {
            public override Result GetResult(Post post, Column column)
            {
                var underflow = false;
                switch (column.Type)
                {
                    case DataType.NATIONAL_CHARACTER:
                    case DataType.CHARACTER:
                        underflow = post.Data.Length < column.Param[0];
                        break;
                }
                return underflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Blank : Test
        {
            bool IsWhitespace(char c)
            {
                return c == ' ' || c == '\n' || c == '\t' || c == '\r';
            }

            public override Result GetResult(Post post, Column column)
            {
                return post.Data.Length > 0  && (IsWhitespace(post.Data[0]) || IsWhitespace(post.Data[post.Data.Length - 1])) ? Result.ERROR : Result.OKAY;
            }
        }

        public class Pattern : Test
        {
            public Regex Regex { get; private set; }
            public Func<MatchCollection, Result> HandleMatches { get; private set; }

            public Pattern(Regex regex, Func<MatchCollection, Result> handleMatches = null)
            {
                Regex = regex;
                HandleMatches = handleMatches;
            }

            public override Result GetResult(Post post, Column column)
            {
                var matches = Regex.Matches(post.Data);

                if (HandleMatches == null)
                {
                    return matches.Count > 0 ? Result.OKAY : Result.ERROR;
                }

                return HandleMatches(matches);
            }
        }
    }

    public class ColumnAnalysis
    {
        public event EventHandler<ErrorOccuredEventArgs> ErrorOccured = delegate { };

        public class ErrorOccuredEventArgs : EventArgs
        {
            public Test Test { get; private set; }

            public ErrorOccuredEventArgs(Test test)
            {
                Test = test;
            }
        }

        public int ErrorCount { get; private set; }
        public List<Test> Tests { get; private set; }
        public int[] MinLengths { get; private set; }
        public int[] MaxLengths { get; private set; }
        public Tuple<DataType, DataTypeParam> SuggestedType { get; set; }
        public Column Column { get; private set; }
        public bool AnalysisFirstRow { get; set; }

        public ColumnAnalysis(Column column)
        {
            AnalysisFirstRow = false;
            Column = column;
            ErrorCount = 0;
            MinLengths = new int[column.Param != null ? column.Param.Length : 1];
            MaxLengths = new int[column.Param != null ? column.Param.Length : 1];
            Tests = new List<Test>();
        }

        public void RunTests(Post post)
        {
            foreach (var test in Tests)
            {
                var result = test.Run(post, Column);
                if (result == Test.Result.ERROR)
                {
                    ErrorOccured(this, new ErrorOccuredEventArgs(test));
                    ErrorCount++;
                }
            }
        }

        /// <summary>
        /// Update the length measurements given the new data.
        /// </summary>
        /// <param name="data"></param>
        public void UpdateLengthStatistics(string data)
        {
            switch (Column.Type)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (AnalysisFirstRow)
                    {
                        MinLengths[0] = Math.Min(MinLengths[0], data.Length);
                        MaxLengths[0] = Math.Max(MaxLengths[0], data.Length);
                    }
                    else
                    {
                        MinLengths[0] = data.Length;
                        MaxLengths[0] = data.Length;
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (AnalysisFirstRow)
                    {
                        MinLengths[0] = Math.Min(MinLengths[0], data.Length);
                        MaxLengths[0] = Math.Max(MaxLengths[0], data.Length);
                    }
                    else
                    {
                        MinLengths[0] = data.Length;
                        MaxLengths[0] = data.Length;
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    if (AnalysisFirstRow)
                    {
                        MinLengths[0] = Math.Min(MinLengths[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        MaxLengths[0] = Math.Max(MaxLengths[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        MinLengths[1] = Math.Min(MinLengths[1], components.Length == 1 ? 0 : components[1].Length);
                        MaxLengths[1] = Math.Max(MaxLengths[1], components.Length == 1 ? 0 : components[1].Length);
                    }
                    else
                    {
                        MinLengths[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        MaxLengths[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        MinLengths[1] = components.Length == 1 ? 0 : components[1].Length;
                        MaxLengths[1] = components.Length == 1 ? 0 : components[1].Length;
                    }
                    break;
                case DataType.TIME:
                case DataType.DATE:
                case DataType.TIMESTAMP:
                    break;
            }
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
                    if (MaxLengths[0] != Column.Param[0] || MaxLengths[1] != Column.Param[1])
                    {
                        SuggestedType = new Tuple<DataType, DataTypeParam>(DataType.DECIMAL, new DataTypeParam(new int[] { MaxLengths[0], MaxLengths[1] }));
                    }
                    break;
            }
        }
    }

    public class Analyzer
    {
        public ArchiveVersion ArchiveVersion { get; private set; }
        ILogger _log;

        public static Regex date_regex = new Regex(@"(\d\d\d\d)-(\d\d)-(\d\d)$");
        public static Regex timestamp_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?$");
        public static Regex time_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?$");
        public static Regex timestamp_timezone_regex = new Regex(@"^(\d\d\d\d)-(\d\d)-(\d\d)T(\d\d):(\d\d):(\d\d)(?:.(\d+))?(\+|-)(\d\d:\d\d)$");
        public static Regex time_timezone_regex = new Regex(@"^(\d\d):(\d\d):(\d\d)(?:.(\d+))?(\+|-)(\d\d:\d\d)$");
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
            }
        }

        /// <summary>
        /// Analyze the lengths of the columns of the archive version.
        /// </summary>
        /// <param name="report"></param>
        /// <param name="data"></param>
        void AnalyzeLengths(ColumnAnalysis report, string data)
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
                    if (components.Length > 0 && components[0][0] == '-')
                    {
                        components[0] = components[0].Substring(1);
                    }
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
                   month > 12 && month < 1 ||
                   day < 1 ||
                   month == 2 && leap && day > months[2] - 1 ||
                   day > months[month - 1];
        }
    }
}
