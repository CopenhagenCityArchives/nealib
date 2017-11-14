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
    public enum AnalysisErrorType
    {
        OVERFLOW,
        UNDERFLOW,
        FORMAT,
        NULL,
        BLANK,
        REGEX
    }

    public abstract class Test : INotifyPropertyChanged
    {
        public static readonly int MAX_ERROR_POSTS = 50;

        public event PropertyChangedEventHandler PropertyChanged;
        public void NotifyOfPropertyChanged(string propertyName)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
            }
        }

        public enum Result
        {
            ERROR,
            OKAY
        }

        public int ErrorCount { get; private set; }
        public abstract string Name { get; }

        List<Post> _posts = new List<Post>();
        public IEnumerable<Post> ErrorPosts { get { return _posts; } }

        public void Add(Post post)
        {
            if (ErrorCount < MAX_ERROR_POSTS)
            {
                _posts.Add(post);
                NotifyOfPropertyChanged("ErrorPosts");
            }
            ErrorCount++;
            NotifyOfPropertyChanged("ErrorCount");
        }

        public Result Run(Post post, Column column)
        {
            if (post.IsNull)
                return Result.OKAY;

            var result = GetResult(post, column);
            if (result == Result.ERROR)
            {
                Add(post);
            }
            return result;
        }

        public abstract Result GetResult(Post post, Column column);

        #region Predefined format tests
        public static Test DateFormatTest()
        {
            return new Pattern(Analyzer.date_regex, m => {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);

                if (Analyzer.invalidDate(year, month, day))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, "DATE format");
        }

        public static Test TimeFormatTest()
        {
            return new Pattern(Analyzer.time_regex, m => {
                int hour = int.Parse(m[0].Groups[1].Value);
                int minute = int.Parse(m[0].Groups[2].Value);
                int second = int.Parse(m[0].Groups[3].Value);

                if (Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, "TIME format");
        }

        public static Test TimeWithTimeZoneTest()
        {
            return new Pattern(Analyzer.time_timezone_regex, m => {
                int hour = int.Parse(m[0].Groups[1].Value);
                int minute = int.Parse(m[0].Groups[2].Value);
                int second = int.Parse(m[0].Groups[3].Value);
                if (m[0].Groups[5].Value == "Z")
                {

                }
                else
                {
                    int tzHour = int.Parse(m[0].Groups[6].Value);
                    int tzMinute = int.Parse(m[0].Groups[7].Value);
                    if (tzHour > 12 || tzMinute > 59)
                        return Result.ERROR;
                }

                if (Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, "TIME format");
        }

        public static Test TimestampWithTimeZoneFormatTest()
        {
            return new Pattern(Analyzer.timestamp_timezone_regex, m => {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);
                int hour = int.Parse(m[0].Groups[4].Value);
                int minute = int.Parse(m[0].Groups[5].Value);
                int second = int.Parse(m[0].Groups[6].Value);
                if (m[0].Groups[8].Value == "Z")
                {

                }
                else
                {
                    int tzHour = int.Parse(m[0].Groups[9].Value);
                    int tzMinute = int.Parse(m[0].Groups[10].Value);
                    if (tzHour > 12 || tzMinute > 59)
                        return Result.ERROR;
                }

                if (Analyzer.invalidDate(year, month, day) || Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, "TIMESTAMP WITH TIME ZONE format");
        }

        public static Test TimestampFormatTest()
        {
            return new Pattern(Analyzer.timestamp_regex, m =>
            {
                int year = int.Parse(m[0].Groups[1].Value);
                int month = int.Parse(m[0].Groups[2].Value);
                int day = int.Parse(m[0].Groups[3].Value);
                int hour = int.Parse(m[0].Groups[4].Value);
                int minute = int.Parse(m[0].Groups[5].Value);
                int second = int.Parse(m[0].Groups[6].Value);

                if (Analyzer.invalidDate(year, month, day) || Analyzer.invalidTime(hour, minute, second))
                {
                    return Result.ERROR;
                }

                return Result.OKAY;
            }, "TIMESTAMP format");
        }
        #endregion

        public class Overflow : Test
        {
            public override string Name { get { return "Overskridelse"; } }

            public override Result GetResult(Post post, Column column)
            {
                Match match;
                bool overflow = false;

                switch (column.ParameterizedDataType.DataType)
                {
                    case DataType.NATIONAL_CHARACTER:
                    case DataType.CHARACTER:
                    case DataType.NATIONAL_CHARACTER_VARYING:
                    case DataType.CHARACTER_VARYING:
                        overflow = post.Data.Length > column.ParameterizedDataType.Parameter[0];
                        break;
                    case DataType.DECIMAL:
                        var components = post.Data.Split('.');
                        // Remove negation
                        if (components.Length > 0 && components[0][0] == '-')
                        {
                            components[0] = components[0].Substring(1);
                        }
                        // No separator
                        if (components.Length == 1)
                            overflow = components[0].Length > column.ParameterizedDataType.Parameter[0];
                        // With separator
                        if (components.Length == 2)
                            overflow = components[0].Length + components[1].Length > column.ParameterizedDataType.Parameter[0] || components[1].Length > column.ParameterizedDataType.Parameter[1];
                        break;
                    case DataType.TIME:
                        match = Analyzer.time_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[4].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                    case DataType.TIME_WITH_TIME_ZONE:
                        match = Analyzer.time_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[4].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                    case DataType.TIMESTAMP:
                        match = Analyzer.timestamp_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[7].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                    case DataType.TIMESTAMP_WITH_TIME_ZONE:
                        match = Analyzer.timestamp_timezone_regex.Match(post.Data);
                        if (match.Success)
                        {
                            overflow = column.ParameterizedDataType.Parameter != null && match.Groups.Count == 8 && match.Groups[7].Length > column.ParameterizedDataType.Parameter[0];
                        }
                        break;
                }

                return overflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Underflow : Test
        {
            public override string Name { get { return "Underudfyldelse"; } }

            public override Result GetResult(Post post, Column column)
            {
                var underflow = false;
                switch (column.ParameterizedDataType.DataType)
                {
                    case DataType.NATIONAL_CHARACTER:
                    case DataType.CHARACTER:
                        underflow = post.Data.Length < column.ParameterizedDataType.Parameter[0];
                        break;
                }
                return underflow ? Result.ERROR : Result.OKAY;
            }
        }

        public class Blank : Test
        {
            public override string Name { get { return "Blanktegn"; } }

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
            string _name;
            public override string Name { get { return _name; } }

            public Regex Regex { get; private set; }
            public Func<MatchCollection, Result> HandleMatches { get; private set; }

            public Pattern(Regex regex, Func<MatchCollection, Result> handleMatches = null, string name = null)
            {
                Regex = regex;
                HandleMatches = handleMatches;
                _name = name ?? regex.ToString();
            }

            public override Result GetResult(Post post, Column column)
            {
                var matches = Regex.Matches(post.Data);

                if (HandleMatches == null || matches.Count == 0)
                {
                    return matches.Count > 0 ? Result.OKAY : Result.ERROR;
                }

                return HandleMatches(matches);
            }
        }

        internal void Clear()
        {
            ErrorCount = 0;
            _posts.Clear();
        }
    }

    public abstract class AnalysisErrorOccuredBase
    {
        public event AnalysisErrorOccuredEventHandler AnalysisErrorOccured;
        public delegate void AnalysisErrorOccuredEventHandler(object sender, AnalysisErrorOccuredArgs e);
        protected virtual void NotifyOfAnalysisErrorOccured(Test test)
        {
            if (AnalysisErrorOccured != null)
                AnalysisErrorOccured(this, new AnalysisErrorOccuredArgs(test));
        }
    }


    public class ColumnAnalysis : AnalysisErrorOccuredBase, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged = delegate { };

        Test _selectedTest;
        public Test SelectedTest { get { return _selectedTest; } set { _selectedTest = Tests.IndexOf(value) == -1 ? _selectedTest : value; PropertyChanged(this, new PropertyChangedEventArgs("SelectedTest")); } }

        public int ErrorCount { get; private set; }
        public List<Test> Tests { get; private set; }
        public DataTypeParam MinParam { get; private set; }
        public DataTypeParam MaxParam { get; private set; }
        public ParameterizedDataType SuggestedType { get; set; }
        public Column Column { get; private set; }
        public bool FirstRowAnalyzed { get; set; }

        public ColumnAnalysis(Column column)
        {
            FirstRowAnalyzed = false;
            Column = column;
            ErrorCount = 0;
            MinParam = new DataTypeParam(new int[column.ParameterizedDataType.Parameter != null ? column.ParameterizedDataType.Parameter.Length : 1]);
            MaxParam = new DataTypeParam(new int[column.ParameterizedDataType.Parameter != null ? column.ParameterizedDataType.Parameter.Length : 1]);
            Tests = new List<Test>();
        }

        public void RunTests(Post post)
        {
            foreach (var test in Tests)
            {
                var result = test.Run(post, Column);
                if (result == Test.Result.ERROR)
                {
                    ErrorCount++;
                    PropertyChanged(this, new PropertyChangedEventArgs("ErrorCount"));
                    NotifyOfAnalysisErrorOccured(test);
                }
            }
        }

        /// <summary>
        /// Update the length measurements given the new data.
        /// </summary>
        /// <param name="data"></param>
        public void UpdateLengthStatistics(string data)
        {
            switch (Column.ParameterizedDataType.DataType)
            {
                case DataType.NATIONAL_CHARACTER:
                case DataType.CHARACTER:
                    if (FirstRowAnalyzed)
                    {
                        MinParam[0] = Math.Min(MinParam[0], data.Length);
                        MaxParam[0] = Math.Max(MaxParam[0], data.Length);
                    }
                    else
                    {
                        MinParam[0] = data.Length;
                        MaxParam[0] = data.Length;
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                case DataType.CHARACTER_VARYING:
                    if (FirstRowAnalyzed)
                    {
                        MinParam[0] = Math.Min(MinParam[0], data.Length);
                        MaxParam[0] = Math.Max(MaxParam[0], data.Length);
                    }
                    else
                    {
                        MinParam[0] = data.Length;
                        MaxParam[0] = data.Length;
                    }
                    break;
                case DataType.DECIMAL:
                    var components = data.Split('.');
                    if (components.Length > 0 && components[0].Length > 0 && components[0][0] == '-')
                    {
                        components[0] = components[0].Substring(1);
                    }
                    if (FirstRowAnalyzed)
                    {
                        MinParam[0] = Math.Min(MinParam[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        MaxParam[0] = Math.Max(MaxParam[0], components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length);
                        MinParam[1] = Math.Min(MinParam[1], components.Length == 1 ? 0 : components[1].Length);
                        MaxParam[1] = Math.Max(MaxParam[1], components.Length == 1 ? 0 : components[1].Length);
                    }
                    else
                    {
                        MinParam[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        MaxParam[0] = components.Length == 1 ? components[0].Length : components[0].Length + components[1].Length;
                        MinParam[1] = components.Length == 1 ? 0 : components[1].Length;
                        MaxParam[1] = components.Length == 1 ? 0 : components[1].Length;
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
            switch (Column.ParameterizedDataType.DataType)
            {
                case DataType.CHARACTER:
                    if (MinParam[0] == MaxParam[0] && MaxParam[0] > Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER, new DataTypeParam(MaxParam[0]));
                    }
                    else if (MinParam[0] != MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER_VARYING, new DataTypeParam(MaxParam[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER:
                    if (MinParam[0] == MaxParam[0] && MaxParam[0] > Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER, new DataTypeParam(MaxParam[0]));
                    }
                    else if (MinParam[0] != MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, new DataTypeParam(MaxParam[0]));
                    }
                    break;
                case DataType.CHARACTER_VARYING:
                    if (MinParam[0] == MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER, new DataTypeParam(MaxParam[0]));
                    }
                    else if (MaxParam[0] != Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.CHARACTER_VARYING, new DataTypeParam(MaxParam[0]));
                    }
                    break;
                case DataType.NATIONAL_CHARACTER_VARYING:
                    if (MinParam[0] == MaxParam[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER, new DataTypeParam(MaxParam[0]));
                    }
                    else if (MaxParam[0] != Column.ParameterizedDataType.Parameter[0])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.NATIONAL_CHARACTER_VARYING, new DataTypeParam(MaxParam[0]));
                    }
                    break;
                case DataType.DECIMAL:
                    if (MaxParam[0] != Column.ParameterizedDataType.Parameter[0] || MaxParam[1] != Column.ParameterizedDataType.Parameter[1])
                    {
                        SuggestedType = new ParameterizedDataType(DataType.DECIMAL, new DataTypeParam(new int[] { MaxParam[0], MaxParam[1] }));
                    }
                    break;
            }

            PropertyChanged(this, new PropertyChangedEventArgs("SuggestedType"));
        }

        public void Clear()
        {
            ErrorCount = 0;
            Tests.Clear();
        }
    }

    public class AnalysisErrorOccuredArgs : EventArgs
    {
        public AnalysisErrorOccuredArgs(Test test) {
            Test = test;
        }

        public Test Test { get; set; }
        public Test.Result Result { get; set; }
    }

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
